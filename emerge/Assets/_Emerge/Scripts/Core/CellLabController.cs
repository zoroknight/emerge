using System.Collections.Generic;
using Emerge.Cells;
using Emerge.Presentation;
using Emerge.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Emerge.Core
{
    public enum LabMode { Edit, Swim }
    public enum LabExample { Straight, Turn, Reverse, Feeding, Filter, Membrane, Contraction }

    public sealed class CellLabController : MonoBehaviour
    {
        [SerializeField] private CellDefinition core;
        [SerializeField] private CellDefinition cilia;
        [SerializeField] private CellDefinition absorber;
        [SerializeField] private CellView absorberPrefab;
        [SerializeField] private CellDefinition membrane;
        [SerializeField] private CellView membranePrefab;
        [SerializeField] private CellDefinition contractor;
        [SerializeField] private CellView contractorPrefab;
        private GameObject membraneTrialFlow;
        private int membraneTrialIndex = -1;
        [SerializeField] private MetabolismSettings metabolismSettings;
        [SerializeField] private NutrientParticle nutrientPrefab;
        [SerializeField] private LocalFlowSettings localFlowSettings;
        [SerializeField] private CellView corePrefab;
        [SerializeField] private CellView ciliaPrefab;
        [SerializeField] private Transform cellsRoot;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CellLabPanel panel;
        [SerializeField] private Material connectionMaterial;
        [SerializeField] private InputActionAsset inputControls;
        [SerializeField, Range(2, 30)] private int capacity = 20;
        [SerializeField] private CellExperiment[] experimentTemplates = new CellExperiment[0];
        public ExperimentLibrary Experiments { get; private set; }
        public ExperimentMenu ExperimentMenu { get; private set; }

        private readonly List<CellView> cells = new List<CellView>();
        private CellView selected;
        private CellView dragged;
        private Vector3 dragOffset;
        private HashSet<CellView> dragGroup;
        private bool dragMoved;
        private int exampleIndex = -1;
        public CellGraph Graph { get; } = new CellGraph();
        public CellView PrimaryCore { get; private set; }
        public string Message { get; private set; } = "拖近其他细胞后松开即可连接。";
        public LabMode Mode { get; private set; } = LabMode.Edit;
        public bool IsEditing => Mode == LabMode.Edit;
        public bool IsDragging => dragged != null;
        public IReadOnlyList<CellView> Cells => cells;
        public int Capacity => capacity;
        public CellView Selected => selected;
        public CellConnection SelectedConnection { get; private set; }
        public CellLabPanel Panel => panel;
        public CellLabPhysics Physics { get; private set; }
        public CellDefinition CoreDefinition => core;
        public CellDefinition CiliaDefinition => cilia;
        public CellDefinition AbsorberDefinition => absorber;
        public CellDefinition MembraneDefinition => membrane;
        public CellDefinition ContractorDefinition => contractor;
        public MetabolismState Metabolism { get; private set; }
        public NutrientWorld Food { get; private set; }
        public bool ShowFlow { get; private set; } = true;
        public const float ArenaTop = 0.72f;

        private void Start()
        {
            Metabolism = new MetabolismState(metabolismSettings);
            Food = gameObject.AddComponent<NutrientWorld>(); Food.Initialize(this, nutrientPrefab, localFlowSettings, worldCamera);
            panel.Initialize(this);
            var connections = new GameObject("连接显示").AddComponent<CellConnectionsView>();
            connections.transform.SetParent(transform, false);
            connections.Initialize(this, connectionMaterial);
            Physics = gameObject.AddComponent<CellLabPhysics>();
            Physics.Initialize(this, inputControls, connectionMaterial, worldCamera);
            var motion = new GameObject("纤毛方向显示").AddComponent<CellMotionView>();
            motion.transform.SetParent(transform, false);
            motion.Initialize(this, connectionMaterial);
            var flowView = new GameObject("局部流场显示").AddComponent<CellFlowView>();
            flowView.transform.SetParent(transform, false); flowView.Initialize(this, worldCamera, connectionMaterial);
            Experiments = new ExperimentLibrary(this, experimentTemplates);
            ExperimentMenu = gameObject.AddComponent<ExperimentMenu>();
            ExperimentMenu.Initialize(this);
            ResetLab();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            bool stress = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--cell-lab-stress") >= 0;
#if UNITY_EDITOR
            stress |= UnityEditor.SessionState.GetBool("Emerge.RunT05", false);
            UnityEditor.SessionState.EraseBool("Emerge.RunT05");
#endif
            if (stress)
            {
                Application.runInBackground = true;
                gameObject.AddComponent<CellStressCheck>();
            }
            else if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "--cell-lab-smoke") >= 0)
            {
                Application.runInBackground = true;
                gameObject.AddComponent<CellLabSmokeCheck>();
            }
#endif
        }

        public void SpawnCore() => Spawn(core, corePrefab);
        public void SpawnCilia() => Spawn(cilia, ciliaPrefab);
        public void SpawnAbsorber() => Spawn(absorber, absorberPrefab);
        public void SpawnMembrane() => Spawn(membrane, membranePrefab);
        public void SpawnContractor() => Spawn(contractor, contractorPrefab);
        public void LoadContractionExample()
        {
            if (!IsEditing) return;
            ClearLab(); SpawnCore(); SpawnContractor(); SpawnAbsorber();
            cells[1].transform.position = new Vector3(0, -1, 0);
            cells[0].transform.position = cells[1].transform.position + Vector3.left * (core.radius + contractor.radius);
            cells[2].transform.position = cells[1].transform.position + Vector3.right * (contractor.radius + absorber.radius);
            Connect(cells[0], cells[1]); Connect(cells[1], cells[2]);
            Graph.ConfigureCoreChannel(Graph.Edges[0], PrimaryCore, IntentChannel.A);
            Select(cells[1]); Metabolism.Reset(2, 8);
            SetMessage("收缩示例：Tab 游动，按住 A 缩短两侧连接，松开复原；W 无响应。收缩耗能，0 添加收缩细胞。");
        }
        public void LoadNextMembraneTrial()
        {
            if (!IsEditing) return;
            membraneTrialIndex = (membraneTrialIndex + 1) % 3;
            LoadMembraneTrial(membraneTrialIndex);
        }

        // All three trials use the same food coordinates, initial resources and prescribed stream.
        // 0: guiding membrane, 1: no membrane, 2: membrane tilted the other way.
        public void LoadMembraneTrial(int variant)
        {
            if (!IsEditing || capacity < 3) return;
            ClearLab(); SpawnCore(); SpawnAbsorber();
            cells[1].transform.position = new Vector3(0, -1, 0);
            cells[0].transform.position = cells[1].transform.position + Vector3.down * (core.radius + absorber.radius);
            Connect(cells[0], cells[1]);
            if (variant != 1)
            {
                SpawnMembrane();
                float gap = absorber.radius + membrane.radius;
                cells[2].transform.position = new Vector3(-0.66f, -1 + Mathf.Sqrt(gap * gap - 0.66f * 0.66f), 0);
                cells[2].transform.rotation = Quaternion.Euler(0, 0, variant == 2 ? -45 : 45);
                Connect(cells[1], cells[2]); Select(cells[2]);
            }
            membraneTrialFlow = new GameObject("膜对照试验水流（离开示例自动移除）");
            membraneTrialFlow.transform.SetParent(transform, false);
            membraneTrialFlow.transform.position = new Vector3(-1, -1, 0);
            var region = membraneTrialFlow.AddComponent<SceneFlowRegion>();
            region.radius = 8; region.speed = 0.8f; region.fadeAtEdge = false;
            Food.Clear(); Metabolism.Reset(0, 5);
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 6; column++)
                    Food.Spawn(new Vector2(-3.5f - column * 0.3f, 0.20f + row * 0.06f));
            SetMessage((variant == 1 ? "无膜" : variant == 2 ? "错误朝向" : "膜导流") + "：Tab 游动，无需 WASD；观察颗粒路径与累计摄食。返回编辑后 9 切换对照。");
        }
        public void SeedFood() => Food.SeedPatch();
        public void ToggleFlowDisplay() => ShowFlow = !ShowFlow;

        public void LoadNextExample()
        {
            if (!IsEditing) return;
            exampleIndex = (exampleIndex + 1) % 7;
            LoadExample((LabExample)exampleIndex);
        }

        public void LoadExample(LabExample example)
        {
            if (!IsEditing || capacity < 3) return;
            if (example == LabExample.Membrane) { LoadMembraneTrial(0); return; }
            if (example == LabExample.Contraction) { LoadContractionExample(); return; }
            if (example == LabExample.Filter)
            {
                if (capacity < 4) { SetMessage("滤食示例需要四个细胞的容量。"); return; }
                ClearLab(); SpawnCore(); SpawnAbsorber(); SpawnCilia(); SpawnCilia();
                Vector3 filterCenter = new Vector3(0, -1, 0);
                cells[1].transform.position = filterCenter;
                cells[0].transform.position = filterCenter + Vector3.left * (core.radius + absorber.radius);
                float filterGap = absorber.radius + cilia.radius;
                cells[2].transform.position = filterCenter + Vector3.up * filterGap;
                cells[3].transform.position = filterCenter + Vector3.down * filterGap;
                cells[2].transform.rotation = Quaternion.Euler(0, 0, -90);
                cells[3].transform.rotation = Quaternion.Euler(0, 0, 90);
                Connect(cells[0], cells[1]); Connect(cells[1], cells[2]); Connect(cells[1], cells[3]); Select(cells[1]);
                Metabolism.Reset(0, 5);
                Food.RestoreSceneNutrients();
                SetMessage("滤食示例：7 投放远处营养，Tab 游动，W 将颗粒送入吸收区；两侧反作用力抵消，6 显示水流。"); return;
            }
            if (example == LabExample.Feeding)
            {
                ClearLab(); SpawnCore(); SpawnCilia(); SpawnAbsorber();
                cells[0].transform.position = new Vector3(0, -1, 0);
                cells[1].transform.position = cells[0].transform.position + Vector3.up * (core.radius + cilia.radius);
                cells[1].transform.rotation = Quaternion.Euler(0, 0, 90);
                cells[2].transform.position = cells[0].transform.position + Vector3.right * (core.radius + absorber.radius);
                Connect(cells[0], cells[1]); Connect(cells[0], cells[2]); Select(cells[2]);
                Metabolism.Reset(0, 1);
                Food.RestoreSceneNutrients();
                SetMessage("摄食示例：按 W 耗能停工，再按 5 投放营养，观察吸收和恢复。"); return;
            }
            ClearLab(); SpawnCore(); SpawnCilia(); SpawnCilia();
            Vector3 center = new Vector3(0, -1, 0);
            float gap = core.radius + cilia.radius;
            cells[0].transform.position = center;
            cells[1].transform.position = center + Vector3.up * gap;
            cells[2].transform.position = center + Vector3.down * gap;
            cells[1].transform.rotation = Quaternion.Euler(0, 0, example == LabExample.Reverse ? 0 : 180);
            cells[2].transform.rotation = Quaternion.Euler(0, 0, example == LabExample.Reverse ? 0 : example == LabExample.Turn ? 90 : 180);
            Connect(cells[0], cells[1]); Connect(cells[0], cells[2]); Select(cells[0]);
            Food.RestoreSceneNutrients();
            string title = example == LabExample.Straight ? "直行" : example == LabExample.Turn ? "偏转" : "反向";
            SetMessage("已载入" + title + "示例：同为核心 + 两个纤毛。按住 W 预览，Tab 游动。");
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void PrepareStressLab(int count, float cameraSize)
        {
            if (!IsEditing) ToggleMode();
            ClearLab();
            capacity = Mathf.Clamp(count, 2, 30);
            worldCamera.orthographicSize = cameraSize;
            Physics.RebuildArena();
        }

        public CellView CreateStressCell(CellKind kind, Vector3 position, float rotation)
        {
            if (!IsEditing || cells.Count >= capacity) throw new System.InvalidOperationException("压力测试节点数量无效。");
            var data = kind == CellKind.Core ? core : kind == CellKind.Cilia ? cilia : kind == CellKind.Membrane ? membrane : kind == CellKind.Contractor ? contractor : absorber;
            var prefab = kind == CellKind.Core ? corePrefab : kind == CellKind.Cilia ? ciliaPrefab : kind == CellKind.Membrane ? membranePrefab : kind == CellKind.Contractor ? contractorPrefab : absorberPrefab;
            var cell = Instantiate(prefab, position, Quaternion.Euler(0, 0, rotation), cellsRoot);
            cell.Initialize(data); cell.name = "压力样本 " + cells.Count;
            cell.Body.interpolation = RigidbodyInterpolation2D.None;
            cells.Add(cell);
            if (PrimaryCore == null && kind == CellKind.Core) PrimaryCore = cell;
            return cell;
        }
#endif

        private void Spawn(CellDefinition data, CellView prefab)
        {
            if (!IsEditing || cells.Count >= capacity) return;
            int slot = cells.Count;
            Vector3 position = Vector3.zero;
            bool available = false;
            for (int i = 0; i < capacity; i++)
            {
                position = new Vector3(-5.1f + i % 5 * 2.55f, 1.8f - i / 5 * 2f, 0);
                if (Fits(data.radius, position, null)) { available = true; break; }
            }
            if (!available) { SetMessage("没有空闲生成位置，请移动细胞后再添加。"); return; }
            CellView cell = Instantiate(prefab, position, Quaternion.identity, cellsRoot);
            cell.name = data.displayName + " " + (slot + 1);
            cell.Initialize(data);
            cells.Add(cell);
            if (PrimaryCore == null && data.kind == CellKind.Core) PrimaryCore = cell;
            Select(cell);
        }

        public void ResetLab()
        {
            if (!IsEditing) return;
            if (Experiments != null && Experiments.IsActive) { Experiments.Load(Experiments.Active); return; }
            ClearLab();
            SpawnCore();
            SpawnCilia();
            Food.RestoreSceneNutrients();
        }

        public void ClearLab()
        {
            if (!IsEditing) return;
            Experiments?.End();
            EndDrag(false);
            Physics?.StopSimulation();
            Graph.Clear();
            if (membraneTrialFlow != null) { membraneTrialFlow.SetActive(false); Destroy(membraneTrialFlow); membraneTrialFlow = null; }
            Food?.Clear(); Metabolism?.Reset();
            PrimaryCore = null;
            selected = null;
            SelectedConnection = null;
            foreach (CellView cell in cells)
            {
                // Destroy is deferred; hide samples immediately before respawning.
                cell.gameObject.SetActive(false);
                Destroy(cell.gameObject);
            }
            cells.Clear();
            panel.Refresh();
        }

        public void Select(CellView cell)
        {
            if (selected != null) selected.SetSelected(false);
            selected = cell;
            SelectedConnection = null;
            if (selected != null) selected.SetSelected(true);
            panel.Refresh();
        }

        public void SelectConnection(CellConnection edge)
        {
            Select(null);
            if (edge != null && !ContainsConnection(edge)) return;
            SelectedConnection = edge;
            SetMessage(IsCoreExit(edge) ? "选择核心出口的信号。" : "自动跟随上游信号。");
        }

        private bool ContainsConnection(CellConnection edge)
        { foreach (var item in Graph.Edges) if (item == edge) return true; return false; }

        public bool IsCoreExit(CellConnection edge) => edge != null && edge.Contains(PrimaryCore);

        public void SetCoreChannel(int channel)
        {
            if (!IsEditing || channel < 0 || channel > 3 || !IsCoreExit(SelectedConnection)) return;
            if (Graph.ConfigureCoreChannel(SelectedConnection, PrimaryCore, (IntentChannel)channel))
                SetMessage("核心出口已设为 " + (IntentChannel)channel + "。");
        }

        public int ConnectionChannels(CellConnection edge)
        {
            if (edge == null || Physics == null) return 0;
            if (IsCoreExit(edge)) return 1 << (int)edge.CoreChannel;
            Physics.Signals.Refresh(Graph, cells, PrimaryCore);
            int mask = 0;
            for (int c = 0; c < 4; c++)
                if (Mathf.Min(Physics.Signals.Strength(PrimaryCore, edge.A, c), Physics.Signals.Strength(PrimaryCore, edge.B, c)) > 0)
                    mask |= 1 << c;
            return mask;
        }

        public void ToggleMode()
        {
            EndDrag(false);
            Mode = IsEditing ? LabMode.Swim : LabMode.Edit;
            if (IsEditing) Physics.StopSimulation(); else Physics.StartSimulation();
            panel.Refresh();
        }

        public void BeginDrag(CellView cell, Vector3 pointer)
        {
            if (!IsEditing || cell == null || !cells.Contains(cell)) return;
            Select(cell);
            dragged = cell;
            dragOffset = cell.transform.position - pointer;
            dragGroup = Graph.Component(cell);
            dragMoved = false;
        }

        public void MoveDrag(Vector3 pointer)
        {
            if (!IsEditing || dragged == null) return;
            Vector3 position = pointer + dragOffset;
            float radius = dragged.EffectiveRadius;
            Vector3 min = worldCamera.ViewportToWorldPoint(new Vector3(0, 0.08f, -worldCamera.transform.position.z));
            Vector3 max = worldCamera.ViewportToWorldPoint(new Vector3(1, ArenaTop, -worldCamera.transform.position.z));
            position.x = Mathf.Clamp(position.x, min.x + radius, max.x - radius);
            position.y = Mathf.Clamp(position.y, min.y + radius, max.y - radius);
            position.z = 0;
            Vector3 delta = position - dragged.transform.position;
            if (!GroupFits(dragGroup, delta)) { SetMessage("移动被阻止：细胞不能重叠或超出操作区域。"); return; }
            foreach (var cell in dragGroup) cell.transform.position += delta;
            if (delta.sqrMagnitude > 0.000001f) dragMoved = true;
            SetMessage("松开尝试圆周连接；已连接细胞整体移动。");
        }

        public void EndDrag(bool snap = true)
        {
            if (dragged == null) return;
            if (snap && dragMoved && IsEditing) SnapGroup();
            dragged = null; dragGroup = null; dragMoved = false;
        }

        public bool IsCoreConnected(CellView cell) => cell != null && Graph.Component(PrimaryCore).Contains(cell);

        public void SetMessage(string text) { Message = text; panel.Refresh(); }

        public bool Connect(CellView a, CellView b)
        {
            if (!IsEditing) return false;
            if (!cells.Contains(a) || !cells.Contains(b)) { SetMessage("连接对象不在当前实验室。"); return false; }
            bool success = Graph.TryConnect(a, b, out string reason);
            SetMessage(success ? "连接成功：圆周接触位置不限制角度。" : reason);
            return success;
        }

        public void DisconnectSelected()
        {
            if (!IsEditing || (selected == null && SelectedConnection == null)) return;
            EndDrag(false);
            if (SelectedConnection != null)
            {
                Graph.Remove(SelectedConnection); SelectedConnection = null;
                SetMessage("连接已拆开，机械连接与信号线路同时移除。"); return;
            }
            Graph.Disconnect(selected);
            SetMessage("已拆开所选细胞，脱离主核心的部分失去控制资格。");
        }

        public void DeleteSelected()
        {
            if (!IsEditing || selected == null) return;
            EndDrag(false);
            var cell = selected;
            Food.ReleaseFrom(cell);
            Graph.Disconnect(cell); cells.Remove(cell);
            if (cell == PrimaryCore)
            {
                PrimaryCore = null;
                foreach (var other in cells) if (other.Definition.kind == CellKind.Core) { PrimaryCore = other; break; }
            }
            cell.gameObject.SetActive(false); Destroy(cell.gameObject);
            selected = null;
            SetMessage("细胞已删除；连接和核心连通状态已更新。");
        }

        private bool Fits(float radius, Vector3 position, HashSet<CellView> ignored)
        {
            foreach (var other in cells)
            {
                if (ignored != null && ignored.Contains(other)) continue;
                if (Vector2.Distance(position, other.transform.position) < radius + other.EffectiveRadius - 0.01f) return false;
            }
            return true;
        }

        private bool GroupFits(HashSet<CellView> group, Vector3 delta)
        {
            Vector3 min = worldCamera.ViewportToWorldPoint(new Vector3(0, 0.08f, -worldCamera.transform.position.z));
            Vector3 max = worldCamera.ViewportToWorldPoint(new Vector3(1, ArenaTop, -worldCamera.transform.position.z));
            foreach (var cell in group)
            {
                Vector3 position = cell.transform.position + delta;
                float r = cell.EffectiveRadius;
                if (position.x < min.x + r || position.x > max.x - r || position.y < min.y + r || position.y > max.y - r) return false;
                if (!Fits(r, position, group)) return false;
            }
            return true;
        }

        private void SnapGroup()
        {
            CellView from = null, to = null;
            float closest = 0.45f;
            foreach (var member in dragGroup)
                foreach (var other in cells)
                {
                    if (dragGroup.Contains(other)) continue;
                    float gap = Mathf.Abs(Vector2.Distance(member.transform.position, other.transform.position) - member.EffectiveRadius - other.EffectiveRadius);
                    if (gap < closest) { closest = gap; from = member; to = other; }
                }
            if (from == null) { SetMessage("位置已保存；靠近圆周接触处可连接。"); return; }
            if (Graph.Degree(from) >= from.Definition.maxConnections || Graph.Degree(to) >= to.Definition.maxConnections)
            { SetMessage("连接数量已达上限，未吸附。"); return; }
            Vector3 direction = (from.transform.position - to.transform.position).normalized;
            Vector3 target = to.transform.position + direction * (from.EffectiveRadius + to.EffectiveRadius);
            Vector3 delta = target - from.transform.position;
            if (!GroupFits(dragGroup, delta)) { SetMessage("吸附被阻止：会造成重叠或超出操作区域。"); return; }
            foreach (var cell in dragGroup) cell.transform.position += delta;
            Connect(from, to);
        }

        public void RotateSelected(float degrees)
        {
            if (!IsEditing || selected == null) return;
            selected.transform.Rotate(0, 0, degrees);
            panel.Refresh();
        }

        public CellDefinition DefinitionFor(CellKind kind) => kind == CellKind.Core ? core : kind == CellKind.Cilia ? cilia : kind == CellKind.Absorber ? absorber : kind == CellKind.Membrane ? membrane : contractor;
        public void SetExperimentCapacity(int count) => capacity = Mathf.Clamp(count, 2, 30);
        public bool ExperimentPositionFits(Vector2 position, float radius)
        {
            Vector2 min = worldCamera.ViewportToWorldPoint(new Vector3(0, 0.08f, -worldCamera.transform.position.z));
            Vector2 max = worldCamera.ViewportToWorldPoint(new Vector3(1, ArenaTop, -worldCamera.transform.position.z));
            return position.x >= min.x + radius && position.x <= max.x - radius && position.y >= min.y + radius && position.y <= max.y - radius;
        }
        public CellView CreateExperimentCell(CellKind kind, Vector2 position, float rotation)
        {
            var prefab = kind == CellKind.Core ? corePrefab : kind == CellKind.Cilia ? ciliaPrefab : kind == CellKind.Absorber ? absorberPrefab : kind == CellKind.Membrane ? membranePrefab : contractorPrefab;
            var cell = Instantiate(prefab, position, Quaternion.Euler(0, 0, rotation), cellsRoot);
            cell.Initialize(DefinitionFor(kind)); cell.name = cell.Definition.displayName + " " + (cells.Count + 1);
            cells.Add(cell); if (PrimaryCore == null && kind == CellKind.Core) PrimaryCore = cell;
            return cell;
        }
        private void OnApplicationFocus(bool focused) { if (!focused) EndDrag(false); }
        private void OnDisable() { EndDrag(false); Physics?.StopSimulation(); }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (ExperimentMenu != null)
            {
                if (!ExperimentMenu.IsOpen && keyboard != null && keyboard.bKey.wasPressedThisFrame) ExperimentMenu.Open();
                if (ExperimentMenu.IsOpen) { panel.Refresh(); return; }
            }
            if (keyboard != null)
            {
                if (keyboard.tabKey.wasPressedThisFrame) ToggleMode();
                if (keyboard.digit1Key.wasPressedThisFrame) SpawnCore();
                if (keyboard.digit2Key.wasPressedThisFrame) SpawnCilia();
                if (keyboard.digit3Key.wasPressedThisFrame) LoadNextExample();
                if (keyboard.digit4Key.wasPressedThisFrame) SpawnAbsorber();
                if (keyboard.digit5Key.wasPressedThisFrame) SeedFood();
                if (keyboard.digit6Key.wasPressedThisFrame) ToggleFlowDisplay();
                if (keyboard.digit7Key.wasPressedThisFrame) Food.SeedFilterPatch();
                if (keyboard.digit8Key.wasPressedThisFrame) SpawnMembrane();
                if (keyboard.digit9Key.wasPressedThisFrame) LoadNextMembraneTrial();
                if (keyboard.digit0Key.wasPressedThisFrame) SpawnContractor();
                if (keyboard.cKey.wasPressedThisFrame) LoadContractionExample();
                if (keyboard.backspaceKey.wasPressedThisFrame) ResetLab();
                if (keyboard.xKey.wasPressedThisFrame) DisconnectSelected();
                if (keyboard.deleteKey.wasPressedThisFrame) DeleteSelected();
                if (keyboard.escapeKey.wasPressedThisFrame && !Application.isEditor) Application.Quit();
                float direction = (keyboard.eKey.isPressed ? 1 : 0) - (keyboard.qKey.isPressed ? 1 : 0);
                if (direction != 0) RotateSelected(-direction * 90f * Time.deltaTime);
            }
            Mouse mouse = Mouse.current;
            panel.Refresh();
            if (mouse == null) return;
            if (mouse.leftButton.wasReleasedThisFrame || !mouse.leftButton.isPressed) EndDrag();
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector3 pointer = worldCamera.ScreenToWorldPoint(new Vector3(mouse.position.x.ReadValue(), mouse.position.y.ReadValue(), -worldCamera.transform.position.z));
            if (IsDragging && mouse.leftButton.isPressed) MoveDrag(pointer);
            if (!overUI && mouse.scroll.y.ReadValue() != 0) RotateSelected(-Mathf.Sign(mouse.scroll.y.ReadValue()) * 15f);
            if (overUI || !mouse.leftButton.wasPressedThisFrame) return;
            CellView hit = null;
            for (int i = cells.Count - 1; i >= 0; i--)
                if (Vector2.Distance(pointer, cells[i].transform.position) <= cells[i].EffectiveRadius)
                { hit = cells[i]; break; }
            if (keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed))
            { Connect(selected, hit); return; }
            {
                CellConnection nearest = null; float distance = 0.18f;
                foreach (var edge in Graph.Edges)
                {
                    Vector2 a = edge.A.transform.position, b = edge.B.transform.position;
                    Vector2 segment = b - a;
                    float t = segment.sqrMagnitude > 0 ? Mathf.Clamp(Vector2.Dot((Vector2)pointer - a, segment) / segment.sqrMagnitude, 0.42f, 0.58f) : 0;
                    float d = Vector2.Distance(pointer, a + segment * t);
                    if (d < distance) { nearest = edge; distance = d; }
                }
                if (nearest != null) { SelectConnection(nearest); return; }
            }
            Select(hit);
            BeginDrag(hit, pointer);
        }
    }
}
