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
    [Serializable]
    public sealed class M1MotionTrial
    {
        public string example;
        public int repeat, cells, fixedSteps;
        public Vector2 predictedForce, displacement;
        public float predictedTorque, angle;
    }

    [Serializable]
    public sealed class M1MotionReport
    {
        public string timestampUtc, unity, failure;
        public bool passed;
        public float timestep;
        public List<M1MotionTrial> trials = new List<M1MotionTrial>();
    }

    public static class CellM1SmokeCheck
    {
        public static string Failure { get; private set; }
        private static void Check(bool value, string message)
        { if (!value && Failure == null) Failure = "T07: " + message; }
        private static IEnumerator Steps(int count)
        { for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate(); }

        public static IEnumerator Run(CellLabController lab)
        {
            Failure = null;
            var report = new M1MotionReport { timestampUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion, timestep = Time.fixedDeltaTime };
            var previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            lab.Physics.enabled = false; lab.Physics.enabled = true;
            try
            {
                lab.Panel.ExampleButton.onClick.Invoke();
                Check(lab.Cells.Count == 3 && lab.Graph.Edges.Count == 2, "Example button did not load a valid body");
                for (int repeat = 0; repeat < 2; repeat++)
                    for (int kind = 0; kind < 3; kind++)
                    {
                        lab.LoadExample((LabExample)kind);
                        var core = lab.PrimaryCore;
                        Vector2 position = core.transform.position;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                        yield return null; yield return Steps(10);
                        var preview = CellForceSummary.Calculate(lab);
                        Check(lab.IsEditing && lab.Cells.Count == 3 && preview.ActiveCilia == 2, "Preview did not activate the equal-count body");
                        Check((Vector2)core.transform.position == position && !core.Body.simulated && core.LastForce == Vector2.zero && lab.Physics.JointCount == 0,
                            "Preview moved the editor body or created physics joints");
                        Check(lab.Panel.ForceText.Contains("预览") && lab.Panel.ForceText.Contains("合力"), "Preview UI omitted force feedback");
                        if (kind == 0) Check(preview.Force.x > 6 && Mathf.Abs(preview.Torque) < 0.001f, "Straight preview was not balanced");
                        if (kind == 1) Check(preview.Force.x > 3 && preview.Force.y < -3 && preview.Torque < -4, "Turning preview had wrong force or torque");
                        if (kind == 2) Check(preview.Force.x < -6 && Mathf.Abs(preview.Torque) < 0.001f, "Reverse preview did not reverse the force");
                        if (repeat == 0)
                        {
                            yield return new WaitForEndOfFrame();
                            string folder = EvidenceFolder(); Directory.CreateDirectory(folder);
                            CellLabSmokeCheck.CapturePreview(Path.Combine(folder, "t07-preview-" + ((LabExample)kind) + ".png"));
                        }
                        lab.ToggleMode();
                        Vector2 initial = core.Body.position; float rotation = core.Body.rotation;
                        lab.Panel.ExampleButton.onClick.Invoke();
                        Check(lab.Cells.Count == 3 && lab.PrimaryCore == core && !lab.Panel.ExampleButton.interactable, "Swim mode replaced the body");
                        yield return Steps(60);
                        var trial = new M1MotionTrial { example = ((LabExample)kind).ToString(), repeat = repeat + 1, cells = lab.Cells.Count, fixedSteps = 60,
                            predictedForce = preview.Force, predictedTorque = preview.Torque, displacement = core.Body.position - initial, angle = Mathf.DeltaAngle(rotation, core.Body.rotation) };
                        report.trials.Add(trial);
                        if (kind == 0) Check(trial.displacement.x > 0.1f && Mathf.Abs(trial.displacement.y) < 0.02f && Mathf.Abs(trial.angle) < 2,
                            "Straight body did not translate without appreciable turning");
                        if (kind == 1) Check(trial.displacement.x > 0.03f && trial.displacement.y < -0.03f && trial.angle < -5, "Turning body failed to translate and rotate clockwise");
                        if (kind == 2) Check(trial.displacement.x < -0.1f && Mathf.Abs(trial.displacement.y) < 0.02f && Mathf.Abs(trial.angle) < 2,
                            "Reverse body failed to translate in the opposite direction");
                        if (repeat == 1)
                        {
                            var previous = report.trials[kind];
                            Check(Vector2.Distance(previous.displacement, trial.displacement) < 0.03f && Mathf.Abs(previous.angle - trial.angle) < 3,
                                "Repeated equal-count motion differed beyond tolerance");
                        }
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return Steps(3);
                        Check(lab.Cells[1].LastForce == Vector2.zero && lab.Cells[2].LastForce == Vector2.zero, "Release retained thrust");
                        lab.ToggleMode();
                        foreach (var cell in lab.Cells) Check(cell.Body.linearVelocity == Vector2.zero && cell.Body.angularVelocity == 0 && !cell.Body.simulated, "Edit mode retained motion");
                        Debug.Log("CELL_LAB_T07_TRIAL: " + JsonUtility.ToJson(trial));
                    }
                // Change only root signal configuration while keeping the same layout and cell count.
                lab.LoadExample(LabExample.Straight);
                foreach (var edge in lab.Graph.Edges) lab.Graph.ConfigureCoreChannel(edge, lab.PrimaryCore, IntentChannel.A);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); yield return null; yield return Steps(3);
                Check(CellForceSummary.Calculate(lab).Force == Vector2.zero, "Wrong preview key activated the A layout");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A)); yield return null; yield return Steps(3);
                Check(CellForceSummary.Calculate(lab).Force.x > 6, "Changing only the root signal failed to change preview response");
                lab.ToggleMode(); yield return Steps(15);
                Check(lab.Cells[1].Activation > 0.89f && lab.Cells[2].Activation > 0.89f, "Changed root signal did not drive actual actuators");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                lab.ToggleMode();
                report.passed = Failure == null; report.failure = Failure ?? "";
                Directory.CreateDirectory(EvidenceFolder());
                File.WriteAllText(Path.Combine(EvidenceFolder(), "t07-results.json"), JsonUtility.ToJson(report, true));
                if (Failure == null) Debug.Log("CELL_LAB_T07_M1_PASS: equal-count straight/turn/reverse repeated, input preview without motion, predicted force/torque vs actual physics, root signal change, release and mode cleanup.");
            }
            finally
            {
                if (!lab.IsEditing) lab.ToggleMode();
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = previousBackground;
                lab.ResetLab();
            }
        }

        private static string EvidenceFolder() => Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs")) :
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), "..", "..", "Logs"));
    }
}
#endif
