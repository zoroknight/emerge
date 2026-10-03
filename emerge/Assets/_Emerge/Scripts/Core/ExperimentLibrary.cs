using System;
using System.Collections.Generic;
using System.IO;
using Emerge.Cells;
using Emerge.World;
using UnityEngine;

namespace Emerge.Core
{
    public sealed class ExperimentEntry { public ExperimentData data; public string path; public bool IsCustom => !string.IsNullOrEmpty(path); }
    public sealed class ExperimentLibrary
    {
        private readonly CellLabController lab;
        private readonly CellExperiment[] defaults;
        public List<ExperimentEntry> Entries { get; } = new List<ExperimentEntry>();
        public string DirectoryPath { get; }
        public ExperimentData Active { get; private set; }
        private GameObject environment;
        public bool IsActive => Active != null;
        public bool DisableContraction { get; set; }
        public string ReadErrors { get; private set; }
        public string LastSavedPath { get; private set; }
        private int normalCapacity;
        public ExperimentLibrary(CellLabController lab, CellExperiment[] defaults, string directory = null)
        {
            this.lab = lab; this.defaults = defaults ?? new CellExperiment[0];
            DirectoryPath = directory ?? Path.Combine(Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "UserData")) : Application.persistentDataPath, "Experiments");
            Refresh();
        }
        public void Refresh()
        {
            Entries.Clear(); ReadErrors = "";
            foreach (var asset in defaults) if (asset != null) Entries.Add(new ExperimentEntry { data = asset.data });
            if (!Directory.Exists(DirectoryPath)) return;
            foreach (var path in Directory.GetFiles(DirectoryPath, "*.json"))
                try
                {
                    var data = JsonUtility.FromJson<ExperimentData>(File.ReadAllText(path));
                    if (data == null || data.version != 1 || data.cells == null) throw new InvalidDataException();
                    Entries.Add(new ExperimentEntry { data = data, path = path });
                }
                catch (Exception) { ReadErrors += "无法读取：" + Path.GetFileName(path) + "\n"; }
        }
        public void End()
        {
            if (environment != null) { environment.SetActive(false); UnityEngine.Object.Destroy(environment); environment = null; }
            if (Active != null) lab.SetExperimentCapacity(normalCapacity);
            Active = null; DisableContraction = false;
        }
        private static bool Finite(float f) => !float.IsNaN(f) && !float.IsInfinity(f);
        private static bool Finite(Vector2 v) => Finite(v.x) && Finite(v.y);
        public string Validate(ExperimentData d)
        {
            if (d == null || d.version != 1 || d.cells == null || d.links == null || d.food == null || d.flows == null) return "实验文件格式不正确。";
            if (d.cells.Length == 0 || d.cells.Length > 30 || d.food.Length > NutrientWorld.Capacity) return "需要 1～30 个细胞，食物最多 40 颗。";
            if (!Finite(d.nutrients) || !Finite(d.energy) || d.nutrients < 0 || d.nutrients > lab.Metabolism.Settings.nutrientCapacity || d.energy < 0 || d.energy > lab.Metabolism.Settings.energyCapacity || !Finite(d.ambient)) return "初始资源或水流数值不合法。";
            int cores = 0; var degree = new int[d.cells.Length]; var stomach = new float[d.cells.Length]; var slots = new int[d.cells.Length];
            for (int i = 0; i < d.cells.Length; i++)
            {
                var c = d.cells[i];
                if (c == null || !Enum.IsDefined(typeof(CellKind), c.kind) || !Finite(c.position) || !Finite(c.rotation) || !Finite(c.contraction) || c.contraction < 0 || c.contraction > 1) return "细胞配置不合法。";
                if (c.kind == CellKind.Core) cores++;
                if (c.kind != CellKind.Contractor && c.contraction != 0) return "只有收缩细胞能设置初始收缩。";
                if (!lab.ExperimentPositionFits(c.position, Radius(c))) return "细胞超出实验室操作范围。";
                for (int j = 0; j < i; j++) if (Vector2.Distance(c.position, d.cells[j].position) < Radius(c) + Radius(d.cells[j]) - 0.01f) return "细胞重叠，请修改位置。";
            }
            if (cores != 1) return "组合实验需要且仅需要一个核心。";
            var pairs = new HashSet<string>();
            foreach (var e in d.links)
            {
                if (e == null || e.a < 0 || e.b < 0 || e.a >= degree.Length || e.b >= degree.Length || e.a == e.b || !Enum.IsDefined(typeof(IntentChannel), e.channel)) return "连接索引或信号不合法。";
                if (!pairs.Add(Mathf.Min(e.a, e.b) + ":" + Mathf.Max(e.a, e.b))) return "存在重复连接。";
                if (++degree[e.a] > lab.DefinitionFor(d.cells[e.a].kind).maxConnections || ++degree[e.b] > lab.DefinitionFor(d.cells[e.b].kind).maxConnections) return "细胞连接数超出上限。";
                float gap = Vector2.Distance(d.cells[e.a].position, d.cells[e.b].position) - Radius(d.cells[e.a]) - Radius(d.cells[e.b]);
                if (gap < -0.01f || gap > 0.06f) return "连接两端必须在圆周接触位置。";
            }
            foreach (var p in d.food)
            {
                if (p == null || !Finite(p.position) || !Finite(p.amount) || p.amount <= 0 || p.amount > 10000 || p.captor < -1 || p.captor >= degree.Length) return "食物配置不合法。";
                if (p.captor < 0) { if (!lab.ExperimentPositionFits(p.position, NutrientParticle.Radius)) return "食物超出实验室操作范围。"; continue; }
                var def = lab.DefinitionFor(d.cells[p.captor].kind);
                if (def.kind != CellKind.Absorber || ++slots[p.captor] > def.foodSlots || (stomach[p.captor] += p.amount) > def.foodCapacity + 0.00001f) return "胃内食物超出吸收细胞容量。";
            }
            foreach (var f in d.flows) if (f == null || !Finite(f.position) || !Finite(f.rotation) || !Finite(f.radius) || !Finite(f.speed) || f.radius < 0.1f || f.radius > 100 || f.speed < 0 || f.speed > 20) return "局部流场配置不合法。";
            return null;
        }
        private float Radius(ExperimentCell c)
        { var d = lab.DefinitionFor(c.kind); return d.radius * (1 - (1 - d.contractedSize) * c.contraction); }
        public bool Load(ExperimentData data)
        {
            if (!lab.IsEditing) { lab.SetMessage("先返回编辑模式再载入实验。"); return false; }
            string error = Validate(data); if (error != null) { lab.SetMessage(error); return false; }
            var copy = JsonUtility.FromJson<ExperimentData>(JsonUtility.ToJson(data));
            lab.ClearLab(); normalCapacity = lab.Capacity; lab.SetExperimentCapacity(Mathf.Max(normalCapacity, copy.cells.Length));
            Active = copy; DisableContraction = copy.disableContraction;
            foreach (var c in copy.cells) { var cell = lab.CreateExperimentCell(c.kind, c.position, c.rotation); cell.RestoreContraction(c.contraction); }
            foreach (var e in copy.links)
            {
                if (!lab.Connect(lab.Cells[e.a], lab.Cells[e.b])) throw new InvalidOperationException("已验证连接无法载入。");
                var edge = lab.Graph.Edges[lab.Graph.Edges.Count - 1];
                if (lab.IsCoreExit(edge)) lab.Graph.ConfigureCoreChannel(edge, lab.PrimaryCore, e.channel);
            }
            environment = new GameObject("组合实验环境"); environment.transform.SetParent(lab.transform, false);
            foreach (var f in copy.flows)
            {
                var obj = new GameObject("实验局部流场"); obj.transform.SetParent(environment.transform, false);
                obj.transform.SetPositionAndRotation(f.position, Quaternion.Euler(0, 0, f.rotation));
                var region = obj.AddComponent<SceneFlowRegion>(); region.IsExperimentSource = true;
                region.radius = f.radius; region.speed = f.speed; region.fadeAtEdge = f.fade;
            }
            foreach (var p in copy.food)
            {
                var food = lab.Food.Spawn(p.position, p.amount);
                if (p.captor >= 0) food.Capture(lab.Cells[p.captor], lab.Food.StoredCount(lab.Cells[p.captor]));
            }
            lab.Metabolism.Reset(copy.nutrients, copy.energy); lab.Select(lab.PrimaryCore);
            lab.SetMessage(copy.title + "：" + copy.controls); return true;
        }
        public ExperimentData Snapshot(string title, string idea, string notes)
        {
            var active = lab.Experiments != null ? lab.Experiments.Active : Active;
            var d = new ExperimentData { id = Guid.NewGuid().ToString("N"), title = title, idea = idea, notes = notes,
                controls = active != null ? active.controls : "Tab 游动；按核心出口设定的 WASD 观察。", nutrients = lab.Metabolism.Nutrients, energy = lab.Metabolism.Energy,
                disableContraction = lab.Experiments != null ? lab.Experiments.DisableContraction : DisableContraction, ambient = active != null ? active.ambient : lab.Food.FlowSettings.ambientVelocity };
            var cells = new List<ExperimentCell>(); var links = new List<ExperimentLink>(); var food = new List<ExperimentFood>(); var flows = new List<ExperimentFlow>();
            foreach (var c in lab.Cells) cells.Add(new ExperimentCell { kind = c.Definition.kind, position = c.transform.position, rotation = c.transform.eulerAngles.z, contraction = c.Contraction });
            foreach (var e in lab.Graph.Edges) links.Add(new ExperimentLink { a = Index(e.A), b = Index(e.B), channel = e.CoreChannel });
            foreach (var p in lab.Food.Particles) food.Add(new ExperimentFood { position = p.transform.position, amount = p.Remaining, captor = p.IsCaptured ? Index(p.Captor) : -1 });
            foreach (var f in lab.Food.FlowRegions) if (f != null && f.isActiveAndEnabled && (active == null || f.IsExperimentSource))
                flows.Add(new ExperimentFlow { position = f.transform.position, rotation = f.transform.eulerAngles.z, radius = f.radius, speed = f.speed, fade = f.fadeAtEdge });
            d.cells = cells.ToArray(); d.links = links.ToArray(); d.food = food.ToArray(); d.flows = flows.ToArray(); return d;
        }
        private int Index(CellView cell) { for (int i = 0; i < lab.Cells.Count; i++) if (lab.Cells[i] == cell) return i; return -1; }
        public bool Save(string title, string idea, string notes, ExperimentEntry overwrite = null)
        {
            if (!lab.IsEditing) { lab.SetMessage("先返回编辑模式再保存。"); return false; }
            if (string.IsNullOrWhiteSpace(title)) { lab.SetMessage("请为实验填写名称。"); return false; }
            var data = Snapshot(title.Trim(), idea, notes); string error = Validate(data);
            if (error != null) { lab.SetMessage(error); return false; }
            if (overwrite != null && !overwrite.IsCustom) { lab.SetMessage("内置实验请保存为新实验，再修改自己的副本。"); return false; }
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                string path = overwrite == null ? Path.Combine(DirectoryPath, data.id + ".json") : overwrite.path;
                path = Path.GetFullPath(path);
                if (!string.Equals(Path.GetDirectoryName(path), Path.GetFullPath(DirectoryPath).TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path) != ".json") throw new InvalidDataException("实验保存路径不合法。");
                if (overwrite != null) data.id = overwrite.data.id;
                string temp = path + ".tmp"; File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
                LastSavedPath = path; Refresh(); lab.SetMessage("已保存：" + data.title + "。退出 Play 后仍可载入。"); return true;
            }
            catch (Exception e) { lab.SetMessage("保存失败：" + e.Message); return false; }
        }
    }
}
