using UnityEngine;

namespace Emerge.World
{
    public sealed class NutrientParticle : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer visual;
        public const float Radius = 0.12f;
        public float Remaining { get; private set; }
        private float initial;
        private LineRenderer motion;
        public float TravelDistance { get; private set; }
        public void Initialize(float amount) { Remaining = initial = Mathf.Max(0, amount); transform.localScale = Vector3.one * Radius * 2; }
        public float Consume(float amount)
        {
            float eaten = Mathf.Min(Remaining, Mathf.Max(0, amount)); Remaining = Mathf.Max(0, Remaining - eaten);
            transform.localScale = Vector3.one * Radius * 2 * Mathf.Sqrt(initial > 0 ? Remaining / initial : 0);
            if (Remaining <= 0.000001f) gameObject.SetActive(false);
            return eaten;
        }
        public void MoveTo(Vector2 position, Vector2 velocity)
        {
            TravelDistance += Vector2.Distance(transform.position, position);
            transform.position = position;
            if (motion == null)
            {
                var obj = new GameObject("颗粒流速"); obj.transform.SetParent(transform, false);
                motion = obj.AddComponent<LineRenderer>(); motion.useWorldSpace = true; motion.positionCount = 2;
                motion.sharedMaterial = visual.sharedMaterial; motion.sortingOrder = 1;
                motion.startWidth = 0.015f; motion.endWidth = 0.04f;
                motion.startColor = motion.endColor = new Color(0.5f, 0.85f, 0.65f, 0.7f);
            }
            motion.enabled = velocity.sqrMagnitude > 0.0025f;
            motion.SetPosition(0, position - Vector2.ClampMagnitude(velocity * 0.16f, 0.4f));
            motion.SetPosition(1, position);
        }
    }
}
