using UnityEngine;

namespace Emerge.Core
{
    public sealed class MetabolismState
    {
        public MetabolismSettings Settings { get; }
        public float Nutrients { get; private set; }
        public float Energy { get; private set; }
        public float SupplyRatio { get; private set; } = 1;
        public float RequestedRate { get; private set; }
        public float Ingested { get; private set; }
        public float Metabolized { get; private set; }
        public float Produced { get; private set; }
        public float MaintenanceSpent { get; private set; }
        public float ActivitySpent { get; private set; }
        public float ProductionRate { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Only the physical stress benchmark bypasses resources; normal gameplay never toggles this.
        public bool DebugUnlimited { get; set; }
#endif
        public MetabolismState(MetabolismSettings settings)
        { Settings = settings ?? throw new System.InvalidOperationException("缺少代谢配置。"); Reset(); }
        public void Reset() => Reset(Settings.initialNutrients, Settings.initialEnergy);
        public void Reset(float nutrients, float energy)
        {
            Nutrients = Mathf.Clamp(nutrients, 0, Settings.nutrientCapacity);
            Energy = Mathf.Clamp(energy, 0, Settings.energyCapacity);
            SupplyRatio = 1; RequestedRate = Ingested = Metabolized = Produced = MaintenanceSpent = ActivitySpent = ProductionRate = 0;
        }
        public float Receive(float amount)
        {
            float received = Mathf.Clamp(amount, 0, Mathf.Max(0, Settings.nutrientCapacity - Nutrients));
            Nutrients += received; Ingested += received; return received;
        }
        public float PreviewSupply(float requestedRate, int cells, float dt)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugUnlimited) return 1;
#endif
            if (requestedRate <= 0 || dt <= 0) return 1;
            float conversion = Conversion(dt);
            float available = Mathf.Max(0, Energy + conversion * Settings.energyYield - cells * Settings.maintenancePerCell * dt);
            return Ratio(available, requestedRate, dt);
        }
        private float Ratio(float available, float rate, float dt) => rate > 0 ? Mathf.Clamp01(available / (rate * Mathf.Max(dt, Settings.supplyWindow))) : 1;
        private float Conversion(float dt) => Settings.energyYield > 0 ? Mathf.Min(Nutrients, Settings.metabolismRate * dt,
            Mathf.Max(0, Settings.energyCapacity - Energy) / Settings.energyYield) : 0;

        public void Step(float dt, float requestedRate, int cells)
        {
            RequestedRate = Mathf.Max(0, requestedRate);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugUnlimited) { SupplyRatio = 1; ProductionRate = 0; return; }
#endif
            float converted = Conversion(dt);
            Nutrients = Mathf.Max(0, Nutrients - converted); Metabolized += converted;
            float generated = converted * Settings.energyYield;
            Energy = Mathf.Min(Settings.energyCapacity, Energy + generated); Produced += generated;
            ProductionRate = dt > 0 ? generated / dt : 0;
            float maintenance = Mathf.Min(Energy, Mathf.Max(0, cells * Settings.maintenancePerCell * dt));
            Energy -= maintenance; MaintenanceSpent += maintenance;
            float demand = RequestedRate * dt;
            SupplyRatio = Ratio(Energy, RequestedRate, dt);
            float allocated = Mathf.Min(Energy, demand * SupplyRatio);
            Energy = Mathf.Max(0, Energy - allocated); ActivitySpent += allocated;
        }
    }
}
