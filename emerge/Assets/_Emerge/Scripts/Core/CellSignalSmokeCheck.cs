#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using Emerge.Cells;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Emerge.Core
{
    public static class CellSignalSmokeCheck
    {
        public static string Failure { get; private set; }
        private static void Check(bool value, string message)
        { if (!value && Failure == null) Failure = "T06: " + message; }
        private static void Near(float actual, float expected, string message)
        { Check(Mathf.Abs(actual - expected) < 0.0001f, message + " actual=" + actual); }
        private static IEnumerator Steps(int count)
        { for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate(); }

        public static IEnumerator Run(CellLabController lab)
        {
            Failure = null;
            int capacity = lab.Capacity;
            float cameraSize = Camera.main.orthographicSize;
            float coreRetention = lab.CoreDefinition.signalRetention, ciliaRetention = lab.CiliaDefinition.signalRetention;
            var background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            lab.Physics.enabled = false; lab.Physics.enabled = true;
            try
            {
                CellStressFixtures.Build(lab, "chain", 3);
                var core = lab.Cells[0]; var near = lab.Cells[1]; var far = lab.Cells[2];
                var first = lab.Graph.Edges[0]; var second = lab.Graph.Edges[1];
                Vector2 pointer = Camera.main.WorldToScreenPoint((first.A.transform.position + first.B.transform.position) / 2);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer, buttons = 1 });
                yield return null; yield return null;
                Check(lab.SelectedConnection == first && !lab.IsDragging, "Mouse did not select root bridge");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = pointer }); yield return null;
                var network = lab.Physics.Signals;
                network.Refresh(lab.Graph, lab.Cells, core);
                Near(network.Strength(core, near, 0), 0.9f, "One-hop loss");
                Near(network.Strength(core, far, 0), 0.81f, "Two-hop automatic relay");
                Near(network.Strength(core, far, 1), 0, "Wrong root channel leaked");
                int rebuild = network.RebuildCount;
                for (int i = 0; i < 100; i++) network.Refresh(lab.Graph, lab.Cells, core);
                Check(network.RebuildCount == rebuild, "Stable graph rebuilt paths");
                lab.SelectConnection(second);
                Check(!lab.Panel.SignalButtons[0].gameObject.activeSelf, "Downstream edge exposed channel UI");
                Check(!lab.Graph.ConfigureCoreChannel(second, core, IntentChannel.A), "Downstream edge accepted root configuration");
                lab.SetCoreChannel(1);
                network.Refresh(lab.Graph, lab.Cells, core);
                Near(network.Strength(core, far, 0), 0.81f, "Downstream UI changed relay");
                lab.Select(far);
                Check(!lab.Panel.SignalButtons[0].gameObject.activeSelf, "Cilia exposed WASD configuration");
                lab.SetCoreChannel(1);
                Check(first.CoreChannel == IntentChannel.W, "Cilia selection changed root channel");
                Key[] keys = { Key.W, Key.A, Key.S, Key.D };
                for (int c = 0; c < 4; c++)
                {
                    lab.SelectConnection(first); lab.Panel.SignalButtons[c].onClick.Invoke();
                    Check(first.CoreChannel == (IntentChannel)c, "Root channel UI failed");
                    lab.ToggleMode(); lab.SetCoreChannel((c + 1) % 4);
                    Check(first.CoreChannel == (IntentChannel)c, "Swim edited root channel");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[(c + 1) % 4]));
                    yield return null; yield return Steps(3);
                    Near(far.Activation, 0, "Wrong key drove branch");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[c]));
                    yield return null; yield return Steps(4);
                    Near(near.Activation, 0.9f, "First cell did not follow root");
                    Near(far.Activation, 0.81f, "Remote cell did not automatically follow root");
                    Near(far.LastForce.magnitude, far.Definition.thrust * 0.81f, "Relay did not drive actual force");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.A, Key.S, Key.D));
                    yield return null; yield return Steps(3);
                    Near(far.Activation, 0.81f, "Multi-key amplification");
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return null; yield return Steps(2);
                    Near(far.Activation, 0, "Release retained thrust");
                    lab.ToggleMode();
                }
                lab.SelectConnection(second); lab.DisconnectSelected();
                network.Refresh(lab.Graph, lab.Cells, core);
                Near(network.Strength(core, far, 3), 0, "Bridge removal retained cached signal");
                Check(lab.SelectedConnection == null, "Removed edge remained selected");

                // Separate root exits cannot communicate by passing back through the core.
                lab.PrepareStressLab(9, 12);
                core = lab.CreateStressCell(CellKind.Core, Vector3.zero, 0);
                var leaves = new CellView[4];
                Vector3[] directions = { Vector3.right, Vector3.up, Vector3.left, Vector3.down };
                for (int c = 0; c < 4; c++)
                {
                    var child = lab.CreateStressCell(CellKind.Cilia, directions[c] * 1.3f, 180);
                    leaves[c] = lab.CreateStressCell(CellKind.Cilia, directions[c] * 2.46f, 180);
                    Check(lab.Connect(core, child) && lab.Connect(child, leaves[c]), "Branch fixture failed");
                    lab.Graph.ConfigureCoreChannel(lab.Graph.Edges[c * 2], core, (IntentChannel)c);
                }
                lab.ToggleMode();
                for (int c = 0; c < 4; c++)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[c]));
                    yield return null; yield return Steps(3);
                    for (int other = 0; other < 4; other++) Near(leaves[other].Activation, other == c ? 0.81f : 0, "Branch isolation failed");
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.A, Key.S, Key.D));
                yield return null; yield return Steps(3);
                foreach (var leaf in leaves) Near(leaf.Activation, 0.81f, "Combined branches amplified");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                lab.ToggleMode();

                CellStressFixtures.Build(lab, "ring", 4);
                core = lab.Cells[0]; near = lab.Cells[1]; far = lab.Cells[3];
                lab.Graph.ConfigureCoreChannel(lab.Graph.Edges[3], core, IntentChannel.A);
                network.Refresh(lab.Graph, lab.Cells, core);
                Near(network.Strength(core, near, 0), 0.9f, "Ring W injection");
                Near(network.Strength(core, far, 0), 0.729f, "Ring failed to relay W");
                Near(network.Strength(core, near, 1), 0.729f, "Ring failed to relay A");
                Near(network.Strength(near, core, 0), 0, "Signal re-entered core");
                // A genuine downstream cycle, connected to the source by a single entry.
                lab.PrepareStressLab(4, 12);
                core = lab.CreateStressCell(CellKind.Core, new Vector3(-1.3f, 0, 0), 0);
                near = lab.CreateStressCell(CellKind.Cilia, Vector3.zero, 180);
                far = lab.CreateStressCell(CellKind.Cilia, new Vector3(1.16f, 0, 0), 180);
                var tip = lab.CreateStressCell(CellKind.Cilia, new Vector3(0.58f, 1.16f * Mathf.Sqrt(3) / 2, 0), 180);
                Check(lab.Connect(core, near) && lab.Connect(near, far) && lab.Connect(far, tip) && lab.Connect(tip, near), "Downstream cycle fixture failed");
                lab.CoreDefinition.signalRetention = lab.CiliaDefinition.signalRetention = 1;
                lab.Graph.ConfigureCoreChannel(lab.Graph.Edges[0], core, IntentChannel.W);
                network.Refresh(lab.Graph, lab.Cells, core);
                foreach (var cell in lab.Cells) Near(network.Strength(core, cell, 0), 1, "Lossless downstream ring amplified or failed to terminate");
                lab.Select(core); lab.DeleteSelected(); network.Refresh(lab.Graph, lab.Cells, lab.PrimaryCore);
                foreach (var cell in lab.Cells) Near(network.Strength(lab.PrimaryCore, cell, 0), 0, "Deleted source retained signal");
                if (Failure == null) Debug.Log("CELL_LAB_T06_SIGNAL_PASS: root-only W/A/S/D configuration, automatic cilia relay, 0.9/0.81 attenuation, wrong-key rejection, actual thrust, isolated branches, merged ring signals, no return through core, max-not-sum, lossless ring, source deletion and UI locks.");
            }
            finally
            {
                lab.CoreDefinition.signalRetention = coreRetention; lab.CiliaDefinition.signalRetention = ciliaRetention;
                if (!lab.IsEditing) lab.ToggleMode();
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.backgroundBehavior = background;
                lab.PrepareStressLab(capacity, cameraSize); lab.ResetLab();
            }
        }
    }
}
#endif
