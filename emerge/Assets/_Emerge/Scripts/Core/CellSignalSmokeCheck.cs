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
                Vector2 bridgePointer = Camera.main.WorldToScreenPoint((first.A.transform.position + first.B.transform.position) / 2);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = bridgePointer, buttons = 1 });
                yield return null; yield return null;
                Check(lab.SelectedConnection == first && !lab.IsDragging, "Mouse click did not select tangent bridge");
                InputSystem.QueueStateEvent(mouse, new MouseState { position = bridgePointer });
                yield return null;
                var network = lab.Physics.Signals;
                network.Refresh(lab.Graph, lab.Cells);
                Near(network.Strength(core, near, 0), 0.9f, "One-hop attenuation");
                Near(network.Strength(core, far, 0), 0.81f, "Two-hop attenuation");
                Near(network.Strength(core, far, 1), 0, "Default route leaked A");
                int rebuild = network.RebuildCount;
                for (int i = 0; i < 100; i++) network.Refresh(lab.Graph, lab.Cells);
                Check(network.RebuildCount == rebuild, "Stable graph rebuilt signal paths");
                lab.SelectConnection(second);
                lab.Panel.SignalButtons[0].onClick.Invoke();
                network.Refresh(lab.Graph, lab.Cells);
                Near(network.Strength(core, far, 0), 0, "Disabled route still conducted");
                Check(lab.Graph.Edges.Count == 2 && lab.IsCoreConnected(far), "Disabling signal removed mechanical connection");
                lab.Panel.SignalButtons[0].onClick.Invoke();
                lab.Panel.EfficiencyButton.onClick.Invoke();
                network.Refresh(lab.Graph, lab.Cells);
                Near(network.Strength(core, far, 0), 0.6075f, "Edge efficiency did not attenuate");
                lab.Graph.Configure(second, 15, 1); lab.Graph.Configure(first, 15, 1);
                lab.Select(far);
                for (int c = 1; c < 4; c++) lab.Panel.SignalButtons[c].onClick.Invoke();
                Check(far.ResponseMask == 15, "Response UI did not enable four channels");
                lab.SelectConnection(second);
                float efficiencyBefore = second.Efficiency;
                lab.ToggleMode();
                lab.Panel.SignalButtons[0].onClick.Invoke(); lab.Panel.EfficiencyButton.onClick.Invoke();
                Check(second.ChannelMask == 15 && second.Efficiency == efficiencyBefore, "Swim mode edited connection route");
                lab.Select(far); lab.Panel.SignalButtons[0].onClick.Invoke();
                Check(far.ResponseMask == 15, "Swim mode edited response mask");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return null; yield return Steps(8);
                Near(far.Activation, 0.81f, "Actual W input lost propagated strength");
                Near(far.LastForce.magnitude, far.Definition.thrust * 0.81f, "Actual actuator force differs from received signal");
                Key[] individualKeys = { Key.A, Key.S, Key.D };
                foreach (Key key in individualKeys)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                    yield return null; yield return Steps(3);
                    Near(far.Activation, 0.81f, "Individual input channel did not propagate: " + key);
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.A, Key.S, Key.D));
                yield return null; yield return Steps(5);
                Near(far.Activation, 0.81f, "Multiple keys amplified actuator");
                Check(lab.Physics.ActiveMask == 15, "Four-channel input subscription failed");
                lab.Graph.Configure(second, 0, 1);
                yield return Steps(2);
                Near(far.Activation, 0, "Runtime circuit break did not stop actuator");
                Check(lab.Physics.JointCount == 2, "Circuit break removed physical joint");
                lab.Graph.Configure(second, 15, 1);
                yield return Steps(2);
                Near(far.Activation, 0.81f, "Runtime route restore did not refresh cache");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return Steps(2);
                Near(far.Activation, 0, "Release retained propagated activation");
                far.SetResponseMask(0);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return null; yield return Steps(2);
                Near(far.Activation, 0, "Disabled actuator response still fired");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                lab.ToggleMode();
                lab.SelectConnection(second); lab.DisconnectSelected();
                network.Refresh(lab.Graph, lab.Cells);
                Near(network.Strength(core, far, 0), 0, "Removed bridge retained cached path");
                Check(lab.SelectedConnection == null, "Removed edge stayed selected");

                CellStressFixtures.Build(lab, "ring", 4);
                core = lab.Cells[0]; near = lab.Cells[1];
                first = lab.Graph.Edges[0];
                lab.Graph.Configure(first, 1, 0.5f);
                network.Refresh(lab.Graph, lab.Cells);
                Near(network.Strength(core, near, 0), 0.729f, "Ring failed to choose stronger indirect path");
                lab.Graph.Configure(first, 0, 1);
                network.Refresh(lab.Graph, lab.Cells);
                Near(network.Strength(core, near, 0), 0.729f, "Ring bypass failed");
                lab.CoreDefinition.signalRetention = lab.CiliaDefinition.signalRetention = 1;
                lab.Graph.Configure(first, 15, 1);
                foreach (var edge in lab.Graph.Edges) lab.Graph.Configure(edge, 15, 1);
                network.Refresh(lab.Graph, lab.Cells);
                foreach (var cell in lab.Cells)
                    for (int c = 0; c < 4; c++) Near(network.Strength(core, cell, c), 1, "Lossless cycle amplified or failed to terminate");
                lab.Graph.Remove(first); lab.Graph.Remove(lab.Graph.Edges[lab.Graph.Edges.Count - 1]);
                network.Refresh(lab.Graph, lab.Cells);
                Near(network.Strength(core, near, 0), 0, "Isolated source still drove ring");
                lab.Select(core); lab.DeleteSelected(); network.Refresh(lab.Graph, lab.Cells);
                foreach (var cell in lab.Cells) Near(network.Strength(lab.PrimaryCore, cell, 0), 0, "Deleted source retained signal");
                if (Failure == null) Debug.Log("CELL_LAB_T06_SIGNAL_PASS: 0.9/0.81 attenuation, edge efficiency, route UI and swim locks, W/A/S/D individually, max-not-sum actuation, response off, circuit breaks, cache invalidation, strongest ring path, lossless cycle termination and source deletion.");
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
