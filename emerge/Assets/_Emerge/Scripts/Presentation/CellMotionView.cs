using System.Collections.Generic;
using Emerge.Cells;
using Emerge.Core;
using UnityEngine;

namespace Emerge.Presentation
{
    public sealed class CellMotionView : MonoBehaviour
    {
        private CellLabController lab;
        private Material material;
        private LineRenderer totalForce;
        private readonly Dictionary<CellView, LineRenderer[]> arrows = new Dictionary<CellView, LineRenderer[]>();
        public void Initialize(CellLabController controller, Material sharedMaterial) { lab = controller; material = sharedMaterial; }

        private void LateUpdate()
        {
            if (lab == null) return;
            var removed = new List<CellView>();
            foreach (var entry in arrows)
                if (entry.Key == null || !entry.Key.gameObject.activeSelf)
                {
                    foreach (var line in entry.Value) { line.gameObject.SetActive(false); Destroy(line.gameObject); }
                    removed.Add(entry.Key);
                }
            foreach (var cell in removed) arrows.Remove(cell);
            float supply = lab.IsEditing ? lab.Physics.PreviewSupply() : 1;
            foreach (var cell in lab.Cells)
            {
                if (cell.Definition.kind != CellKind.Cilia) continue;
                if (!arrows.TryGetValue(cell, out var pair))
                {
                    pair = new[] { CreateLine("推水方向"), CreateLine("反作用力方向") }; arrows.Add(cell, pair);
                }
                Arrow(pair[0], cell, cell.FluidDirection, new Color(0.35f, 0.75f, 1f));
                float activation = lab.IsEditing ? lab.Physics.ActivationFor(cell) * supply : cell.Activation;
                Arrow(pair[1], cell, -cell.FluidDirection, Color.Lerp(new Color(0.65f, 0.43f, 0.2f), new Color(1f, 0.8f, 0.3f), activation), 0.25f + activation * 0.6f);
            }
            if (totalForce == null) { totalForce = CreateLine("身体合力"); totalForce.sortingOrder = 5; totalForce.startWidth = totalForce.endWidth = 0.07f; }
            var summary = CellForceSummary.Calculate(lab);
            totalForce.enabled = summary.Force.sqrMagnitude > 0.0001f;
            if (totalForce.enabled) DrawArrow(totalForce, summary.Center, summary.Force.normalized, Mathf.Min(2.2f, summary.Force.magnitude * 0.22f), new Color(0.85f, 0.5f, 1f));
        }

        private LineRenderer CreateLine(string title)
        {
            var obj = new GameObject(title); obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.positionCount = 5; line.useWorldSpace = true; line.sortingOrder = 4;
            line.startWidth = line.endWidth = 0.04f;
            return line;
        }

        private static void Arrow(LineRenderer line, CellView cell, Vector2 direction, Color color, float length = 0.6f)
        {
            Vector2 center = cell.transform.position;
            Vector2 start = center + direction * cell.Definition.radius;
            DrawArrow(line, start, direction, length, color);
        }

        private static void DrawArrow(LineRenderer line, Vector2 start, Vector2 direction, float length, Color color)
        {
            Vector2 tip = start + direction * length;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            line.SetPositions(new Vector3[] { start, tip, tip - direction * 0.18f + perpendicular * 0.12f, tip, tip - direction * 0.18f - perpendicular * 0.12f });
            line.startColor = line.endColor = color;
        }
    }
}
