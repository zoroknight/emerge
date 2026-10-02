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
    [Serializable] public sealed class StomachContractionReport
    {
        public string timestampUtc, unity, failure;
        public bool passed, retained, delayed, capacityLimited, fullStoragePaused, detachedPaused, deletedReleased, editPaused;
        public float digestedAfterOneSecond, maxLedgerError, restSpan, contractedSpan, restoredSpan, endpointMotion, centerOfMassDrift;
        public float contraction, cost, supplyAtEmpty, shortageContraction, maximumJointError, maximumSpeed;
        public int cycles, mixedNodes;
        public bool unlimitedPhysicalTrial;
    }
    public static class CellStomachContractionSmokeCheck
    {
        public static string Failure { get; private set; }
        private static void Check(bool ok, string reason) { if (!ok && Failure == null) Failure = reason; }
        private static void Input(Keyboard keyboard, params Key[] keys)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
        private static void Step(CellLabController lab, int count)
        { for (int i = 0; i < count; i++) { lab.Physics.StepActuators(); Physics2D.Simulate(Time.fixedDeltaTime); } }
        private static float Span(CellLabController lab) => Vector2.Distance(lab.Cells[0].Body.position, lab.Cells[2].Body.position);
        private static Vector2 Center(CellLabController lab)
        { Vector2 sum = Vector2.zero; float mass = 0; foreach (var c in lab.Cells) { sum += c.Body.position * c.Body.mass; mass += c.Body.mass; } return sum / mass; }
        private static void Ledger(CellLabController lab, StomachContractionReport report, float initialN, float initialE, float initialFood)
        {
            float remaining = 0; foreach (var p in lab.Food.Particles) remaining += p.Remaining;
            var s = lab.Metabolism;
            float error = Mathf.Max(Mathf.Abs(initialFood - remaining - s.Ingested),
                Mathf.Abs(initialN + s.Ingested - s.Nutrients - s.Metabolized),
                Mathf.Abs(initialE + s.Produced - s.Energy - s.ActivitySpent - s.MaintenanceSpent));
            report.maxLedgerError = Mathf.Max(report.maxLedgerError, error);
            Check(error < 0.003f, "Stored digestion or contraction resource conservation failed");
        }
        private static string Folder => Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs")) :
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "..", "..", "Logs"));
        public static IEnumerator Run(CellLabController lab)
        {
            Failure = null;
            var report = new StomachContractionReport { timestampUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion };
            var previousMode = Physics2D.simulationMode;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            var ambient = lab.Food.FlowSettings.ambientVelocity;
            bool previousUnlimited = lab.Metabolism.DebugUnlimited;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            lab.Physics.enabled = false; lab.Physics.enabled = true; Physics2D.simulationMode = SimulationMode2D.Script;
            Directory.CreateDirectory(Folder);
            try
            {
                Input(keyboard); lab.LoadExample(LabExample.Feeding); lab.Metabolism.Reset(0, 0);
                var absorber = lab.Cells[2]; var food = lab.Food.Spawn(absorber.transform.position, 2);
                lab.ToggleMode(); Step(lab, 1);
                report.delayed = food.IsCaptured && food.Remaining == 2 && lab.Metabolism.Ingested == 0 && lab.Metabolism.Energy == 0;
                lab.Food.FlowSettings.ambientVelocity = new Vector2(4, 0);
                absorber.Body.position += Vector2.right; absorber.Body.rotation = 90;
                lab.Food.Advect(Time.fixedDeltaTime); food.FollowCaptor();
                report.retained = Vector2.Distance(food.transform.position, absorber.Body.position) < absorber.Definition.radius;
                lab.ToggleMode(); lab.LoadExample(LabExample.Feeding); absorber = lab.Cells[2];
                lab.Metabolism.Reset(0, 0); food = lab.Food.Spawn(absorber.transform.position, 2);
                lab.Food.FlowSettings.ambientVelocity = Vector2.zero; lab.ToggleMode(); Step(lab, 50);
                report.digestedAfterOneSecond = lab.Metabolism.Ingested;
                Ledger(lab, report, 0, 0, 2);
                Check(report.delayed && report.retained && report.digestedAfterOneSecond > 0.9f && report.digestedAfterOneSecond < 1.2f,
                    "Capture did not retain particles or delay digestion");
                Check(Mathf.Abs(food.Remaining + lab.Metabolism.Ingested - 2) < 0.001f, "Stored food conservation failed");
                lab.ToggleMode(); float before = food.Remaining; Step(lab, 100);
                report.editPaused = food.Remaining == before;
                lab.Metabolism.Reset(8, 10);
                for (int i = 0; i < 100; i++) lab.Food.Capture(Time.fixedDeltaTime, lab.Graph.Component(lab.PrimaryCore));
                report.fullStoragePaused = food.Remaining == before && lab.Metabolism.Ingested == 0;
                for (int i = 0; i < 20; i++) lab.Food.Spawn(absorber.transform.position);
                lab.Food.Capture(Time.fixedDeltaTime, lab.Graph.Component(lab.PrimaryCore));
                report.capacityLimited = lab.Food.StoredFood(absorber) <= absorber.Definition.foodCapacity + 0.00001f &&
                    lab.Food.StoredCount(absorber) <= absorber.Definition.foodSlots && lab.Food.StoredCount(absorber) < lab.Food.Particles.Count;
                lab.Graph.Disconnect(absorber); lab.Metabolism.Reset(0, 0); lab.ToggleMode(); Step(lab, 50);
                report.detachedPaused = food.Remaining == before && food.IsCaptured && lab.Metabolism.Ingested == 0;
                lab.ToggleMode(); lab.Select(absorber); lab.DeleteSelected();
                report.deletedReleased = !food.IsCaptured && food.Remaining == before;
                Check(report.editPaused && report.fullStoragePaused && report.capacityLimited && report.detachedPaused && report.deletedReleased,
                    "Storage limits, pause, detached digestion or deletion release failed");
                lab.LoadExample(LabExample.Feeding); lab.Metabolism.Reset(0, 0); lab.Food.SeedPatch(); lab.ToggleMode(); Step(lab, 1);
                yield return null; yield return new WaitForEndOfFrame();
                CellLabSmokeCheck.CapturePreview(Path.Combine(Folder, "stomach-captured.png"));
                Step(lab, 100);
                yield return null; yield return new WaitForEndOfFrame();
                CellLabSmokeCheck.CapturePreview(Path.Combine(Folder, "stomach-digesting.png"));
                lab.ToggleMode();

                lab.LoadContractionExample(); Input(keyboard, Key.A); Step(lab, 100);
                Check(lab.Cells[1].Contraction == 0 && lab.Metabolism.Energy == 8, "Edit preview contracted or charged energy");
                lab.ToggleMode(); Input(keyboard); Step(lab, 20);
                report.restSpan = Span(lab); Vector2 start = lab.Cells[0].Body.position, center = Center(lab);
                Input(keyboard, Key.W); Step(lab, 50); Check(lab.Cells[1].Contraction == 0, "Wrong core channel contracted");
                Input(keyboard, Key.A); Step(lab, 100);
                report.contractedSpan = Span(lab); report.contraction = lab.Cells[1].Contraction;
                report.endpointMotion = Vector2.Distance(start, lab.Cells[0].Body.position);
                report.centerOfMassDrift = Vector2.Distance(center, Center(lab)); report.cost = lab.Metabolism.ActivitySpent;
                Ledger(lab, report, 2, 8, 0);
                Check(report.restSpan - report.contractedSpan > 0.45f && report.endpointMotion > 0.1f && report.contraction > 0.85f && report.cost > 1,
                    "Contraction did not change actual joint distances and endpoint motion");
                Check(report.centerOfMassDrift < 0.01f && lab.Cells[1].LastForce == Vector2.zero, "Contraction injected direct propulsion");
                yield return null; yield return new WaitForEndOfFrame();
                CellLabSmokeCheck.CapturePreview(Path.Combine(Folder, "contraction-held.png"));
                lab.ToggleMode(); float paused = Span(lab), level = lab.Cells[1].Contraction;
                Step(lab, 100); Check(Span(lab) == paused && lab.Cells[1].Contraction == level, "Pause lost contracted pose");
                lab.ToggleMode(); Input(keyboard); Step(lab, 100); report.restoredSpan = Span(lab);
                Check(Mathf.Abs(report.restoredSpan - report.restSpan) < 0.03f && lab.Cells[1].Contraction == 0, "Release failed to restore rest length");
                yield return null; yield return new WaitForEndOfFrame();
                CellLabSmokeCheck.CapturePreview(Path.Combine(Folder, "contraction-released.png"));
                lab.Metabolism.Reset(0, 0); Input(keyboard, Key.A); Step(lab, 50);
                report.supplyAtEmpty = lab.Metabolism.SupplyRatio;
                Check(lab.Cells[1].Contraction == 0 && report.supplyAtEmpty == 0, "Starved contraction used free work");
                lab.Metabolism.Reset(0, 0.01f); Step(lab, 1); report.shortageContraction = lab.Cells[1].Contraction;
                Check(report.shortageContraction > 0 && report.shortageContraction < 0.9f, "Shortage failed uniform contraction scaling");
                Input(keyboard); lab.ToggleMode(); lab.LoadContractionExample();
                lab.Graph.Disconnect(lab.Cells[1]); lab.ToggleMode(); Input(keyboard, Key.A); Step(lab, 50);
                Check(lab.Cells[1].Contraction == 0 && lab.Physics.ContractionJoints.Count == 0, "Detached cell retained contraction control");
                lab.ToggleMode(); Input(keyboard);

                // Only the stability benchmark bypasses energy to prevent starvation masking failures.
                lab.Metabolism.DebugUnlimited = true; report.unlimitedPhysicalTrial = true;
                foreach (int count in new[] { 20, 30 })
                {
                    lab.PrepareStressLab(count, 12);
                    float x = -count * 0.67f;
                    for (int i = 0; i < count; i++)
                    {
                        CellKind kind = i == 0 ? CellKind.Core : i == count - 1 ? CellKind.Absorber : i % 3 == 0 ? CellKind.Cilia : CellKind.Contractor;
                        var data = kind == CellKind.Core ? lab.CoreDefinition : kind == CellKind.Absorber ? lab.AbsorberDefinition : kind == CellKind.Cilia ? lab.CiliaDefinition : lab.ContractorDefinition;
                        if (i > 0) x += lab.Cells[i - 1].Definition.radius + data.radius;
                        lab.CreateStressCell(kind, new Vector3(x, -1.5f, 0), 180);
                        if (i > 0) Check(lab.Connect(lab.Cells[i - 1], lab.Cells[i]), "Mixed fixture failed connection");
                    }
                    report.mixedNodes = lab.Cells.Count; lab.ToggleMode();
                    for (int step = 0; step < 3000; step++)
                    {
                        if (step % 50 == 0) Input(keyboard, (step / 50) % 2 == 0 ? new[] { Key.W } : new Key[0]);
                        Step(lab, 1);
                        foreach (var joint in lab.Physics.ContractionJoints)
                            report.maximumJointError = Mathf.Max(report.maximumJointError,
                                Mathf.Abs(Vector2.Distance(joint.attachedRigidbody.position, joint.connectedBody.position) - joint.distance));
                        foreach (var cell in lab.Cells)
                        {
                            report.maximumSpeed = Mathf.Max(report.maximumSpeed, cell.Body.linearVelocity.magnitude);
                            Check(float.IsFinite(cell.Body.position.x) && float.IsFinite(cell.Body.position.y), "Mixed contraction chain became non-finite");
                        }
                        if (step % 100 == 99) yield return null;
                    }
                    report.cycles += 30;
                    Input(keyboard); lab.ToggleMode();
                }
                lab.Metabolism.DebugUnlimited = previousUnlimited;
                Check(report.maximumJointError < 0.25f && report.maximumSpeed < 16, "Mixed contraction chain unstable");
                Input(keyboard); lab.ToggleMode(); lab.PrepareStressLab(20, 6); lab.ResetLab(); yield return null;
                Check(UnityEngine.Object.FindObjectsByType<DistanceJoint2D>().Length == 0 && lab.Food.Particles.Count == 0, "Reset leaked joints or stored food");
                report.passed = Failure == null; report.failure = Failure ?? "";
                File.WriteAllText(Path.Combine(Folder, "stomach-contraction-results.json"), JsonUtility.ToJson(report, true));
                if (report.passed) Debug.Log("CELL_LAB_STOMACH_CONTRACTION_PASS: stored visible food, delayed digestion, capacity, pause/release, signal-powered real shortening/recovery and mixed physical stability.");
            }
            finally
            {
                Input(keyboard); if (!lab.IsEditing) lab.ToggleMode();
                lab.Metabolism.DebugUnlimited = previousUnlimited;
                lab.Food.FlowSettings.ambientVelocity = ambient; lab.PrepareStressLab(20, 6); lab.ResetLab();
                Physics2D.simulationMode = previousMode; InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior = previousBackground;
            }
        }
    }
}
#endif
