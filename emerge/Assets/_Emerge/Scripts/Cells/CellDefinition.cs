using UnityEngine;

namespace Emerge.Cells
{
    public enum CellKind { Core, Cilia, Absorber, Membrane, Contractor }
    public enum IntentChannel { W, A, S, D }

    [CreateAssetMenu(menuName = "Emerge/Cell Definition")]
    public sealed class CellDefinition : ScriptableObject
    {
        public CellKind kind;
        public string displayName;
        [Min(0.1f)] public float radius = 0.7f;
        public Color bodyColor = Color.cyan;
        [Min(1)] public int maxConnections = 4;
        [Min(0.1f)] public float mass = 1f;
        [Min(0)] public float thrust = 3.5f;
        [Range(0, 1)] public float signalRetention = 0.9f;
        [Min(0)] public float absorptionRate;
        [Min(0.4f)] public float foodCapacity = 8;
        [Range(1, 32)] public int foodSlots = 16;
        [Min(0.02f)] public float digestionDelay = 0.3f;
        [Range(0.4f, 1)] public float contractedSize = 0.55f;
        [Min(0.1f)] public float contractionSpeed = 1;
    }
}
