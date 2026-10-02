using System.Collections.Generic;
using Emerge.Cells;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Emerge.Core
{
    public sealed class CellLabPhysics : MonoBehaviour
    {
        private CellLabController lab;
        private InputActionAsset controls;
        private readonly InputAction[] intents = new InputAction[4];
        private readonly float[] strengths = new float[4];
        private readonly List<FixedJoint2D> joints = new List<FixedJoint2D>();
        public int JointCount => joints.Count;
        public int ActiveMask { get; private set; }
        // Rigid joints avoid storing large elastic deformations in long chains under wall / flow loads.
        public const float JointFrequency = 0f;
        public IReadOnlyList<FixedJoint2D> ActiveJoints => joints;
        private Camera arenaCamera;
        private Material arenaMaterial;
        private GameObject arena;

        public void Initialize(CellLabController controller, InputActionAsset template, Material material, Camera camera)
        {
            lab = controller;
            if (template == null) throw new System.InvalidOperationException("缺少四通道输入资源。");
            controls = Instantiate(template);
            string[] names = { "Gameplay/IntentW", "Gameplay/IntentA", "Gameplay/IntentS", "Gameplay/IntentD" };
            for (int i = 0; i < intents.Length; i++)
            {
                intents[i] = controls.FindAction(names[i], true);
                intents[i].wantsInitialStateCheck = true;
                intents[i].performed += OnIntent;
                intents[i].canceled += OnIntent;
            }
            controls.Enable();
            arenaCamera = camera; arenaMaterial = material;
            RebuildArena();
        }

        private void OnIntent(InputAction.CallbackContext context)
        {
            for (int i = 0; i < intents.Length; i++)
                if (context.action == intents[i]) strengths[i] = context.canceled ? 0 : Mathf.Clamp01(context.ReadValue<float>());
            ActiveMask = 0;
            for (int i = 0; i < strengths.Length; i++) if (strengths[i] > 0) ActiveMask |= 1 << i;
        }

        public void StartSimulation()
        {
            StopSimulation();
            foreach (var cell in lab.Cells) cell.SetSimulation(true);
            Physics2D.SyncTransforms();
            foreach (var edge in lab.Graph.Edges)
            {
                Vector3 point = Vector3.Lerp(edge.A.transform.position, edge.B.transform.position,
                    edge.A.Definition.radius / (edge.A.Definition.radius + edge.B.Definition.radius));
                var joint = edge.A.gameObject.AddComponent<FixedJoint2D>();
                joint.autoConfigureConnectedAnchor = false;
                joint.connectedBody = edge.B.Body;
                joint.anchor = edge.A.transform.InverseTransformPoint(point);
                joint.connectedAnchor = edge.B.transform.InverseTransformPoint(point);
                joint.frequency = JointFrequency; joint.dampingRatio = 1f;
                joint.enableCollision = false;
                joints.Add(joint);
            }
        }

        public void StopSimulation()
        {
            foreach (var joint in joints)
            {
                if (joint == null) continue;
                joint.enabled = false; Destroy(joint);
            }
            joints.Clear();
            if (lab != null) foreach (var cell in lab.Cells) if (cell != null && cell.Body != null) cell.SetSimulation(false);
        }

        private void FixedUpdate()
        {
            if (Physics2D.simulationMode == SimulationMode2D.FixedUpdate) StepActuators();
        }

        public void StepActuators()
        {
            if (lab == null || lab.IsEditing) return;
            var controlled = lab.Graph.Component(lab.PrimaryCore);
            foreach (var cell in lab.Cells)
            {
                float intensity = cell.Definition.kind == CellKind.Cilia && controlled.Contains(cell) ? strengths[(int)cell.Channel] : 0;
                cell.ApplyThrust(intensity);
            }
        }

        private void OnEnable()
        {
            if (controls == null) return;
            controls.Enable();
            if (lab != null && !lab.IsEditing) StartSimulation();
        }

        private void OnDisable()
        {
            if (controls != null) controls.Disable();
            System.Array.Clear(strengths, 0, strengths.Length); ActiveMask = 0;
            StopSimulation();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (controls == null) return;
            if (focused) controls.Enable();
            else { controls.Disable(); System.Array.Clear(strengths, 0, strengths.Length); ActiveMask = 0; }
        }

        private void OnDestroy()
        {
            if (controls == null) return;
            foreach (var intent in intents) { intent.performed -= OnIntent; intent.canceled -= OnIntent; }
            Destroy(controls);
        }

        public void RebuildArena()
        {
            if (arena != null) { arena.SetActive(false); Destroy(arena); }
            Vector3 min = arenaCamera.ViewportToWorldPoint(new Vector3(0, 0.08f, -arenaCamera.transform.position.z));
            Vector3 max = arenaCamera.ViewportToWorldPoint(new Vector3(1, 0.76f, -arenaCamera.transform.position.z));
            Vector2 center = (min + max) / 2;
            Vector2 size = max - min;
            arena = new GameObject("实验室边界"); arena.transform.SetParent(transform, false);
            Wall(arena.transform, new Vector2(min.x - 0.15f, center.y), new Vector2(0.3f, size.y + 0.6f));
            Wall(arena.transform, new Vector2(max.x + 0.15f, center.y), new Vector2(0.3f, size.y + 0.6f));
            Wall(arena.transform, new Vector2(center.x, min.y - 0.15f), new Vector2(size.x, 0.3f));
            Wall(arena.transform, new Vector2(center.x, max.y + 0.15f), new Vector2(size.x, 0.3f));
            var line = arena.AddComponent<LineRenderer>();
            line.sharedMaterial = arenaMaterial; line.positionCount = 5; line.useWorldSpace = true;
            line.startWidth = line.endWidth = 0.025f; line.sortingOrder = -3;
            line.startColor = line.endColor = new Color(0.18f, 0.4f, 0.44f);
            line.SetPositions(new[] { min, new Vector3(max.x, min.y, 0), max, new Vector3(min.x, max.y, 0), min });
        }

        private static void Wall(Transform parent, Vector2 position, Vector2 size)
        {
            var obj = new GameObject("边界碰撞体"); obj.layer = LayerMask.NameToLayer("Terrain");
            obj.transform.SetParent(parent, false); obj.transform.position = position;
            obj.AddComponent<BoxCollider2D>().size = size;
        }
    }
}
