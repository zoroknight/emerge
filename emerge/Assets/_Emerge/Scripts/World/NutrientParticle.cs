using UnityEngine;

namespace Emerge.World
{
    public sealed class NutrientParticle : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer visual;
        public const float Radius = 0.12f;
        public float Remaining { get; private set; }
        private float initial;
        public void Initialize(float amount) { Remaining = initial = Mathf.Max(0, amount); transform.localScale = Vector3.one * Radius * 2; }
        public float Consume(float amount)
        {
            float eaten = Mathf.Min(Remaining, Mathf.Max(0, amount)); Remaining = Mathf.Max(0, Remaining - eaten);
            transform.localScale = Vector3.one * Radius * 2 * Mathf.Sqrt(initial > 0 ? Remaining / initial : 0);
            if (Remaining <= 0.000001f) gameObject.SetActive(false);
            return eaten;
        }
    }
}
