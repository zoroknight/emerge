#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Emerge.Cells;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Emerge.Core
{
    [Serializable] public sealed class ExperimentTrial
    {
        public string id; public float seconds, rotation, displacement, captured, digested, minimumSupply = 1, maxLedgerError;
        public float activitySpent, maintenanceSpent, finalEnergy, finalNutrients;
    }
    [Serializable] public sealed class ExperimentReport
    {
        public bool passed, persistence, contractionControl, environmentIsolation, invalidRejected, menu;
        public string failure;
        public List<ExperimentTrial> trials = new List<ExperimentTrial>();
    }
    public static class ExperimentSmokeCheck
    {
        public static string Failure { get; private set; }
        private static void Check(bool value, string reason) { if (!value && Failure == null) Failure = reason; }
        private static void Input(Keyboard k, params Key[] keys) { InputSystem.QueueStateEvent(k,new KeyboardState(keys)); InputSystem.Update(); }
        private static void Step(CellLabController lab) { lab.Physics.StepActuators(); Physics2D.Simulate(Time.fixedDeltaTime); }
        private static Vector2 Center(CellLabController lab)
        { Vector2 sum = Vector2.zero; float mass = 0; foreach(var c in lab.Cells) { sum += c.Body.position * c.Body.mass; mass += c.Body.mass; } return sum/mass; }
        private static string Folder => Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath,"..","Logs")) : Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath),"..","..","Logs"));
        public static IEnumerator Run(CellLabController lab)
        {
            Failure = null; var report = new ExperimentReport(); Directory.CreateDirectory(Folder);
            var previousSimulation = Physics2D.simulationMode; var previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>(); lab.Physics.enabled = false; lab.Physics.enabled = true;
            Physics2D.simulationMode = SimulationMode2D.Script;
            string saved = null;
            try
            {
                Check(lab.Experiments.Entries.Count >= 7,"Missing experiment templates");
                var candidates = new List<ExperimentData>(); foreach(var e in lab.Experiments.Entries) if(!e.IsCustom) candidates.Add(e.data);
                foreach (string id in new[] { "swing-tail", "telescoping-arms", "contractile-mouth" })
                {
                    var control = JsonUtility.FromJson<ExperimentData>(JsonUtility.ToJson(candidates.Find(d=>d.id==id)));
                    control.id += "-frozen"; control.disableContraction=true; candidates.Add(control);
                }
                foreach(var data in candidates)
                {
                    Input(keyboard); Check(lab.Experiments.Load(data),"Cannot load " + data.id + ": " + lab.Message);
                    if(Failure != null) break;
                    var trial = new ExperimentTrial { id = data.id, seconds = 20 };
                    var center = Center(lab); float angle = lab.PrimaryCore.Body.rotation;
                    float initialFood = 0; foreach(var p in lab.Food.Particles) initialFood += p.Remaining;
                    lab.ToggleMode();
                    for(int i = 0; i < 1000; i++)
                    {
                        bool pulse = i % 100 < 50;
                        if(data.id.StartsWith("swing-tail")) Input(keyboard,pulse ? new[]{Key.W,Key.A} : new[]{Key.W});
                        else if(data.id.Contains("mouth")) Input(keyboard,pulse ? new[]{Key.A} : new Key[0]);
                        else Input(keyboard,Key.W);
                        Step(lab);
                        float next = lab.PrimaryCore.Body.rotation; trial.rotation += Mathf.DeltaAngle(angle,next); angle = next;
                        trial.minimumSupply = Mathf.Min(trial.minimumSupply,lab.Metabolism.SupplyRatio);
                        float remaining = 0; foreach(var p in lab.Food.Particles) remaining += p.Remaining;
                        var s = lab.Metabolism;
                        float error = Mathf.Max(Mathf.Abs(initialFood-remaining-s.Ingested),Mathf.Abs(data.nutrients+s.Ingested-s.Nutrients-s.Metabolized),Mathf.Abs(data.energy+s.Produced-s.Energy-s.ActivitySpent-s.MaintenanceSpent));
                        trial.maxLedgerError = Mathf.Max(trial.maxLedgerError,error);
                        foreach(var c in lab.Cells) Check(!float.IsNaN(c.Body.position.x) && !float.IsNaN(c.Body.position.y),"Nonfinite experiment body");
                        if(data.id=="spiral" && i==74)
                        { yield return new WaitForEndOfFrame(); CellLabSmokeCheck.CapturePreview(Path.Combine(Folder,"experiment-spiral-feeding.png")); }
                    }
                    trial.displacement = Vector2.Distance(center,Center(lab)); trial.captured = lab.Food.CapturedTotal; trial.digested = lab.Metabolism.Ingested;
                    trial.activitySpent=lab.Metabolism.ActivitySpent; trial.maintenanceSpent=lab.Metabolism.MaintenanceSpent;
                    trial.finalEnergy=lab.Metabolism.Energy; trial.finalNutrients=lab.Metabolism.Nutrients;
                    Check(trial.maxLedgerError < .005f,"Experiment resource ledger failed: " + data.id);
                    report.trials.Add(trial); lab.ToggleMode();
                    if(data.id == "spiral") CellLabSmokeCheck.CapturePreview(Path.Combine(Folder,"experiment-spiral.png"));
                    yield return null;
                }
                Input(keyboard); var arms = candidates.Find(d=>d.id=="telescoping-arms"); lab.Experiments.Load(arms);
                lab.Experiments.DisableContraction = true; Input(keyboard,Key.W); lab.ToggleMode();
                for(int i=0;i<50;i++) Step(lab);
                report.contractionControl = lab.Cells[1].Contraction == 0 && lab.Cells[3].Activation > .7f;
                Check(report.contractionControl,"Contraction control changed cilia signals"); lab.ToggleMode();
                var outside = new GameObject("External flow isolation test").AddComponent<Emerge.World.SceneFlowRegion>(); outside.radius = 100; outside.speed=5;
                report.environmentIsolation = lab.Food.AmbientVelocityAt(Vector2.zero) == Vector2.zero;
                outside.gameObject.SetActive(false); UnityEngine.Object.Destroy(outside.gameObject);
                Check(report.environmentIsolation,"External scene flow leaked into experiment");
                int count = lab.Cells.Count; var invalid = JsonUtility.FromJson<ExperimentData>(JsonUtility.ToJson(arms)); invalid.links[0].a=999;
                report.invalidRejected = !lab.Experiments.Load(invalid) && lab.Cells.Count==count;
                Check(report.invalidRejected,"Invalid data destroyed the active experiment");
                lab.Experiments.Load(candidates.Find(d=>d.id=="contractile-mouth"));
                Input(keyboard,Key.A); lab.ToggleMode(); for(int i=0;i<50;i++) Step(lab); lab.ToggleMode();
                var caught = lab.Food.Spawn(lab.Cells[1].transform.position); caught.Capture(lab.Cells[1],0);
                count=lab.Cells.Count; float contraction=lab.Cells[2].Contraction; int foodCount=lab.Food.Particles.Count;
                // Saving a current scene persists ordinary JSON; this isolated folder never modifies user experiments.
                var library = new ExperimentLibrary(lab,new CellExperiment[0],Path.Combine(Folder,"experiment-save-check"));
                Check(library.Save("测试副本","保存与重新载入","中文记录"),"Cannot save experiment: " + lab.Message);
                saved = library.LastSavedPath;
                var reload = new ExperimentLibrary(lab,new CellExperiment[0],library.DirectoryPath);
                var savedEntry = reload.Entries.Find(e=>e.path==saved);
                report.persistence = savedEntry != null && savedEntry.data.notes=="中文记录" && lab.Experiments.Load(savedEntry.data) && lab.Cells.Count==count
                    && Mathf.Abs(lab.Cells[2].Contraction-contraction)<.0001f && lab.Food.Particles.Count==foodCount && lab.Food.StoredCount(lab.Cells[1])==1
                    && lab.Food.AmbientVelocityAt(Vector2.zero).y<-.4f && lab.Graph.Edges[0].CoreChannel==IntentChannel.A;
                Check(report.persistence,"Saved layout roundtrip failed");
                Check(reload.Save("更新的副本","修改假说","第二次记录",savedEntry),"Cannot update own experiment");
                var reread = new ExperimentLibrary(lab,new CellExperiment[0],library.DirectoryPath);
                Check(reread.Entries.Find(e=>e.path==saved).data.notes=="第二次记录","Saved edit did not persist");
                lab.Experiments.Load(candidates.Find(d=>d.id=="spiral")); lab.ExperimentMenu.Open();
                report.menu = lab.ExperimentMenu.IsOpen && lab.IsEditing; Check(report.menu,"Experiment menu did not pause simulation");
                var field = lab.ExperimentMenu.TitleField;
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(field.gameObject); field.ActivateInputField();
                yield return null;
                field.text=""; field.ProcessEvent(new Event { type=EventType.KeyDown, character='中' });
                field.ProcessEvent(new Event { type=EventType.KeyDown, character='W' });
                Check(field.text=="中W" && lab.IsEditing,"Chinese text entry failed or activated lab shortcuts");
                field.DeactivateInputField(); field.text="我的旋转吸入实验";
                yield return new WaitForEndOfFrame();
                CellLabSmokeCheck.CapturePreview(Path.Combine(Folder,"experiment-menu.png")); lab.ExperimentMenu.Close();
            }
            finally
            {
                if(saved != null && File.Exists(saved)) File.Delete(saved);
                Input(keyboard); InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior = previousBackground;
                if(!lab.IsEditing) lab.ToggleMode(); lab.ExperimentMenu.Close(); lab.ClearLab(); lab.ResetLab();
                Physics2D.simulationMode = previousSimulation;
                report.failure = Failure; report.passed = Failure == null;
                File.WriteAllText(Path.Combine(Folder,"experiment-results.json"),JsonUtility.ToJson(report,true));
                Debug.Log("EXPERIMENT_LIBRARY_" + (report.passed ? "PASS" : "FAIL") + ": " + Failure);
            }
            yield return null;
        }
    }
}
#endif
