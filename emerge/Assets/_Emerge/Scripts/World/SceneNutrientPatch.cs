using UnityEngine;

namespace Emerge.World
{
    [AddComponentMenu("Emerge/场景/营养放置区域")]
    public sealed class SceneNutrientPatch : MonoBehaviour
    {
        [Tooltip("初始颗粒数量，仍受实验室总上限 40 限制。"), Range(1, 40)] public int count = 8;
        [Tooltip("每颗营养含量。"), Min(0.01f)] public float amount = 0.4f;
        [Tooltip("区域宽高。单颗时生成在对象中心；区域支持旋转和缩放。")]
        public Vector2 size = new Vector2(1.6f, 1.2f);
        private NutrientWorld world;
        private NutrientWorld populatedWorld;
        internal void BindWorld(NutrientWorld target) => world = target;
        internal void PopulateOnce(NutrientWorld target) { if (populatedWorld != target) Populate(target); }
        public Vector2 PositionFor(int index)
        {
            if (count <= 1) return transform.position;
            int columns = Mathf.CeilToInt(Mathf.Sqrt(count)), rows = Mathf.CeilToInt(count / (float)columns);
            float x = columns > 1 ? (index % columns / (float)(columns - 1) - 0.5f) * size.x : 0;
            float y = rows > 1 ? (index / columns / (float)(rows - 1) - 0.5f) * size.y : 0;
            return transform.TransformPoint(new Vector3(x, y, 0));
        }
        public void Populate(NutrientWorld target)
        {
            if (!isActiveAndEnabled) return;
            populatedWorld = target;
            for (int i = 0; i < Mathf.Clamp(count, 1, NutrientWorld.Capacity); i++) target.Spawn(PositionFor(i), amount);
        }
        private void OnEnable()
        { world = FindAnyObjectByType<NutrientWorld>(); if (world != null) world.RegisterPatch(this); }
        private void OnDisable() { if (world != null) world.UnregisterPatch(this); }
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.8f, 1, 0.3f, 0.8f);
            for (int i = 0; i < Mathf.Clamp(count, 1, NutrientWorld.Capacity); i++) Gizmos.DrawWireSphere(PositionFor(i), 0.12f);
        }
    }
}
