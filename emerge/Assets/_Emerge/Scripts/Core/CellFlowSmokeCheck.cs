#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using Emerge.Cells;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Emerge.Core
{
    [Serializable]
    public sealed class FilterTrial
    {
        public string control;
        public int repeat, steps;
        public float ingested, transportedIngested, produced, activitySpent, maintenanceSpent;
        public float initialEnergy, finalEnergy, finalNutrients, minimumLateSupply;
        public float measuredProduced, measuredActivity, measuredMaintenance, resourceGain, bodyDrift;
    }
    [Serializable]
    public sealed class FlowReport
    {
        public string timestampUtc, unity, failure;
        public bool passed, unlimitedEnergy;
        public float timestep, influenceRadius, ciliaSpeed, ciliaCost, initialFood, ledgerMaxError;
        public float ambientDisplacement, fieldSpeedLimit;
        public List<FilterTrial> trials = new List<FilterTrial>();
    }
    public static class CellFlowSmokeCheck
    {
        public static string Failure { get; private set; }
        private static string Folder => Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs")) :
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "..", "..", "Logs"));
        private static void Check(bool value, string message)
        { if (!value && Failure == null) Failure = "T09: " + message; }
        private static void Input(Keyboard keyboard, bool pressed)
        { InputSystem.QueueStateEvent(keyboard, pressed ? new KeyboardState(Key.W) : new KeyboardState()); InputSystem.Update(); }
        private static void Step(CellLabController lab)
        { lab.Physics.StepActuators(); Physics2D.Simulate(Time.fixedDeltaTime); }
        private static float Remaining(CellLabController lab)
        { float result = 0; foreach (var particle in lab.Food.Particles) result += particle.Remaining; return result; }
        public static IEnumerator Run(CellLabController lab)
        {
            Failure = null;
            var report = new FlowReport { timestampUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                timestep = Time.fixedDeltaTime, influenceRadius = lab.Food.FlowSettings.influenceRadius,
                ciliaSpeed = lab.Food.FlowSettings.ciliaSpeed, ciliaCost = lab.Metabolism.Settings.ciliaCost, initialFood = 16 };
            var previousMode = Physics2D.simulationMode;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            Vector2 previousAmbient = lab.Food.FlowSettings.ambientVelocity;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            lab.Physics.enabled = false; lab.Physics.enabled = true;
            Physics2D.simulationMode = SimulationMode2D.Script;
            Directory.CreateDirectory(Folder);
            var trace = new StringBuilder("step,seconds,particle,x,y,remaining,distance_to_absorber,travel\n");
            try
            {
                Check(!lab.Metabolism.DebugUnlimited, "Resource constraints were bypassed");
                lab.LoadExample(LabExample.Filter); lab.Food.SeedFilterPatch(); Input(keyboard, true);
                Vector2 initialParticle = lab.Food.Particles[0].transform.position;
                float initialEnergy = lab.Metabolism.Energy;
                for (int i = 0; i < 50; i++) Step(lab);
                Check(lab.Metabolism.Energy == initialEnergy && (Vector2)lab.Food.Particles[0].transform.position == initialParticle && lab.Metabolism.Ingested == 0,
                    "Edit preview moved particles or consumed resources");
                Check(lab.Food.FlowVelocityAt(new Vector2(0, 1.4f), true, lab.Physics.PreviewSupply()).y < -0.5f,
                    "Preview did not show the fixed cilia direction");

                for (int trialIndex = 0; trialIndex < 4; trialIndex++)
                {
                    lab.LoadExample(LabExample.Filter); lab.Food.SeedFilterPatch();
                    bool active = trialIndex != 0, reverse = trialIndex == 1;
                    if (reverse)
                        foreach (var cell in lab.Cells)
                            if (cell.Definition.kind == CellKind.Cilia) cell.transform.Rotate(0, 0, 180);
                    Input(keyboard, active); lab.ToggleMode();
                    var observed = new[] { lab.Food.Particles[0], lab.Food.Particles[19], lab.Food.Particles[20], lab.Food.Particles[39] };
                    var state = lab.Metabolism;
                    Vector2 bodyStart = lab.PrimaryCore.Body.position;
                    var trial = new FilterTrial { control = !active ? "no_input" : reverse ? "reversed" : "inward",
                        repeat = trialIndex < 2 ? 1 : trialIndex - 1, steps = 550, initialEnergy = state.Energy, minimumLateSupply = 1 };
                    float producedAtStart = 0, activityAtStart = 0, maintenanceAtStart = 0, wealthAtStart = 0;
                    for (int step = 0; step < trial.steps; step++)
                    {
                        if (trialIndex == 2 && step % 10 == 0)
                            for (int p = 0; p < observed.Length; p++)
                            {
                                var mote = observed[p]; if (mote == null || mote.Remaining <= 0) continue;
                                Vector2 position = mote.transform.position;
                                trace.AppendFormat(CultureInfo.InvariantCulture, "{0},{1:F3},{2},{3:F6},{4:F6},{5:F6},{6:F6},{7:F6}\n",
                                    step, step * Time.fixedDeltaTime, p, position.x, position.y, mote.Remaining,
                                    Vector2.Distance(position, lab.Cells[1].Body.worldCenterOfMass), mote.TravelDistance);
                            }
                        Step(lab);
                        float error = Mathf.Max(Mathf.Abs(state.Ingested - state.Nutrients - state.Metabolized),
                            Mathf.Abs(trial.initialEnergy + state.Produced - state.Energy - state.MaintenanceSpent - state.ActivitySpent),
                            Mathf.Abs(report.initialFood - Remaining(lab) - state.Ingested));
                        report.ledgerMaxError = Mathf.Max(report.ledgerMaxError, error);
                        Check(error < 0.003f, "Particle / resource conservation failed during flow");
                        if (step == 149)
                        {
                            producedAtStart = state.Produced; activityAtStart = state.ActivitySpent; maintenanceAtStart = state.MaintenanceSpent;
                            wealthAtStart = state.Energy + state.Nutrients * state.Settings.energyYield;
                        }
                        if (step >= 150 && active && !reverse) trial.minimumLateSupply = Mathf.Min(trial.minimumLateSupply, state.SupplyRatio);
                        if (trialIndex == 2 && (step == 0 || step == 99))
                        {
                            yield return null; yield return new WaitForEndOfFrame();
                            CellLabSmokeCheck.CapturePreview(Path.Combine(Folder, step == 0 ? "t09-filter-start.png" : "t09-filter-feeding.png"));
                        }
                    }
                    trial.ingested = state.Ingested; trial.transportedIngested = lab.Food.TransportedIngested;
                    trial.produced = state.Produced; trial.activitySpent = state.ActivitySpent; trial.maintenanceSpent = state.MaintenanceSpent;
                    trial.finalEnergy = state.Energy; trial.finalNutrients = state.Nutrients;
                    trial.measuredProduced = state.Produced - producedAtStart; trial.measuredActivity = state.ActivitySpent - activityAtStart;
                    trial.measuredMaintenance = state.MaintenanceSpent - maintenanceAtStart;
                    trial.resourceGain = state.Energy + state.Nutrients * state.Settings.energyYield - wealthAtStart;
                    trial.bodyDrift = Vector2.Distance(bodyStart, lab.PrimaryCore.Body.position);
                    report.trials.Add(trial);
                    if (!active || reverse) Check(trial.ingested < 0.001f, "Control collected distant food");
                    else
                    {
                        Check(trial.ingested > 5 && trial.transportedIngested > 5, "Particles did not reach the absorber after actual transport");
                        Check(trial.measuredProduced > trial.measuredActivity + trial.measuredMaintenance && trial.minimumLateSupply > 0.98f && trial.resourceGain > 0,
                            "Filtering failed to sustain its actual energy costs");
                        Check(trial.bodyDrift < 0.02f, "Filter body movement confounded the transport comparison");
                    }
                    Input(keyboard, false); lab.ToggleMode();
                    Debug.Log("CELL_LAB_T09_TRIAL: " + JsonUtility.ToJson(trial));
                }
                Check(Mathf.Abs(report.trials[2].ingested - report.trials[3].ingested) < 0.001f &&
                    Mathf.Abs(report.trials[2].finalEnergy - report.trials[3].finalEnergy) < 0.001f, "Filtering was not repeatable");

                lab.LoadExample(LabExample.Filter);
                while (lab.Cells.Count > 2) { lab.Select(lab.Cells[lab.Cells.Count - 1]); lab.DeleteSelected(); }
                lab.Food.SeedFilterPatch(); Input(keyboard, false); lab.ToggleMode();
                for (int i = 0; i < 550; i++) Step(lab);
                var passive = lab.Metabolism;
                report.trials.Add(new FilterTrial { control = "absorber_only", repeat = 1, steps = 550, initialEnergy = 5,
                    ingested = passive.Ingested, produced = passive.Produced, activitySpent = passive.ActivitySpent,
                    maintenanceSpent = passive.MaintenanceSpent, finalEnergy = passive.Energy, finalNutrients = passive.Nutrients });
                Check(passive.Ingested == 0 && passive.ActivitySpent == 0 && Remaining(lab) > 15.99f,
                    "Absorber-only control captured distant particles");
                lab.ToggleMode();

                lab.LoadExample(LabExample.Filter); lab.Metabolism.Reset(0, 0); lab.ToggleMode(); Input(keyboard, true); Step(lab);
                Check(lab.Food.FlowVelocityAt(new Vector2(0, 1.4f)) == Vector2.zero, "Starved cilia generated free flow");
                lab.ToggleMode(); lab.LoadExample(LabExample.Filter);
                lab.Graph.Disconnect(lab.Cells[2]); Input(keyboard, true); lab.ToggleMode(); Step(lab);
                Check(lab.Cells[2].Activation == 0 && lab.Food.FlowVelocityAt(new Vector2(0, 2.5f)) == Vector2.zero,
                    "Detached cilia retained a flow source");
                report.fieldSpeedLimit = lab.Food.FlowVelocityAt(lab.Cells[3].Body.worldCenterOfMass).magnitude;
                Check(report.fieldSpeedLimit <= lab.Food.FlowSettings.maximumSpeed && lab.Food.FlowVelocityAt(new Vector2(10, 0)) == Vector2.zero,
                    "Field failed its speed or influence bounds");

                Input(keyboard, false); lab.ToggleMode(); lab.ResetLab();
                lab.Food.FlowSettings.ambientVelocity = new Vector2(10, 0);
                report.fieldSpeedLimit = lab.Food.FlowVelocityAt(new Vector2(-5, -3)).magnitude;
                Check(Mathf.Abs(report.fieldSpeedLimit - lab.Food.FlowSettings.maximumSpeed) < 0.001f, "Combined flow speed cap failed");
                lab.Food.FlowSettings.ambientVelocity = new Vector2(0.3f, 0);
                var ambient = lab.Food.Spawn(new Vector2(-5, -3), 1); Vector2 start = ambient.transform.position;
                lab.ToggleMode(); for (int i = 0; i < 50; i++) Step(lab);
                report.ambientDisplacement = ambient.transform.position.x - start.x;
                Check(Mathf.Abs(report.ambientDisplacement - 0.3f) < 0.001f, "Ambient flow did not advect a particle");
                lab.ToggleMode(); lab.ResetLab(); yield return null;
                Check(lab.Food.Particles.Count == 0 && UnityEngine.Object.FindObjectsByType<Emerge.World.NutrientParticle>().Length == 0, "Flow reset left active particles");
                report.passed = Failure == null; report.failure = Failure ?? "";
                File.WriteAllText(Path.Combine(Folder, "t09-results.json"), JsonUtility.ToJson(report, true));
                File.WriteAllText(Path.Combine(Folder, "t09-trajectories.csv"), trace.ToString());
                if (report.passed) Debug.Log("CELL_LAB_T09_FLOW_PASS: actual particle transport, no-input and reversed controls, repeatable positive resource balance, fixed-direction supply-scaled flow, edit pause, source loss, ambient flow and cleanup.");
            }
            finally
            {
                if (!lab.IsEditing) lab.ToggleMode();
                lab.Food.FlowSettings.ambientVelocity = previousAmbient;
                Physics2D.simulationMode = previousMode;
                InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior = previousBackground;
                lab.ResetLab();
            }
        }
    }
}
#endif
