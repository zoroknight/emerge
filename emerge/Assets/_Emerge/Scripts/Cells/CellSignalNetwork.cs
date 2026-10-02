using System.Collections.Generic;
using UnityEngine;

namespace Emerge.Cells
{
    // Cache strongest path products; each edge / destination node retains at most 100%.
    // Max-product closure terminates after a fixed node count, including lossless cycles.
    public sealed class CellSignalNetwork
    {
        private readonly Dictionary<CellView, int> indices = new Dictionary<CellView, int>();
        private float[,,] paths;
        private CellGraph cachedGraph;
        private int revision = -1;
        private int nodeCount;
        public int RebuildCount { get; private set; }

        public void Refresh(CellGraph graph, IReadOnlyList<CellView> nodes)
        {
            if (cachedGraph == graph && revision == graph.Revision && nodeCount == nodes.Count) return;
            cachedGraph = graph; revision = graph.Revision; nodeCount = nodes.Count; RebuildCount++;
            indices.Clear();
            paths = new float[4, nodeCount, nodeCount];
            for (int i = 0; i < nodeCount; i++) indices[nodes[i]] = i;
            for (int c = 0; c < 4; c++)
            {
                for (int i = 0; i < nodeCount; i++) paths[c, i, i] = 1;
                foreach (var edge in graph.Edges)
                {
                    if ((edge.ChannelMask & (1 << c)) == 0 || !indices.TryGetValue(edge.A, out int a) || !indices.TryGetValue(edge.B, out int b)) continue;
                    paths[c, a, b] = edge.Efficiency * Mathf.Clamp01(edge.B.Definition.signalRetention);
                    paths[c, b, a] = edge.Efficiency * Mathf.Clamp01(edge.A.Definition.signalRetention);
                }
                for (int k = 0; k < nodeCount; k++)
                    for (int i = 0; i < nodeCount; i++)
                        for (int j = 0; j < nodeCount; j++)
                            paths[c, i, j] = Mathf.Max(paths[c, i, j], paths[c, i, k] * paths[c, k, j]);
            }
        }

        public float Strength(CellView source, CellView target, int channel)
        {
            if (channel < 0 || channel > 3 || source == null || target == null || paths == null ||
                !indices.TryGetValue(source, out int a) || !indices.TryGetValue(target, out int b)) return 0;
            return paths[channel, a, b];
        }

        public float Activation(CellView source, CellView target, float[] inputs)
        {
            float result = 0;
            for (int c = 0; c < 4; c++)
                if ((target.ResponseMask & (1 << c)) != 0)
                    result = Mathf.Max(result, Strength(source, target, c) * Mathf.Clamp01(inputs[c]));
            return result;
        }
    }
}
