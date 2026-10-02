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
    public enum LabExample { Straight, Turn, Reverse, Feeding }

    public sealed class CellLabController : MonoBehaviour
    {
        [SerializeField] private CellDefinition core;
        [SerializeField] private CellDefinition cilia;
        [SerializeField] private CellDefinition absorber;
        [SerializeField] private CellView absorberPrefab;
        [SerializeField] private MetabolismSettings metabolismSettings;
        [SerializeField] private NutrientParticle nutrientPrefab;
        [SerializeField] private CellView corePrefab;
        [SerializeField] private CellView ciliaPrefab;
        [SerializeField] private Transform cellsRoot;
        [SerializeField] private Camera worldCamera;
        [SerializeField] private CellLabPanel panel;
        [SerializeField] private Material connectionMaterial;
        [SerializeField] private InputActionAsset inputControls;
        [SerializeField, Range(2, 30)] private int capacity = 20;

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
        public MetabolismState Metabolism { get; private set; }
        public NutrientWorld Food { get; private set; }
        public const float ArenaTop = 0.72f;

        private void Start()
        {
            Metabolism = new MetabolismState(metabolismSettings);
            Food = gameObject.AddComponent<NutrientWorld>(); Food.Initialize(this, nutrientPrefab);
            panel.Initialize(this);
            var connections = new GameObject("连接显示").AddComponent<CellConnectionsView>();
            connections.transform.SetParent(transform, false);
            connections.Initialize(this, connectionMaterial);
            Physics = gameObject.AddComponent<CellLabPhysics>();
            Physics.Initialize(this, inputControls, connectionMaterial, worldCamera);
            var motion = new GameObject("纤毛方向显示").AddComponent<CellMotionView>();
            motion.transform.SetParent(transform, false);
            motion.Initialize(this, connectionMaterial);
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
        public void SeedFood() => Food.SeedPatch();

        public void LoadNextExample()
        {
            if (!IsEditing) return;
            exampleIndex = (exampleIndex + 1) % 4;
            LoadExample((LabExample)exampleIndex);
        }

        public void LoadExample(LabExample example)
        {
            if (!IsEditing || capacity < 3) return;
            if (example == LabExample.Feeding)
            {
                ClearLab(); SpawnCore(); SpawnCilia(); SpawnAbsorber();
                cells[0].transform.position = new Vector3(0, -1, 0);
                cells[1].transform.position = cells[0].transform.position + Vector3.up * (core.radius + cilia.radius);
                cells[1].transform.rotation = Quaternion.Euler(0, 0, 90);
                cells[2].transform.position = cells[0].transform.position + Vector3.right * (core.radius + absorber.radius);
                Connect(cells[0], cells[1]); Connect(cells[0], cells[2]); Select(cells[2]);
                Metabolism.Reset(0, 1);
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
            var data = kind == CellKind.Core ? core : kind == CellKind.Cilia ? cilia : absorber;
            var prefab = kind == CellKind.Core ? corePrefab : kind == CellKind.Cilia ? ciliaPrefab : absorberPrefab;
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
            ClearLab();
            SpawnCore();
            SpawnCilia();
        }

        public void ClearLab()
        {
            if (!IsEditing) return;
            EndDrag(false);
            Physics?.StopSimulation();
            Graph.Clear();
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
            float radius = dragged.Definition.radius;
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
                if (Vector2.Distance(position, other.transform.position) < radius + other.Definition.radius - 0.01f) return false;
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
                float r = cell.Definition.radius;
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
                    float gap = Mathf.Abs(Vector2.Distance(member.transform.position, other.transform.position) - member.Definition.radius - other.Definition.radius);
                    if (gap < closest) { closest = gap; from = member; to = other; }
                }
            if (from == null) { SetMessage("位置已保存；靠近圆周接触处可连接。"); return; }
            if (Graph.Degree(from) >= from.Definition.maxConnections || Graph.Degree(to) >= to.Definition.maxConnections)
            { SetMessage("连接数量已达上限，未吸附。"); return; }
            Vector3 direction = (from.transform.position - to.transform.position).normalized;
            Vector3 target = to.transform.position + direction * (from.Definition.radius + to.Definition.radius);
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

        private void OnApplicationFocus(bool focused) { if (!focused) EndDrag(false); }
        private void OnDisable() { EndDrag(false); Physics?.StopSimulation(); }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.tabKey.wasPressedThisFrame) ToggleMode();
                if (keyboard.digit1Key.wasPressedThisFrame) SpawnCore();
                if (keyboard.digit2Key.wasPressedThisFrame) SpawnCilia();
                if (keyboard.digit3Key.wasPressedThisFrame) LoadNextExample();
                if (keyboard.digit4Key.wasPressedThisFrame) SpawnAbsorber();
                if (keyboard.digit5Key.wasPressedThisFrame) SeedFood();
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
                if (Vector2.Distance(pointer, cells[i].transform.position) <= cells[i].Definition.radius)
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
