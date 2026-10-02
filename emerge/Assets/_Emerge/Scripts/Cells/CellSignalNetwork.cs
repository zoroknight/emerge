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
        private CellView cachedSource;
        private int revision = -1;
        private int nodeCount;
        public int RebuildCount { get; private set; }

        public void Refresh(CellGraph graph, IReadOnlyList<CellView> nodes, CellView source)
        {
            if (cachedGraph == graph && cachedSource == source && revision == graph.Revision && nodeCount == nodes.Count) return;
            cachedSource = source;
            cachedGraph = graph; revision = graph.Revision; nodeCount = nodes.Count; RebuildCount++;
            indices.Clear();
            paths = new float[4, nodeCount, nodeCount];
            for (int i = 0; i < nodeCount; i++) indices[nodes[i]] = i;
            for (int c = 0; c < 4; c++)
            {
                for (int i = 0; i < nodeCount; i++) paths[c, i, i] = 1;
                foreach (var edge in graph.Edges)
                {
                    if (!indices.TryGetValue(edge.A, out int a) || !indices.TryGetValue(edge.B, out int b)) continue;
                    // Only source exits select a channel. Relays forward all received channels.
                    // No signal re-enters the source and leaks out through a different root branch.
                    if (edge.B != source && (edge.A != source || (int)edge.CoreChannel == c))
                        paths[c, a, b] = Mathf.Clamp01(edge.B.Definition.signalRetention);
                    if (edge.A != source && (edge.B != source || (int)edge.CoreChannel == c))
                        paths[c, b, a] = Mathf.Clamp01(edge.A.Definition.signalRetention);
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
                result = Mathf.Max(result, Strength(source, target, c) * Mathf.Clamp01(inputs[c]));
            return result;
        }
    }
}
