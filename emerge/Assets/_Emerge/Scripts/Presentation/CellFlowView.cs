using System.Collections.Generic;
using Emerge.Cells;
using Emerge.Core;
using UnityEngine;

namespace Emerge.Presentation
{
    public sealed class CellFlowView : MonoBehaviour
    {
        private CellLabController lab;
        private Camera worldCamera;
        private Mesh mesh;
        private MeshRenderer view;
        private readonly List<Vector3> vertices = new List<Vector3>(8192);
        private readonly List<Color> colors = new List<Color>(8192);
        private readonly List<Vector2> uvs = new List<Vector2>(8192);
        private readonly List<int> triangles = new List<int>(12288);
        public int VisibleLocalSamples { get; private set; }
        public int VisibleSamples { get; private set; }
        public void Initialize(CellLabController controller, Camera sceneCamera, Material material)
        {
            lab = controller; worldCamera = sceneCamera;
            mesh = new Mesh { name = "水流方向与流动标记" }; mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            view = gameObject.AddComponent<MeshRenderer>(); view.sharedMaterial = material; view.sortingOrder = -2;
        }
        private void LateUpdate()
        {
            if (lab == null) return;
            VisibleLocalSamples = VisibleSamples = 0; view.enabled = lab.ShowFlow;
            if (!lab.ShowFlow) return;
            vertices.Clear(); colors.Clear(); uvs.Clear(); triangles.Clear();
            float supply = lab.IsEditing ? lab.Physics.PreviewSupply() : 1;
            for (int i = 0; i < 48; i++)
            {
                Vector2 point = worldCamera.ViewportToWorldPoint(new Vector3((i % 8 + 0.5f) / 8,
                    0.08f + (i / 8 + 0.5f) / 6 * (CellLabController.ArenaTop - 0.08f), -worldCamera.transform.position.z));
                Sample(point, supply, false);
            }
            foreach (var cell in lab.Cells)
            {
                if (cell.Definition.kind != CellKind.Cilia) continue;
                float activation = lab.IsEditing ? lab.Physics.ActivationFor(cell) * supply : cell.Activation;
                if (activation <= 0.001f) continue;
                Vector2 center = lab.IsEditing ? (Vector2)cell.transform.position : cell.Body.worldCenterOfMass;
                Ring(center, lab.Food.FlowSettings.influenceRadius * 0.4f, supply, true);
                Ring(center, lab.Food.FlowSettings.influenceRadius * 0.75f, supply, true);
            }
            foreach (var region in lab.Food.FlowRegions)
            {
                if (region == null || !region.isActiveAndEnabled) continue;
                Ring(region.transform.position, region.radius * 0.4f, supply, false);
                Ring(region.transform.position, region.radius * 0.75f, supply, false);
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }
        private void Ring(Vector2 center, float radius, float supply, bool local)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                Sample(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, supply, local);
            }
        }
        private void Sample(Vector2 point, float supply, bool local)
        {
            Vector2 velocity = lab.Food.FlowVelocityAt(point, lab.IsEditing, supply);
            if (velocity.sqrMagnitude <= 0.0025f) return;
            if (local) VisibleLocalSamples++;
            VisibleSamples++;
            Vector2 direction = velocity.normalized;
            float length = Mathf.Min(0.55f, velocity.magnitude * 0.24f);
            float head = Mathf.Min(0.13f, length * 0.4f);
            Vector2 side = new Vector2(-direction.y, direction.x), tip = point + direction * length;
            Color color = local ? new Color(0.3f, 0.86f, 1, 0.75f) : new Color(0.25f, 0.65f, 0.75f, 0.45f);
            Segment(point, tip, 0.025f, color);
            Segment(tip, tip - direction * head + side * head * 0.6f, 0.025f, color);
            Segment(tip, tip - direction * head - side * head * 0.6f, 0.025f, color);
            float phase = Mathf.Repeat(Time.time * Mathf.Min(3, velocity.magnitude) + point.x * 0.27f + point.y * 0.31f, 1);
            Vector2 mark = point + direction * length * phase;
            Segment(mark - direction * 0.025f, mark + direction * 0.025f, 0.045f, new Color(0.6f, 0.95f, 1, 0.9f));
        }
        private void Segment(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 side = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
            int start = vertices.Count;
            Vertex(a - side, color); Vertex(a + side, color); Vertex(b + side, color); Vertex(b - side, color);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }
        private void Vertex(Vector2 point, Color color)
        { vertices.Add(transform.InverseTransformPoint(point)); colors.Add(color); uvs.Add(new Vector2(0.5f, 0.5f)); }
        private void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
