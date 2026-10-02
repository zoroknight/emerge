using System.Collections.Generic;
using Emerge.Cells;
using Emerge.Core;
using UnityEngine;

namespace Emerge.Presentation
{
    public sealed class CellConnectionsView : MonoBehaviour
    {
        private CellLabController lab;
        private Material material;
        private readonly Dictionary<CellConnection, LineRenderer> lines = new Dictionary<CellConnection, LineRenderer>();
        public void Initialize(CellLabController controller, Material sharedMaterial)
        {
            lab = controller;
            material = sharedMaterial;
            if (material == null) throw new System.InvalidOperationException("连接显示缺少材质引用。");
        }

        private void LateUpdate()
        {
            if (lab == null) return;
            var removed = new List<CellConnection>();
            foreach (var entry in lines)
            {
                bool exists = false;
                foreach (var edge in lab.Graph.Edges) if (edge == entry.Key) { exists = true; break; }
                if (!exists) { entry.Value.gameObject.SetActive(false); Destroy(entry.Value.gameObject); removed.Add(entry.Key); }
            }
            foreach (var edge in removed) lines.Remove(edge);
            foreach (var edge in lab.Graph.Edges)
            {
                if (!lines.TryGetValue(edge, out var line))
                {
                    var obj = new GameObject("连接桥"); obj.transform.SetParent(transform, false);
                    line = obj.AddComponent<LineRenderer>();
                    line.sharedMaterial = material; line.positionCount = 2; line.useWorldSpace = true;
                    line.startWidth = line.endWidth = 0.11f; line.numCapVertices = 4; line.sortingOrder = 3;
                    lines.Add(edge, line);
                }
                Vector3 direction = (edge.B.transform.position - edge.A.transform.position).normalized;
                line.SetPosition(0, edge.A.transform.position + direction * edge.A.Definition.radius * 0.82f);
                line.SetPosition(1, edge.B.transform.position - direction * edge.B.Definition.radius * 0.82f);
                line.startColor = line.endColor = lab.IsCoreConnected(edge.A) ? new Color(0.55f, 0.95f, 0.78f) : new Color(0.5f, 0.55f, 0.6f);
            }
        }

    }
}
