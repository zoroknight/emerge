using UnityEngine;

namespace Emerge.Cells
{
    public enum CellKind { Core, Cilia }

    [CreateAssetMenu(menuName = "Emerge/Cell Definition")]
    public sealed class CellDefinition : ScriptableObject
    {
        public CellKind kind;
        public string displayName;
        [Min(0.1f)] public float radius = 0.7f;
        public Color bodyColor = Color.cyan;
    }
}
