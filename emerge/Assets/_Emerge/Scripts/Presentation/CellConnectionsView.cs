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
        private static readonly Color[] ChannelColors = { new Color(0.22f, 0.72f, 0.82f), new Color(0.55f, 0.85f, 0.4f), new Color(0.95f, 0.78f, 0.3f), new Color(0.85f, 0.45f, 0.85f) };
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
                Color color = new Color(0.38f, 0.42f, 0.46f);
                for (int c = 0; c < 4; c++) if ((edge.ChannelMask & (1 << c)) != 0) { color = ChannelColors[c]; break; }
                float activity = 0;
                if (!lab.IsEditing)
                    for (int c = 0; c < 4; c++)
                        if ((edge.ChannelMask & lab.Physics.ActiveMask & (1 << c)) != 0)
                            activity = Mathf.Max(activity, Mathf.Min(lab.Physics.Signals.Strength(lab.PrimaryCore, edge.A, c), lab.Physics.Signals.Strength(lab.PrimaryCore, edge.B, c)));
                if (!lab.IsCoreConnected(edge.A)) color = new Color(0.5f, 0.55f, 0.6f);
                color = Color.Lerp(color * 0.65f, Color.white, activity * 0.7f); color.a = 1;
                if (lab.SelectedConnection == edge) color = new Color(1f, 0.85f, 0.35f);
                line.startWidth = line.endWidth = lab.SelectedConnection == edge ? 0.18f : 0.11f;
                line.startColor = line.endColor = color;
            }
        }

    }
}
