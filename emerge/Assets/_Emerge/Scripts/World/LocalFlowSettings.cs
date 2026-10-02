using UnityEngine;

namespace Emerge.World
{
    [CreateAssetMenu(menuName = "Emerge/Local Flow Settings")]
    public sealed class LocalFlowSettings : ScriptableObject
    {
        [Min(0.1f)] public float influenceRadius = 2.6f;
        [Min(0)] public float ciliaSpeed = 2.6f;
        [Min(0.1f)] public float maximumSpeed = 4;
        public Vector2 ambientVelocity;
        [Min(0)] public float ambientBodyDrag;
    }
}
