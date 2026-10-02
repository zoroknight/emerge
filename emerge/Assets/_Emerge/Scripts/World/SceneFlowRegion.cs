using UnityEngine;

namespace Emerge.World
{
    [AddComponentMenu("Emerge/场景/局部环境流场")]
    public sealed class SceneFlowRegion : MonoBehaviour
    {
        [Tooltip("影响半径，单位为世界单位；旋转对象改变流向。"), Min(0.1f)] public float radius = 2.4f;
        [Tooltip("沿对象局部 +X 的水流速度。"), Min(0)] public float speed = 0.6f;
        [Tooltip("勾选时水流由中心向边缘衰减；取消则范围内均匀流动。")]
        public bool fadeAtEdge = true;
        private NutrientWorld world;
        internal void BindWorld(NutrientWorld target) => world = target;
        public Vector2 VelocityAt(Vector2 point)
        {
            float distance = (point - (Vector2)transform.position).sqrMagnitude;
            float squaredRadius = Mathf.Max(0.01f, radius * radius);
            if (!isActiveAndEnabled || distance >= squaredRadius) return Vector2.zero;
            float weight = fadeAtEdge ? 1 - distance / squaredRadius : 1;
            return (Vector2)transform.right * speed * weight;
        }
        private void OnEnable()
        { world = FindAnyObjectByType<NutrientWorld>(); if (world != null) world.RegisterFlow(this); }
        private void OnDisable() { if (world != null) world.UnregisterFlow(this); }
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.8f, 1, 0.7f);
            Gizmos.DrawWireSphere(transform.position, radius);
            Vector3 tip = transform.position + transform.right * Mathf.Min(radius, Mathf.Max(0.3f, speed));
            Gizmos.DrawLine(transform.position, tip);
            Gizmos.DrawLine(tip, tip - transform.right * 0.2f + transform.up * 0.15f);
            Gizmos.DrawLine(tip, tip - transform.right * 0.2f - transform.up * 0.15f);
        }
    }
}
