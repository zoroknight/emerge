#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Emerge.Cells;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Emerge.Core
{
    [Serializable]
    public sealed class MetabolismReport
    {
        public string timestampUtc, unity, failure;
        public bool passed, unlimitedEnergy;
        public float coreIngested, absorberIngested, starvationEnergy, starvationSupply;
        public float recoveryIngested, recoverySupply, recoveryForce, recoveryDistance;
        public float ledgerMaxError, rationA, rationB, starvationMaxSupplyDrop;
        public int particleLimit;
    }

    public static class CellMetabolismSmokeCheck
    {
        public static string Failure { get; private set; }
        private static void Check(bool condition, string message)
        { if (!condition && Failure == null) Failure = "T08: " + message; }
        private static string Folder => Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs")) :
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "..", "..", "Logs"));
        private static void Steps(CellLabController lab, int count)
        {
            for (int i = 0; i < count; i++)
            { lab.Physics.StepActuators(); Physics2D.Simulate(Time.fixedDeltaTime); }
        }
        private static void Key(Keyboard keyboard, bool pressed)
        { InputSystem.QueueStateEvent(keyboard, pressed ? new KeyboardState(UnityEngine.InputSystem.Key.W) : new KeyboardState()); InputSystem.Update(); }
        private static CellView Absorber(CellLabController lab)
        { foreach (var cell in lab.Cells) if (cell.Definition.kind == CellKind.Absorber) return cell; return null; }
        private static void Ledger(MetabolismState state, float n, float e, MetabolismReport report)
        {
            float error = Mathf.Max(Mathf.Abs(n + state.Ingested - state.Nutrients - state.Metabolized),
                Mathf.Abs(e + state.Produced - state.Energy - state.MaintenanceSpent - state.ActivitySpent));
            report.ledgerMaxError = Mathf.Max(report.ledgerMaxError, error);
            Check(error < 0.001f && state.Nutrients >= 0 && state.Energy >= 0 &&
                state.Nutrients <= state.Settings.nutrientCapacity && state.Energy <= state.Settings.energyCapacity,
                "Resource ledger or capacity invariant failed");
        }
        public static IEnumerator Run(CellLabController lab)
        {
            Failure = null;
            var report = new MetabolismReport { timestampUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion };
            var previousMode = Physics2D.simulationMode;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            lab.Physics.enabled = false; lab.Physics.enabled = true;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Directory.CreateDirectory(Folder);
            try
            {
                Check(!lab.Metabolism.DebugUnlimited, "Normal gameplay bypassed energy");
                var state = new MetabolismState(lab.Metabolism.Settings);
                state.Reset(2, 0); state.Step(1, 0, 1);
                Check(Mathf.Abs(state.Nutrients - 1.3f) < 0.001f && Mathf.Abs(state.Energy - 2.77f) < 0.001f, "Nutrients did not convert into energy with upkeep");
                Ledger(state, 2, 0, report);

                lab.LoadExample(LabExample.Feeding); lab.Metabolism.Reset(0, 0); Key(keyboard, false); lab.ToggleMode();
                var core = lab.PrimaryCore;
                var particle = lab.Food.Spawn(core.Body.worldCenterOfMass + Vector2.left * (core.Definition.radius + 0.03f), 2);
                Steps(lab, 50); report.coreIngested = lab.Metabolism.Ingested;
                Check(Mathf.Abs(report.coreIngested - 0.08f) < 0.001f, "Core fallback ingestion failed");
                Check(Mathf.Abs(particle.Remaining + report.coreIngested - 2) < 0.001f, "Food was duplicated or lost");
                Ledger(lab.Metabolism, 0, 0, report);

                lab.Food.Clear(); lab.Metabolism.Reset(0, 0);
                var absorber = Absorber(lab);
                particle = lab.Food.Spawn(absorber.Body.worldCenterOfMass + Vector2.right * (absorber.Definition.radius + 0.03f), 2);
                Steps(lab, 50); report.absorberIngested = lab.Metabolism.Ingested;
                Check(Mathf.Abs(report.absorberIngested - 1.5f) < 0.001f && report.absorberIngested > report.coreIngested * 10, "Absorber was not an efficient contact collector");
                Check(Mathf.Abs(particle.Remaining + report.absorberIngested - 2) < 0.001f, "Absorber double-counted food");
                Ledger(lab.Metabolism, 0, 0, report);

                lab.ToggleMode(); lab.Graph.Disconnect(absorber); lab.Food.Clear(); lab.Metabolism.Reset(0, 0);
                particle = lab.Food.Spawn(absorber.transform.position, 1); lab.ToggleMode(); Steps(lab, 50);
                Check(lab.Metabolism.Ingested == 0 && particle.Remaining == 1, "Detached absorber fed the main body");
                lab.ToggleMode(); lab.LoadExample(LabExample.Feeding); lab.Food.Spawn(new Vector2(10, 0), 1);
                lab.Metabolism.Reset(0, 0); lab.ToggleMode(); Steps(lab, 50);
                Check(lab.Metabolism.Ingested == 0, "Distant food was captured without contact");

                lab.ToggleMode(); lab.LoadExample(LabExample.Feeding); lab.Food.SeedPatch(); Key(keyboard, true);
                Steps(lab, 200);
                Check(lab.Metabolism.Energy == 1 && lab.Metabolism.Nutrients == 0 && lab.Metabolism.Ingested == 0 && lab.Food.Particles.Count == 16,
                    "Editing consumed food or energy");
                foreach (var cell in lab.Cells) Check(!cell.Body.simulated && cell.LastForce == Vector2.zero, "Editing applied real forces");

                lab.Food.Clear(); lab.ToggleMode();
                bool partialSupply = false; float previousSupply = 1;
                for (int i = 0; i < 200; i++)
                {
                    Steps(lab, 1); float supply = lab.Metabolism.SupplyRatio;
                    partialSupply |= supply > 0 && supply < 1;
                    report.starvationMaxSupplyDrop = Mathf.Max(report.starvationMaxSupplyDrop, previousSupply - supply);
                    previousSupply = supply;
                }
                Check(partialSupply && report.starvationMaxSupplyDrop < 0.1f, "Energy shortage did not smoothly reduce supply");
                report.starvationEnergy = lab.Metabolism.Energy; report.starvationSupply = lab.Metabolism.SupplyRatio;
                Check(report.starvationEnergy == 0 && report.starvationSupply == 0 && lab.Cells[1].LastForce == Vector2.zero, "Starved actuator did not stop");
                Ledger(lab.Metabolism, 0, 1, report);
                yield return null; yield return new WaitForEndOfFrame();
                CellLabSmokeCheck.CapturePreview(Path.Combine(Folder, "t08-starved.png"));
                absorber = Absorber(lab); Vector2 initial = lab.PrimaryCore.Body.position;
                lab.Food.Spawn(absorber.Body.worldCenterOfMass, 6); Steps(lab, 50);
                report.recoveryIngested = lab.Metabolism.Ingested; report.recoverySupply = lab.Metabolism.SupplyRatio;
                report.recoveryForce = lab.Cells[1].LastForce.magnitude; report.recoveryDistance = Vector2.Distance(initial, lab.PrimaryCore.Body.position);
                Check(report.recoveryIngested > 0 && report.recoverySupply > 0.95f && report.recoveryForce > 3 && report.recoveryDistance > 0.03f,
                    "Contact feeding did not restore actual propulsion");
                Ledger(lab.Metabolism, 0, 1, report);
                yield return null; yield return new WaitForEndOfFrame();
                CellLabSmokeCheck.CapturePreview(Path.Combine(Folder, "t08-recovered.png"));

                Key(keyboard, false); lab.ToggleMode(); lab.LoadExample(LabExample.Straight);
                lab.Metabolism.Reset(0, 0.01f); lab.ToggleMode(); Key(keyboard, true); Steps(lab, 1);
                report.rationA = lab.Cells[1].Activation; report.rationB = lab.Cells[2].Activation;
                Check(report.rationA > 0 && report.rationA < 0.9f && Mathf.Abs(report.rationA - report.rationB) < 0.00001f, "Scarce energy was not uniformly allocated");
                Ledger(lab.Metabolism, 0, 0.01f, report);

                Key(keyboard, false); lab.ToggleMode(); lab.LoadExample(LabExample.Feeding);
                lab.Metabolism.Reset(lab.Metabolism.Settings.nutrientCapacity, lab.Metabolism.Settings.energyCapacity);
                particle = lab.Food.Spawn(Absorber(lab).transform.position, 2); lab.ToggleMode(); Steps(lab, 1);
                Check(particle.Remaining == 2 && lab.Metabolism.Ingested == 0, "Full storage consumed food");
                Ledger(lab.Metabolism, lab.Metabolism.Settings.nutrientCapacity, lab.Metabolism.Settings.energyCapacity, report);
                lab.ToggleMode(); lab.Food.Clear();
                for (int i = 0; i < 50; i++) lab.Food.Spawn(new Vector2(10, 0));
                report.particleLimit = lab.Food.Particles.Count; Check(report.particleLimit == 40, "Food limit failed");
                lab.Food.Clear(); lab.Metabolism.Reset(1, 1);
                lab.Select(lab.PrimaryCore); lab.DeleteSelected();
                particle = lab.Food.Spawn(Absorber(lab).transform.position, 1); lab.ToggleMode(); Steps(lab, 50);
                Check(lab.PrimaryCore == null && lab.Metabolism.Nutrients == 1 && lab.Metabolism.Energy == 1 && particle.Remaining == 1,
                    "Body without a core retained ingestion or metabolism");
                lab.ToggleMode();
                lab.ResetLab(); yield return null;
                Check(lab.Food.Particles.Count == 0 && UnityEngine.Object.FindObjectsByType<Emerge.World.NutrientParticle>().Length == 0,
                    "Reset left active food objects");
                report.passed = Failure == null; report.failure = Failure ?? "";
                File.WriteAllText(Path.Combine(Folder, "t08-results.json"), JsonUtility.ToJson(report, true));
                if (report.passed) Debug.Log("CELL_LAB_T08_METABOLISM_PASS: contact collection, core fallback, detached and distant exclusion, resource conservation, edit pause, starvation and recovery, uniform rationing, storage and particle limits, reset cleanup.");
            }
            finally
            {
                if (!lab.IsEditing) lab.ToggleMode();
                Physics2D.simulationMode = previousMode;
                InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior = previousBackground;
                lab.ResetLab();
            }
        }
    }
}
#endif
