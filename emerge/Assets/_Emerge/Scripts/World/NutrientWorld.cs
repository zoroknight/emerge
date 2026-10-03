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
        private LocalFlowSettings flow;
        private Camera worldCamera;
        public LocalFlowSettings FlowSettings => flow;
        private Transform root;
        private readonly List<NutrientParticle> particles = new List<NutrientParticle>();
        private readonly List<SceneFlowRegion> regions = new List<SceneFlowRegion>();
        private readonly List<SceneNutrientPatch> patches = new List<SceneNutrientPatch>();
        public IReadOnlyList<SceneFlowRegion> FlowRegions => regions;
        public IReadOnlyList<NutrientParticle> Particles => particles;
        public float TransportedIngested { get; private set; }
        public int MembraneContacts { get; private set; }
        public float CapturedTotal { get; private set; }
        public int StoredCount(CellView cell)
        { int count = 0; foreach (var p in particles) if (p.IsCaptured && p.Captor == cell) count++; return count; }
        public float StoredFood(CellView cell)
        { float amount = 0; foreach (var p in particles) if (p.IsCaptured && p.Captor == cell) amount += p.Remaining; return amount; }
        public void ReleaseFrom(CellView cell)
        { foreach (var p in particles) if (p.IsCaptured && p.Captor == cell) { p.FollowCaptor(); p.Release(); } }
        private int FreeSlot(CellView cell)
        {
            for (int slot = 0; slot < cell.Definition.foodSlots; slot++)
            {
                bool occupied = false;
                foreach (var p in particles) if (p.IsCaptured && p.Captor == cell && p.CaptureSlot == slot) { occupied = true; break; }
                if (!occupied) return slot;
            }
            return -1;
        }
        public void Initialize(CellLabController controller, NutrientParticle template, LocalFlowSettings settings, Camera camera)
        {
            lab = controller; prefab = template;
            flow = settings; worldCamera = camera;
            if (flow == null) throw new System.InvalidOperationException("缺少局部流场配置。");
            if (prefab == null) throw new System.InvalidOperationException("缺少营养颗粒预制体。");
            root = new GameObject("营养颗粒").transform; root.SetParent(transform, false);
            foreach (var region in FindObjectsByType<SceneFlowRegion>()) RegisterFlow(region);
            foreach (var patch in FindObjectsByType<SceneNutrientPatch>()) RegisterPatch(patch, false);
        }
        public void RegisterFlow(SceneFlowRegion region) { region.BindWorld(this); if (!regions.Contains(region)) regions.Add(region); }
        public void UnregisterFlow(SceneFlowRegion region) => regions.Remove(region);
        public void RegisterPatch(SceneNutrientPatch patch, bool populate = true)
        {
            if (patches.Contains(patch)) return;
            patch.BindWorld(this);
            patches.Add(patch);
            if (populate && root != null && (lab.Experiments == null || !lab.Experiments.IsActive)) patch.PopulateOnce(this);
        }
        public void UnregisterPatch(SceneNutrientPatch patch) => patches.Remove(patch);
        public void RestoreSceneNutrients()
        { if (lab.Experiments != null && lab.Experiments.IsActive) return; foreach (var patch in patches) if (patch != null && patch.isActiveAndEnabled) patch.Populate(this); }
        public Vector2 AmbientVelocityAt(Vector2 point)
        {
            bool experiment = lab.Experiments != null && lab.Experiments.IsActive;
            Vector2 velocity = experiment ? lab.Experiments.Active.ambient : flow.ambientVelocity;
            foreach (var region in regions) if (region != null && (!experiment || region.IsExperimentSource)) velocity += region.VelocityAt(point);
            return velocity;
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
            TransportedIngested = 0;
            MembraneContacts = 0;
            CapturedTotal = 0;
        }
        public Vector2 FlowVelocityAt(Vector2 point, bool preview = false, float previewSupply = 1)
        {
            Vector2 velocity = AmbientVelocityAt(point);
            foreach (var cell in lab.Cells)
            {
                if (cell.Definition.kind != CellKind.Cilia) continue;
                float activation = preview ? lab.Physics.ActivationFor(cell) * previewSupply : cell.Activation;
                if (activation <= 0) continue;
                Vector2 center = preview ? (Vector2)cell.transform.position : cell.Body.worldCenterOfMass;
                float distanceSquared = (point - center).sqrMagnitude;
                float radiusSquared = flow.influenceRadius * flow.influenceRadius;
                if (distanceSquared >= radiusSquared) continue;
                float angle = (preview ? cell.transform.eulerAngles.z : cell.Body.rotation) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                velocity += direction * flow.ciliaSpeed * activation * (1 - distanceSquared / radiusSquared);
            }
            return MembraneTransport.SurfaceVelocity(lab.Cells, point, Vector2.ClampMagnitude(velocity, flow.maximumSpeed), preview);
        }
        public void Advect(float dt)
        {
            if (lab.IsEditing || dt <= 0) return;
            Vector2 min = worldCamera.ViewportToWorldPoint(new Vector3(0, 0.08f, -worldCamera.transform.position.z));
            Vector2 max = worldCamera.ViewportToWorldPoint(new Vector3(1, CellLabController.ArenaTop, -worldCamera.transform.position.z));
            foreach (var particle in particles)
            {
                if (particle.IsCaptured) { particle.FollowCaptor(); continue; }
                Vector2 previous = particle.transform.position;
                Vector2 velocity = FlowVelocityAt(previous);
                Vector2 next = MembraneTransport.Move(lab.Cells, previous, velocity * dt, out int contacts);
                MembraneContacts += contacts;
                next.x = Mathf.Clamp(next.x, min.x + NutrientParticle.Radius, max.x - NutrientParticle.Radius);
                next.y = Mathf.Clamp(next.y, min.y + NutrientParticle.Radius, max.y - NutrientParticle.Radius);
                particle.MoveTo(next, (next - previous) / dt);
            }
            if (flow.ambientBodyDrag > 0)
                foreach (var cell in lab.Cells)
                    cell.Body.AddForce((Vector2.ClampMagnitude(AmbientVelocityAt(cell.Body.worldCenterOfMass), flow.maximumSpeed) - cell.Body.linearVelocity) * flow.ambientBodyDrag);
        }
        public void SeedFilterPatch()
        {
            var connected = lab.Graph.Component(lab.PrimaryCore);
            CellView collector = null;
            foreach (var cell in lab.Cells)
                if (connected.Contains(cell) && cell.Definition.kind == CellKind.Absorber) { collector = cell; break; }
            if (collector == null) { lab.SetMessage("滤食投放需要与核心连通的吸收细胞。"); return; }
            Vector2 center = lab.IsEditing ? (Vector2)collector.transform.position : collector.Body.worldCenterOfMass;
            float angle = (lab.IsEditing ? collector.transform.eulerAngles.z : collector.Body.rotation) * Mathf.Deg2Rad;
            Vector2 up = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)), right = new Vector2(up.y, -up.x);
            for (int sign = -1; sign <= 1; sign += 2)
                for (int row = 0; row < 4; row++)
                    for (int column = 0; column < 5; column++)
                        Spawn(center + up * sign * (1.9f + row * 0.22f) + right * (column - 2) * 0.16f);
            lab.SetMessage("已在吸收区外投放营养；W 开启滤食，松键对照，6 切换流场显示。颗粒不自动补充。");
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
            foreach (var p in particles)
                if (p.IsCaptured)
                {
                    if (p.Captor == null || !p.Captor.gameObject.activeInHierarchy) p.Release();
                    else { p.Age(dt); p.FollowCaptor(); }
                }
            // A dedicated absorber gets first claim; the core fallback cannot nibble its new catch.
            for (int phase = 0; phase < 2; phase++)
            foreach (var cell in connected)
            {
                if ((cell.Definition.kind == CellKind.Absorber) != (phase == 0)) continue;
                float budget = cell.Definition.absorptionRate * dt;
                if (cell.Definition.kind == CellKind.Absorber)
                {
                    // Existing stored particles digest before any new capture: no same-tick reward.
                    int ready = 0;
                    foreach (var p in particles)
                        if (p.IsCaptured && p.Captor == cell && p.Remaining > 0 && p.DigestionAge >= cell.Definition.digestionDelay) ready++;
                    float share = ready > 0 ? budget / ready : 0;
                    foreach (var p in particles)
                    {
                        if (!p.IsCaptured || p.Captor != cell || p.DigestionAge < cell.Definition.digestionDelay || budget <= 0) continue;
                        float amount = Mathf.Min(share, p.Remaining, lab.Metabolism.Settings.nutrientCapacity - lab.Metabolism.Nutrients);
                        float eaten = p.Consume(amount); lab.Metabolism.Receive(eaten); budget -= eaten;
                        if (p.TravelDistance > 0.1f) TransportedIngested += eaten;
                    }
                    int slots = StoredCount(cell); float stored = StoredFood(cell);
                    foreach (var p in particles)
                    {
                        if (p.IsCaptured || p.Remaining <= 0 || slots >= cell.Definition.foodSlots || stored + p.Remaining > cell.Definition.foodCapacity + 0.00001f) continue;
                        if (Vector2.Distance(cell.Body.worldCenterOfMass, p.transform.position) > cell.Definition.radius + NutrientParticle.Radius) continue;
                        p.Capture(cell, FreeSlot(cell)); slots++; stored += p.Remaining; CapturedTotal += p.Remaining;
                    }
                    continue;
                }
                foreach (var particle in particles)
                {
                    if (budget <= 0 || lab.Metabolism.Nutrients >= lab.Metabolism.Settings.nutrientCapacity) break;
                    if (particle.IsCaptured || particle.Remaining <= 0 || Vector2.Distance(cell.Body.worldCenterOfMass, particle.transform.position) > cell.Definition.radius + NutrientParticle.Radius) continue;
                    float amount = Mathf.Min(budget, particle.Remaining, lab.Metabolism.Settings.nutrientCapacity - lab.Metabolism.Nutrients);
                    float eaten = particle.Consume(amount); lab.Metabolism.Receive(eaten); budget -= eaten;
                    if (particle.TravelDistance > 0.1f) TransportedIngested += eaten;
                }
            }
            for (int i = particles.Count - 1; i >= 0; i--)
                if (particles[i].Remaining <= 0.000001f) { Destroy(particles[i].gameObject); particles.RemoveAt(i); }
        }
    }
}
