using System.Collections.Generic;
using Emerge.Cells;
using Emerge.Core;
using UnityEngine;

namespace Emerge.World
{
    public sealed class NutrientWorld : MonoBehaviour
    {
        public const int Capacity = 40;
        private CellLabController lab;
        private NutrientParticle prefab;
        private Transform root;
        private readonly List<NutrientParticle> particles = new List<NutrientParticle>();
        public IReadOnlyList<NutrientParticle> Particles => particles;
        public void Initialize(CellLabController controller, NutrientParticle template)
        {
            lab = controller; prefab = template;
            if (prefab == null) throw new System.InvalidOperationException("缺少营养颗粒预制体。");
            root = new GameObject("营养颗粒").transform; root.SetParent(transform, false);
        }
        public NutrientParticle Spawn(Vector2 position, float amount = 0.4f)
        {
            if (particles.Count >= Capacity || amount <= 0) return null;
            var particle = Instantiate(prefab, position, Quaternion.identity, root);
            particle.Initialize(amount); particles.Add(particle); return particle;
        }
        public void Clear()
        {
            foreach (var particle in particles) { particle.gameObject.SetActive(false); Destroy(particle.gameObject); }
            particles.Clear();
        }
        public void SeedPatch()
        {
            var connected = lab.Graph.Component(lab.PrimaryCore);
            CellView collector = null;
            foreach (var cell in lab.Cells)
                if (connected.Contains(cell) && (collector == null || cell.Definition.absorptionRate > collector.Definition.absorptionRate)) collector = cell;
            if (collector == null) return;
            Vector2 center = lab.IsEditing ? (Vector2)collector.transform.position : collector.Body.worldCenterOfMass;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2 / 16;
                Spawn(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (collector.Definition.radius + 0.07f));
            }
            lab.SetMessage("已投放营养；游动模式接触吸收。编辑模式暂停摄食与代谢。");
        }
        public void Capture(float dt, HashSet<CellView> connected)
        {
            foreach (var cell in connected)
            {
                float budget = cell.Definition.absorptionRate * dt;
                foreach (var particle in particles)
                {
                    if (budget <= 0 || lab.Metabolism.Nutrients >= lab.Metabolism.Settings.nutrientCapacity) break;
                    if (particle.Remaining <= 0 || Vector2.Distance(cell.Body.worldCenterOfMass, particle.transform.position) > cell.Definition.radius + NutrientParticle.Radius) continue;
                    float amount = Mathf.Min(budget, particle.Remaining, lab.Metabolism.Settings.nutrientCapacity - lab.Metabolism.Nutrients);
                    float eaten = particle.Consume(amount); lab.Metabolism.Receive(eaten); budget -= eaten;
                }
            }
            for (int i = particles.Count - 1; i >= 0; i--)
                if (particles[i].Remaining <= 0.000001f) { Destroy(particles[i].gameObject); particles.RemoveAt(i); }
        }
    }
}
