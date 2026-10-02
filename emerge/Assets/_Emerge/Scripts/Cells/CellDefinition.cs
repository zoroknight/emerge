using UnityEngine;

namespace Emerge.Cells
{
    public enum CellKind { Core, Cilia }
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
    }
}
