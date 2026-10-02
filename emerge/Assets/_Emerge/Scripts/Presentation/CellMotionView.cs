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
            foreach (var cell in lab.Cells)
            {
                if (cell.Definition.kind != CellKind.Cilia) continue;
                if (!arrows.TryGetValue(cell, out var pair))
                {
                    pair = new[] { CreateLine("推水方向"), CreateLine("反作用力方向") }; arrows.Add(cell, pair);
                }
                Arrow(pair[0], cell, cell.FluidDirection, new Color(0.35f, 0.75f, 1f));
                Arrow(pair[1], cell, -cell.FluidDirection, cell.Activation > 0 ? new Color(1f, 0.7f, 0.25f) : new Color(0.65f, 0.43f, 0.2f));
            }
        }

        private LineRenderer CreateLine(string title)
        {
            var obj = new GameObject(title); obj.transform.SetParent(transform, false);
            var line = obj.AddComponent<LineRenderer>(); line.sharedMaterial = material;
            line.positionCount = 5; line.useWorldSpace = true; line.sortingOrder = 4;
            line.startWidth = line.endWidth = 0.04f;
            return line;
        }

        private static void Arrow(LineRenderer line, CellView cell, Vector2 direction, Color color)
        {
            Vector2 center = cell.transform.position;
            Vector2 start = center + direction * cell.Definition.radius;
            Vector2 tip = start + direction * 0.6f;
            Vector2 perpendicular = new Vector2(-direction.y, direction.x);
            line.SetPositions(new Vector3[] { start, tip, tip - direction * 0.18f + perpendicular * 0.12f, tip, tip - direction * 0.18f - perpendicular * 0.12f });
            line.startColor = line.endColor = color;
        }
    }
}
