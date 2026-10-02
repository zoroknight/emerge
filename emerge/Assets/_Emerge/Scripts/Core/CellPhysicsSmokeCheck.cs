#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using Emerge.Cells;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Emerge.Core
{
    public static class CellPhysicsSmokeCheck
    {
        public static string Failure { get; private set; }
        private static void Require(bool value, string message) { if (!value && Failure == null) Failure = "T04: " + message; }

        private static void Pair(CellLabController lab, Vector3 offset, float angle, bool connected = true)
        {
            if (!lab.IsEditing) lab.ToggleMode();
            lab.ResetLab();
            lab.Cells[0].transform.position = Vector3.zero;
            lab.Cells[1].transform.position = offset;
            lab.Cells[1].transform.rotation = Quaternion.Euler(0, 0, angle);
            if (connected) Require(lab.Connect(lab.Cells[0], lab.Cells[1]), "Test pair failed to connect.");
            lab.Select(lab.Cells[1]);
        }

        private static IEnumerator Steps(int count)
        { for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate(); }

        public static IEnumerator Run(CellLabController lab)
        {
            Failure = null;
            var previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            lab.Physics.enabled = false; lab.Physics.enabled = true;
            float straight = 0, turn = 0, vertical = 0;
            try
            {
                Pair(lab, new Vector3(1.3f, 0, 0), 180);
                lab.ToggleMode();
                Require(lab.Physics.JointCount == 1 && lab.Cells[0].Body.simulated && lab.Cells[1].Body.simulated, "Bodies or joint missing.");
                yield return Steps(10);
                Require(lab.Cells[0].Body.position.magnitude < 0.01f && lab.Cells[0].Body.linearVelocity.magnitude < 0.01f, "No-input body gained an impulse.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return null; yield return Steps(20);
                straight = lab.Cells[0].Body.position.x;
                Require(straight > 0.04f && lab.Cells[1].Body.position.x > 1.34f, "W did not physically propel the connected core.");
                Require(lab.Cells[1].LastForce.x > 3f && Mathf.Abs(lab.Cells[1].LastForce.y) < 0.1f, "Reaction force direction incorrect.");
                Require(Mathf.Abs(Mathf.DeltaAngle(lab.Cells[0].Body.rotation, 0)) < 3, "Aligned pair produced unexpected torque.");
                float speed = lab.Cells[0].Body.linearVelocity.magnitude;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return Steps(15);
                Require(lab.Cells[1].LastForce == Vector2.zero && lab.Cells[0].Body.linearVelocity.magnitude < speed, "Release did not stop thrust / damp inertia.");
                Vector2 beforePause = lab.Cells[0].Body.position;
                lab.ToggleMode();
                Require(Vector2.Distance(lab.Cells[0].Body.position, beforePause) < 0.001f, "Pausing rewound the interpolated pose.");
                Require(lab.Physics.JointCount == 0 && !lab.Cells[0].Body.simulated && lab.Cells[0].Body.linearVelocity == Vector2.zero && lab.Cells[0].Body.angularVelocity == 0,
                    "Editing did not freeze and clear physics.");
                if (Failure != null) yield break;

                Pair(lab, new Vector3(0, 1.3f, 0), 180); lab.ToggleMode();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return null; yield return Steps(25);
                turn = Mathf.DeltaAngle(0, lab.Cells[0].Body.rotation);
                Require(turn < -2f && lab.Cells[0].Body.position.x > 0.03f, "Offset force failed to produce physical torque.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                if (Failure != null) yield break;

                Pair(lab, new Vector3(0, 1.3f, 0), 90); lab.ToggleMode();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return null; yield return Steps(20);
                vertical = lab.Cells[0].Body.position.y;
                Require(vertical < -0.04f && lab.Cells[1].LastForce.y < -3f, "Rotating cilia did not rotate actual force.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                if (Failure != null) yield break;

                Pair(lab, new Vector3(1.3f, 0, 0), 180);
                lab.SelectConnection(lab.Graph.Edges[0]); lab.SetCoreChannel(1);
                Require(lab.Graph.Edges[0].CoreChannel == IntentChannel.A, "Core exit assignment failed.");
                lab.ToggleMode(); lab.SetCoreChannel(0);
                Require(lab.Graph.Edges[0].CoreChannel == IntentChannel.A, "Swim allowed channel editing.");
                lab.Select(lab.Cells[1]);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return null; yield return Steps(15);
                Require(lab.Selected.Activation == 0 && lab.Cells[0].Body.position.magnitude < 0.01f, "Wrong channel activated cilia.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
                yield return null; yield return Steps(15);
                Require(lab.Cells[0].Body.position.x > 0.025f && Mathf.Abs(lab.Selected.Activation - lab.CiliaDefinition.signalRetention) < 0.001f, "Assigned channel A did not drive cilia.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.A, Key.S, Key.D));
                yield return null; yield return Steps(5);
                Require(lab.Physics.ActiveMask == 15 && Mathf.Abs(lab.Selected.LastForce.magnitude - lab.Selected.Definition.thrust * lab.CiliaDefinition.signalRetention) < 0.01f,
                    "Simultaneous input increased actuator strength.");
                lab.ToggleMode();
                Vector2 editedPosition = lab.Cells[0].Body.position;
                yield return Steps(5);
                Require(lab.Cells[0].Body.position == editedPosition && lab.Selected.LastForce == Vector2.zero, "Input moved frozen editor bodies.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                if (Failure != null) yield break;

                Pair(lab, new Vector3(3, 0, 0), 180, false); lab.ToggleMode();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return null; yield return Steps(15);
                Require(lab.Physics.JointCount == 0 && lab.Selected.Activation == 0 && lab.Selected.LastForce == Vector2.zero &&
                    Vector2.Distance(lab.Selected.Body.position, new Vector2(3, 0)) < 0.01f, "Detached cilia retained core-driven thrust.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                if (Failure != null) yield break;

                Pair(lab, new Vector3(1.3f, 0, 0), 180);
                for (int i = 0; i < 5; i++) { lab.ToggleMode(); lab.ToggleMode(); }
                yield return null;
                Require(lab.Physics.JointCount == 0 && Object.FindObjectsByType<FixedJoint2D>().Length == 0, "Mode switches left stale joints.");
                lab.ToggleMode(); yield return Steps(10);
                Require(lab.Physics.JointCount == 1 && Object.FindObjectsByType<FixedJoint2D>().Length == 1 && lab.Cells[0].Body.linearVelocity.magnitude < 0.01f,
                    "Mode switches duplicated joints or created impulses.");
                lab.ToggleMode();
                lab.Select(lab.Cells[1]); lab.DisconnectSelected(); lab.ToggleMode();
                yield return Steps(2);
                Require(lab.Physics.JointCount == 0, "Disconnected edge recreated a physical joint.");
                if (Failure == null) Debug.Log("CELL_LAB_T04_PHYSICS_PASS: straight_core_dx=" + straight.ToString("F3") +
                    "; offset_core_angle=" + turn.ToString("F2") + "; rotated_core_dy=" + vertical.ToString("F3") +
                    "; input actions W/A, simultaneous channels, release damping, disconnected control and mode cleanup passed.");
            }
            finally
            {
                if (!lab.IsEditing) lab.ToggleMode();
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = previousBackground;
                lab.ResetLab();
            }
        }
    }
}
#endif
