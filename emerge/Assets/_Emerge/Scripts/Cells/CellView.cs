using UnityEngine;

namespace Emerge.Cells
{
    public sealed class CellView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private GameObject selection;
        [SerializeField] private CellDefinition definition;
        [SerializeField] private bool applyDefinitionTint = true;

        public CellDefinition Definition => definition;
        public Rigidbody2D Body { get; private set; }
        public Vector2 LastForce { get; private set; }
        public float Activation { get; private set; }
        public Vector2 FluidDirection => transform.right;

        public void Initialize(CellDefinition data)
        {
            definition = data;
            if (applyDefinitionTint) body.color = data.kind == CellKind.Membrane ?
                new Color(data.bodyColor.r, data.bodyColor.g, data.bodyColor.b, 0.3f) : data.bodyColor;
            transform.localScale = Vector3.one * data.radius * 2f;
            Body = GetComponent<Rigidbody2D>();
            if (Body == null) Body = gameObject.AddComponent<Rigidbody2D>();
            Body.simulated = false;
            Body.bodyType = RigidbodyType2D.Dynamic;
            Body.gravityScale = 0;
            Body.mass = data.mass;
            Body.linearDamping = 1.2f;
            Body.angularDamping = 2f;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var collider = GetComponent<CircleCollider2D>();
            if (collider == null) collider = gameObject.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            collider.offset = Vector2.zero; collider.isTrigger = false;
            SetSelected(false);
        }


        public void ApplyThrust(float activation)
        {
            Activation = Mathf.Clamp01(activation);
            LastForce = ForceFor(Activation);
            if (Body.simulated && LastForce.sqrMagnitude > 0) Body.AddForceAtPosition(LastForce, Body.worldCenterOfMass, ForceMode2D.Force);
        }

        public Vector2 ForceFor(float activation)
        {
            float radians = Body.rotation * Mathf.Deg2Rad;
            Vector2 direction = Body.simulated ? new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) : FluidDirection;
            return definition.kind == CellKind.Cilia ? -direction * definition.thrust * Mathf.Clamp01(activation) : Vector2.zero;
        }

        public void SetSimulation(bool enabled)
        {
            LastForce = Vector2.zero; Activation = 0;
            // Preserve the authoritative pose on pause; the rendered interpolation may lag a tick.
            Vector2 position = Body.simulated ? Body.position : (Vector2)transform.position;
            float rotation = Body.simulated ? Body.rotation : transform.eulerAngles.z;
            Body.linearVelocity = Vector2.zero; Body.angularVelocity = 0;
            Body.simulated = false;
            transform.SetPositionAndRotation(new Vector3(position.x, position.y, 0), Quaternion.Euler(0, 0, rotation));
            Body.position = position; Body.rotation = rotation;
            Body.simulated = enabled;
        }

        public void SetSelected(bool value) => selection.SetActive(value);
    }
}
