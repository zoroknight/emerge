using UnityEngine;

namespace Emerge.Core
{
    [CreateAssetMenu(menuName = "Emerge/Metabolism Settings")]
    public sealed class MetabolismSettings : ScriptableObject
    {
        [Min(0.1f)] public float nutrientCapacity = 8, energyCapacity = 10;
        [Min(0)] public float initialNutrients = 2, initialEnergy = 8;
        [Min(0)] public float metabolismRate = 0.7f, energyYield = 4;
        [Min(0)] public float maintenancePerCell = 0.03f, ciliaCost = 1.5f;
        [Min(0)] public float contractionCost = 0.8f;
        [Min(0.02f)] public float supplyWindow = 0.35f;
    }
}
