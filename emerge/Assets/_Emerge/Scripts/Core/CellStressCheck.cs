#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Emerge.Cells;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Debug = UnityEngine.Debug;

namespace Emerge.Core
{
    [Serializable]
    public sealed class StressPhaseResult
    {
        public string phase;
        public int steps;
        public float seconds;
        public float maxAnchorError, maxEdgeLengthError, maxNonAdjacentPenetration, maxSpeed, maxAngularSpeed;
        public float finalAnchorError, finalSpeed, finalAngularSpeed;
        public float maxRelativeAngleError, maxActivation;
        public int finalActiveMask;
        public double simulateMeanMs, simulateP95Ms, simulateMaxMs, driverMeanMs;
        public Vector2 corePosition;
        public string failure = "";
    }

    [Serializable]
    public sealed class StressCaseResult
    {
        public string shape;
        public int nodes, edges;
        public bool passed;
        public string failure = "";
        public List<StressPhaseResult> phases = new List<StressPhaseResult>();
    }

    [Serializable]
    public sealed class StressSuiteResult
    {
        public string timestampUtc, unity, runtime, processor, graphics, os;
        public int ramMb, velocityIterations, positionIterations;
        public float timestep, coreRadius, ciliaRadius, coreMass, ciliaMass, thrust;
        public float jointFrequency;
        public float coreSignalRetention, ciliaSignalRetention;
        public float maxAllowedAnchorError = 0.25f, maxAllowedPenetration = 0.06f, maxAllowedSpeed = 16f, maxAllowedAngularSpeed = 720f;
        public float recoveryAnchorLimit = 0.03f, recoverySpeedLimit = 0.15f, recoveryAngularLimit = 3f;
        public double simulateP95BudgetMs = 4;
        public bool passed;
        public string decision, failure = "";
        public List<string> runtimeErrors = new List<string>();
        public List<StressCaseResult> cases = new List<StressCaseResult>();
    }

    // Accelerated fixed-step tests: frame pacing is not used as a physics benchmark.
    public sealed class CellStressCheck : MonoBehaviour
    {
        private CellLabController lab;
        private Keyboard keyboard;
        private SimulationMode2D previousSimulation;
        private InputSettings.BackgroundBehavior previousBackground;
        private bool globalsChanged;
        private int originalCapacity;
        private float originalCameraSize;
        private readonly StressSuiteResult report = new StressSuiteResult();
        private readonly Dictionary<CellConnection, float> originalAngles = new Dictionary<CellConnection, float>();
        private readonly StringBuilder csv = new StringBuilder("shape,nodes,phase,seconds,anchor_max,edge_error_max,penetration_max,speed_max,angular_max,anchor_final,speed_final,angular_final,simulate_mean_ms,simulate_p95_ms,simulate_max_ms,driver_mean_ms,passed\n");

        private IEnumerator Start()
        {
            yield return null;
            lab = GetComponent<CellLabController>();
            originalCapacity = lab.Capacity; originalCameraSize = Camera.main.orthographicSize;
            report.timestampUtc = DateTime.UtcNow.ToString("O"); report.unity = Application.unityVersion;
            report.runtime = Application.isEditor ? "Editor" : "Windows development player";
            report.processor = SystemInfo.processorType; report.graphics = SystemInfo.graphicsDeviceName;
            report.os = SystemInfo.operatingSystem; report.ramMb = SystemInfo.systemMemorySize;
            report.velocityIterations = Physics2D.velocityIterations; report.positionIterations = Physics2D.positionIterations;
            report.timestep = Time.fixedDeltaTime;
            report.coreRadius = lab.CoreDefinition.radius; report.ciliaRadius = lab.CiliaDefinition.radius;
            report.coreMass = lab.CoreDefinition.mass; report.ciliaMass = lab.CiliaDefinition.mass; report.thrust = lab.CiliaDefinition.thrust;
            report.jointFrequency = CellLabPhysics.JointFrequency;
            report.coreSignalRetention = lab.CoreDefinition.signalRetention; report.ciliaSignalRetention = lab.CiliaDefinition.signalRetention;
            Application.logMessageReceived += OnLog;
            Debug.Log("CELL_STRESS_T05_START: thresholds fixed before execution; 6 fixtures, 67 simulated seconds each.");
            yield return CellPhysicsSmokeCheck.Run(lab);
            if (CellPhysicsSmokeCheck.Failure != null) { report.failure = CellPhysicsSmokeCheck.Failure; Finish(); yield break; }

            previousSimulation = Physics2D.simulationMode;
            previousBackground = InputSystem.settings.backgroundBehavior;
            globalsChanged = true;
            Physics2D.simulationMode = SimulationMode2D.Script;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            lab.Physics.enabled = false; lab.Physics.enabled = true;

            try
            {
                foreach (int count in new[] { 20, 30 })
                    foreach (string shape in new[] { "chain", "ring", "symmetric" })
                    {
                        var result = new StressCaseResult { shape = shape, nodes = count };
                        report.cases.Add(result);
                        if (!BuildCase(result)) continue;
                        yield return null;
                        string[] phases = { "settle", "continuous", "pulsed", "strong_flow", "recovery" };
                        int[] seconds = { 2, 20, 20, 20, 5 };
                        for (int phase = 0; phase < phases.Length; phase++)
                        {
                            var metrics = new StressPhaseResult { phase = phases[phase], seconds = seconds[phase] };
                            result.phases.Add(metrics);
                            int steps = Mathf.RoundToInt(seconds[phase] / report.timestep);
                            var simulateTimes = new List<double>(steps);
                            double driverTotal = 0;
                            for (int step = 0; step < steps; step++)
                            {
                                if (step == 0 || (phase == 2 && step % 25 == 0))
                                    SetKeys(phase == 1 || phase == 3 || (phase == 2 && (step / 25) % 2 == 0));
                                long start = Stopwatch.GetTimestamp();
                                lab.Physics.StepActuators();
                                if (phase == 3) ApplyFlow(step * report.timestep);
                                long simulationStart = Stopwatch.GetTimestamp();
                                bool simulated = Physics2D.Simulate(report.timestep);
                                long end = Stopwatch.GetTimestamp();
                                driverTotal += (simulationStart - start) * 1000.0 / Stopwatch.Frequency;
                                simulateTimes.Add((end - simulationStart) * 1000.0 / Stopwatch.Frequency);
                                metrics.steps++;
                                if (!simulated) metrics.failure = "Physics2D.Simulate returned false.";
                                Sample(metrics);
                                if (metrics.failure.Contains("non-finite")) break;
                                if (step % 50 == 49) yield return null;
                            }
                            simulateTimes.Sort();
                            double total = 0; foreach (double value in simulateTimes) total += value;
                            metrics.simulateMeanMs = total / simulateTimes.Count;
                            metrics.simulateP95Ms = simulateTimes[Math.Min(simulateTimes.Count - 1, (int)Math.Ceiling(simulateTimes.Count * 0.95) - 1)];
                            metrics.simulateMaxMs = simulateTimes[simulateTimes.Count - 1];
                            metrics.driverMeanMs = driverTotal / simulateTimes.Count;
                            Evaluate(metrics, phase);
                            AppendCsv(result, metrics);
                            if (metrics.failure.Length > 0) result.failure += metrics.phase + ": " + metrics.failure + " ";
                        }
                        SetKeys(false);
                        lab.ToggleMode();
                        for (int cycle = 0; cycle < 5; cycle++) { lab.ToggleMode(); lab.ToggleMode(); }
                        yield return null;
                        if (lab.Physics.JointCount != 0 || UnityEngine.Object.FindObjectsByType<FixedJoint2D>().Length != 0)
                            result.failure += "Stale joints after mode cycles. ";
                        foreach (var cell in lab.Cells)
                            if (cell.Body.linearVelocity.sqrMagnitude > 0.000001f || Mathf.Abs(cell.Body.angularVelocity) > 0.001f)
                                result.failure += "Editing retained velocity. ";
                        result.passed = result.failure.Length == 0;
                        Debug.Log("CELL_STRESS_CASE: " + count + "/" + shape + " " + (result.passed ? "PASS" : result.failure));
                    }
            }
            finally { RestoreGlobals(); }
            lab.PrepareStressLab(originalCapacity, originalCameraSize); lab.ResetLab();
            yield return null;
            if (UnityEngine.Object.FindObjectsByType<CellView>().Length != 2) report.failure += "Stress cleanup left cell objects. ";
            report.passed = report.cases.Count == 6 && report.failure.Length == 0 && report.runtimeErrors.Count == 0;
            foreach (var result in report.cases) report.passed &= result.passed;
            Finish();
        }

        private bool BuildCase(StressCaseResult result)
        {
            try
            {
                SetKeys(false);
                CellStressFixtures.Build(lab, result.shape, result.nodes);
                result.edges = lab.Graph.Edges.Count;
                lab.ToggleMode();
                originalAngles.Clear();
                foreach (var edge in lab.Graph.Edges) originalAngles.Add(edge, Mathf.DeltaAngle(edge.A.Body.rotation, edge.B.Body.rotation));
                if (lab.Physics.JointCount != result.edges) throw new InvalidOperationException("Edge / joint count mismatch.");
                return true;
            }
            catch (Exception exception) { result.failure = exception.ToString(); return false; }
        }

        private void SetKeys(bool active)
        {
            InputSystem.QueueStateEvent(keyboard, active ? new KeyboardState(Key.W, Key.A, Key.S, Key.D) : new KeyboardState());
            InputSystem.Update();
        }

        private void ApplyFlow(float time)
        {
            // A nonuniform drag load, not a claim that the nutritional flow system exists.
            foreach (var cell in lab.Cells)
            {
                Vector2 velocity = new Vector2(6 + 2 * Mathf.Sin(cell.Body.position.y * 0.3f + time), 2 * Mathf.Sin(time * 0.5f));
                cell.Body.AddForce((velocity - cell.Body.linearVelocity) * (2 * cell.Body.mass), ForceMode2D.Force);
            }
        }

        private void Sample(StressPhaseResult result)
        {
            float anchorError = 0, speed = 0, angular = 0;
            foreach (var joint in lab.Physics.ActiveJoints)
                anchorError = Mathf.Max(anchorError, Vector2.Distance(joint.transform.TransformPoint(joint.anchor), joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)));
            foreach (var edge in lab.Graph.Edges)
            {
                float error = Mathf.Abs(Vector2.Distance(edge.A.Body.position, edge.B.Body.position) - edge.A.Definition.radius - edge.B.Definition.radius);
                result.maxEdgeLengthError = Mathf.Max(result.maxEdgeLengthError, error);
                result.maxRelativeAngleError = Mathf.Max(result.maxRelativeAngleError,
                    Mathf.Abs(Mathf.DeltaAngle(originalAngles[edge], Mathf.DeltaAngle(edge.A.Body.rotation, edge.B.Body.rotation))));
            }
            for (int i = 0; i < lab.Cells.Count; i++)
            {
                var cell = lab.Cells[i];
                Vector2 position = cell.Body.position;
                if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.x) || float.IsInfinity(position.y))
                    result.failure = "non-finite body pose.";
                speed = Mathf.Max(speed, cell.Body.linearVelocity.magnitude);
                angular = Mathf.Max(angular, Mathf.Abs(cell.Body.angularVelocity));
                result.maxActivation = Mathf.Max(result.maxActivation, cell.Activation);
                for (int j = i + 1; j < lab.Cells.Count; j++)
                {
                    var other = lab.Cells[j];
                    if (lab.Graph.HasEdge(cell, other)) continue;
                    float penetration = cell.Definition.radius + other.Definition.radius - Vector2.Distance(position, other.Body.position);
                    result.maxNonAdjacentPenetration = Mathf.Max(result.maxNonAdjacentPenetration, penetration);
                }
            }
            result.maxAnchorError = Mathf.Max(result.maxAnchorError, anchorError);
            result.maxSpeed = Mathf.Max(result.maxSpeed, speed); result.maxAngularSpeed = Mathf.Max(result.maxAngularSpeed, angular);
            result.finalAnchorError = anchorError; result.finalSpeed = speed; result.finalAngularSpeed = angular;
            result.corePosition = lab.PrimaryCore.Body.position;
            result.finalActiveMask = lab.Physics.ActiveMask;
        }

        private void Evaluate(StressPhaseResult metrics, int phase)
        {
            if (metrics.maxAnchorError > report.maxAllowedAnchorError) metrics.failure += "Anchor error exceeded 0.25. ";
            if (metrics.maxNonAdjacentPenetration > report.maxAllowedPenetration) metrics.failure += "Non-adjacent penetration exceeded 0.06. ";
            if (metrics.maxSpeed > report.maxAllowedSpeed || metrics.maxAngularSpeed > report.maxAllowedAngularSpeed) metrics.failure += "Motion diverged. ";
            if (metrics.simulateP95Ms > report.simulateP95BudgetMs) metrics.failure += "Native simulation p95 exceeded 4ms. ";
            if (phase == 0 && (metrics.finalSpeed > 0.05f || metrics.finalAngularSpeed > 1f)) metrics.failure += "Initial impulse. ";
            if (phase == 4 && (metrics.maxActivation != 0 || metrics.finalActiveMask != 0)) metrics.failure += "Recovery retained input drive. ";
            if (phase == 4 && (metrics.finalAnchorError > report.recoveryAnchorLimit || metrics.finalSpeed > report.recoverySpeedLimit || metrics.finalAngularSpeed > report.recoveryAngularLimit))
                metrics.failure += "Did not settle within 5 seconds after drive and flow stopped. ";
        }

        private void AppendCsv(StressCaseResult trial, StressPhaseResult metrics)
        {
            csv.Append(trial.shape).Append(',').Append(trial.nodes).Append(',').Append(metrics.phase);
            double[] values = { metrics.seconds, metrics.maxAnchorError, metrics.maxEdgeLengthError, metrics.maxNonAdjacentPenetration, metrics.maxSpeed, metrics.maxAngularSpeed,
                metrics.finalAnchorError, metrics.finalSpeed, metrics.finalAngularSpeed, metrics.simulateMeanMs, metrics.simulateP95Ms, metrics.simulateMaxMs, metrics.driverMeanMs };
            foreach (double value in values) csv.Append(',').Append(value.ToString("F6", CultureInfo.InvariantCulture));
            csv.Append(',').Append(metrics.failure.Length == 0 ? "true" : "false").Append('\n');
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) && !message.StartsWith("CELL_STRESS_")) report.runtimeErrors.Add(message);
        }

        private void RestoreGlobals()
        {
            if (!globalsChanged) return;
            if (lab != null && !lab.IsEditing) lab.ToggleMode();
            if (keyboard != null) { InputSystem.RemoveDevice(keyboard); keyboard = null; }
            Physics2D.simulationMode = previousSimulation; InputSystem.settings.backgroundBehavior = previousBackground;
            globalsChanged = false;
        }

        private void Finish()
        {
            Application.logMessageReceived -= OnLog;
            report.decision = report.passed ? "retain_multi_body" : "needs_fix_or_fallback";
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs"));
            if (!Application.isEditor) folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "..", "..", "Logs");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "t05-results.json"), JsonUtility.ToJson(report, true));
            File.WriteAllText(Path.Combine(folder, "t05-results.csv"), csv.ToString());
            Debug.Log((report.passed ? "CELL_STRESS_T05_PASS" : "CELL_STRESS_T05_FAIL") + ": " + report.decision + " " + report.failure);
#if UNITY_EDITOR
            if (Application.isEditor) { UnityEditor.EditorApplication.isPlaying = false; return; }
#endif
            Application.Quit(report.passed ? 0 : 1);
        }

        private void OnDisable() { RestoreGlobals(); Application.logMessageReceived -= OnLog; }
    }
}
#endif
