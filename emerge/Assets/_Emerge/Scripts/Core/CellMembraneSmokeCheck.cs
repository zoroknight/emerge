#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;
using Emerge.Cells;
using Emerge.World;
using UnityEngine;

namespace Emerge.Core
{
    [Serializable] public sealed class MembraneTrial
    {
        public string control;
        public int repeat, steps, contacts;
        public float ingested, remaining, produced, maintenance, finalEnergy, finalNutrients, bodyDrift, ledgerError;
    }
    [Serializable] public sealed class MembraneReport
    {
        public string timestampUtc, unity, failure;
        public bool passed, unlimitedEnergy, highSpeedBlocked, backSideBlocked, tipsPassable, movedBarrier, rotatedBarrier, pocketRetained, pocketOpened, editPaused, disconnectedBlocks;
        public float timestep, initialFood, thickness, minimumClearance;
        public List<MembraneTrial> trials = new List<MembraneTrial>();
    }
    public static class CellMembraneSmokeCheck
    {
        public static string Failure { get; private set; }
        private static void Check(bool ok, string message) { if (!ok && Failure == null) Failure = "T10: " + message; }
        private static string Folder => Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs")) :
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "..", "..", "Logs"));
        private static float Remaining(CellLabController lab)
        { float sum = 0; foreach (var p in lab.Food.Particles) sum += p.Remaining; return sum; }
        private static void Step(CellLabController lab)
        { lab.Physics.StepActuators(); Physics2D.Simulate(Time.fixedDeltaTime); }
        public static IEnumerator Run(CellLabController lab)
        {
            Failure = null;
            var report = new MembraneReport { timestampUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                timestep = Time.fixedDeltaTime, initialFood = 9.6f, thickness = MembraneTransport.HalfThickness * 2, minimumClearance = 100 };
            var previousMode = Physics2D.simulationMode;
            var previousAmbient = lab.Food.FlowSettings.ambientVelocity;
            var trace = new StringBuilder("control,step,particle,x,y,remaining,distance_to_absorber,membrane_clearance\n");
            Physics2D.simulationMode = SimulationMode2D.Script;
            lab.Food.FlowSettings.ambientVelocity = Vector2.zero;
            Directory.CreateDirectory(Folder);
            try
            {
                Check(!lab.Metabolism.DebugUnlimited, "Resource limits bypassed");
                lab.ClearLab(); lab.SpawnMembrane(); var membrane = lab.Cells[0];
                membrane.transform.position = Vector3.zero; membrane.transform.rotation = Quaternion.identity;
                var end = MembraneTransport.Move(lab.Cells, new Vector2(-2, 0), new Vector2(20, 0), out int contacts);
                report.highSpeedBlocked = end.x < -0.199f && contacts > 0;
                end = MembraneTransport.Move(lab.Cells, new Vector2(2, 0), new Vector2(-20, 0), out contacts);
                report.backSideBlocked = end.x > 0.199f && contacts > 0;
                end = MembraneTransport.Move(lab.Cells, new Vector2(-2, 1.1f), new Vector2(4, 0), out contacts);
                report.tipsPassable = end.x > 1.99f && contacts == 0;
                membrane.transform.position = new Vector3(1, 0, 0);
                end = MembraneTransport.Move(lab.Cells, new Vector2(-2, 0), new Vector2(4, 0), out contacts);
                report.movedBarrier = Mathf.Abs(end.x - 0.7999f) < 0.001f;
                membrane.transform.position = Vector3.zero; membrane.transform.rotation = Quaternion.Euler(0, 0, 90);
                end = MembraneTransport.Move(lab.Cells, new Vector2(-2, 0.4f), new Vector2(4, 0), out contacts);
                report.rotatedBarrier = end.x > 1.99f;
                membrane.transform.rotation = Quaternion.identity;
                lab.Graph.Disconnect(membrane);
                end = MembraneTransport.Move(lab.Cells, new Vector2(-2, 0), new Vector2(4, 0), out contacts);
                report.disconnectedBlocks = end.x < -0.199f;
                Check(report.highSpeedBlocked && report.backSideBlocked && report.tipsPassable && report.movedBarrier && report.rotatedBarrier && report.disconnectedBlocks,
                    "Finite, two-sided, translated/rotated or passive barrier failed");

                // An open-left pocket catches right-moving food; rotate the front and it escapes.
                lab.SpawnMembrane(); lab.SpawnMembrane();
                lab.Cells[1].transform.SetPositionAndRotation(new Vector3(-0.6f, 0.9f, 0), Quaternion.Euler(0, 0, 90));
                lab.Cells[2].transform.SetPositionAndRotation(new Vector3(-0.6f, -0.9f, 0), Quaternion.Euler(0, 0, 90));
                end = new Vector2(-0.8f, 0.1f);
                for (int i = 0; i < 500; i++) end = MembraneTransport.Move(lab.Cells, end, new Vector2(0.016f, 0), out contacts);
                report.pocketRetained = end.x < -0.199f && end.x > -0.201f;
                membrane.transform.rotation = Quaternion.Euler(0, 0, 90);
                // Above the new front's expanded surface, the same stream has a clear exit.
                end = new Vector2(-0.8f, 0.3f);
                for (int i = 0; i < 500; i++) end = MembraneTransport.Move(lab.Cells, end, new Vector2(0.016f, 0), out contacts);
                report.pocketOpened = end.x > 2;
                Check(report.pocketRetained && report.pocketOpened, "Pocket retention did not depend on structure");

                lab.LoadMembraneTrial(0);
                Vector2 foodStart = lab.Food.Particles[0].transform.position;
                for (int i = 0; i < 50; i++) Step(lab);
                report.editPaused = (Vector2)lab.Food.Particles[0].transform.position == foodStart && lab.Metabolism.Ingested == 0;
                Check(report.editPaused && lab.Graph.Edges.Count == 2 && lab.MembraneDefinition.absorptionRate == 0,
                    "Edit pause, membrane connection or zero absorption failed");
                lab.ToggleMode(); lab.SpawnMembrane();
                Check(lab.Cells.Count == 3 && !lab.Panel.MembraneButton.interactable, "Swim mode allowed membrane spawning");
                lab.ToggleMode();
                int[] variants = { 0, 1, 2, 0 };
                for (int trialIndex = 0; trialIndex < variants.Length; trialIndex++)
                {
                    int variant = variants[trialIndex]; lab.LoadMembraneTrial(variant); lab.ToggleMode();
                    Vector2 bodyStart = lab.PrimaryCore.Body.position;
                    var trial = new MembraneTrial { control = variant == 1 ? "no_membrane" : variant == 2 ? "wrong_orientation" : "guiding",
                        repeat = trialIndex == 3 ? 2 : 1, steps = 650 };
                    var observed = new List<NutrientParticle>(lab.Food.Particles);
                    for (int step = 0; step < trial.steps; step++)
                    {
                        Step(lab);
                        var state = lab.Metabolism;
                        trial.ledgerError = Mathf.Max(trial.ledgerError, Mathf.Abs(report.initialFood - Remaining(lab) - state.Ingested),
                            Mathf.Abs(state.Ingested - state.Nutrients - state.Metabolized),
                            Mathf.Abs(5 + state.Produced - state.Energy - state.ActivitySpent - state.MaintenanceSpent));
                        if (step % 10 == 0)
                            for (int p = 0; p < observed.Count; p++)
                            {
                                var mote = observed[p]; if (mote == null || mote.Remaining <= 0) continue;
                                Vector2 position = mote.transform.position; float clearance = -1;
                                if (variant != 1)
                                {
                                    MembraneTransport.Geometry(lab.Cells[2], false, out var a, out var b);
                                    clearance = Vector2.Distance(position, MembraneTransport.Closest(position, a, b));
                                    report.minimumClearance = Mathf.Min(report.minimumClearance, clearance);
                                }
                                trace.AppendFormat(CultureInfo.InvariantCulture, "{0},{1},{2},{3:F6},{4:F6},{5:F6},{6:F6},{7:F6}\n",
                                    trial.control, step, p, position.x, position.y, mote.Remaining,
                                    Vector2.Distance(position, lab.Cells[1].Body.worldCenterOfMass), clearance);
                            }
                        if (trialIndex < 3 && (step == 0 || step == 249))
                        {
                            yield return null; yield return new WaitForEndOfFrame();
                            CellLabSmokeCheck.CapturePreview(Path.Combine(Folder, "t10-" + trial.control + (step == 0 ? "-start.png" : "-feeding.png")));
                        }
                    }
                    trial.ingested = lab.Metabolism.Ingested; trial.remaining = Remaining(lab); trial.produced = lab.Metabolism.Produced;
                    trial.maintenance = lab.Metabolism.MaintenanceSpent; trial.finalEnergy = lab.Metabolism.Energy;
                    trial.finalNutrients = lab.Metabolism.Nutrients; trial.contacts = lab.Food.MembraneContacts;
                    trial.bodyDrift = Vector2.Distance(bodyStart, lab.PrimaryCore.Body.position);
                    report.trials.Add(trial);
                    Check(trial.ledgerError < 0.003f && trial.bodyDrift < 0.02f && lab.Metabolism.ActivitySpent == 0,
                        "Resource conservation or stationary comparison failed");
                    Check(variant == 0 ? trial.ingested > 2 : trial.ingested < 0.001f, "Membrane orientation did not determine ingestion: " + JsonUtility.ToJson(trial));
                    Debug.Log("CELL_LAB_T10_TRIAL: " + JsonUtility.ToJson(trial));
                    lab.ToggleMode();
                }
                Check(report.minimumClearance >= MembraneTransport.HalfThickness + NutrientParticle.Radius - 0.001f,
                    "Trajectory penetrated the membrane");
                Check(Mathf.Abs(report.trials[0].ingested - report.trials[3].ingested) < 0.001f &&
                    Mathf.Abs(report.trials[0].finalEnergy - report.trials[3].finalEnergy) < 0.001f, "Guiding trial not repeatable");
                lab.ResetLab(); yield return null;
                Check(lab.Food.Particles.Count == 0 && lab.Food.FlowRegions.Count == 0 && UnityEngine.Object.FindObjectsByType<CellView>().Length == 2,
                    "Membrane reset left stale food, trial stream or cells");
                report.passed = Failure == null; report.failure = Failure ?? "";
                File.WriteAllText(Path.Combine(Folder, "t10-results.json"), JsonUtility.ToJson(report, true));
                File.WriteAllText(Path.Combine(Folder, "t10-trajectories.csv"), trace.ToString());
                if (report.passed) Debug.Log("CELL_LAB_T10_MEMBRANE_PASS: swept finite passive barriers, orientation-dependent capture, pocket retention, repeatable comparison, conservation, edit pause and cleanup.");
            }
            finally
            {
                if (!lab.IsEditing) lab.ToggleMode();
                lab.Food.FlowSettings.ambientVelocity = previousAmbient;
                Physics2D.simulationMode = previousMode;
                lab.ResetLab();
            }
        }
    }
}
#endif
