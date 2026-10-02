using System.Collections.Generic;
using UnityEngine;

namespace Emerge.Cells
{
    public sealed class CellConnection
    {
        public CellView A { get; }
        public CellView B { get; }
        public CellConnection(CellView a, CellView b) { A = a; B = b; }
        public bool Contains(CellView cell) => A == cell || B == cell;
    }

    // Topology only: the editor and later signal / physics systems share these edges.
    public sealed class CellGraph
    {
        private readonly List<CellConnection> edges = new List<CellConnection>();
        public IReadOnlyList<CellConnection> Edges => edges;

        public int Degree(CellView cell)
        {
            int count = 0;
            foreach (var edge in edges) if (edge.Contains(cell)) count++;
            return count;
        }

        public bool HasEdge(CellView a, CellView b)
        {
            foreach (var edge in edges) if (edge.Contains(a) && edge.Contains(b)) return true;
            return false;
        }

        public HashSet<CellView> Component(CellView start)
        {
            var visited = new HashSet<CellView>();
            if (start == null) return visited;
            var queue = new Queue<CellView>();
            visited.Add(start); queue.Enqueue(start);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var edge in edges)
                {
                    if (!edge.Contains(current)) continue;
                    CellView next = edge.A == current ? edge.B : edge.A;
                    if (visited.Add(next)) queue.Enqueue(next);
                }
            }
            return visited;
        }

        public bool TryConnect(CellView a, CellView b, out string reason)
        {
            reason = "";
            if (a == null || b == null || a == b) { reason = "请选择两个不同的细胞。"; return false; }
            if (HasEdge(a, b)) { reason = "这两个细胞已经连接。"; return false; }
            if (Degree(a) >= a.Definition.maxConnections || Degree(b) >= b.Definition.maxConnections)
            { reason = "连接数量已达上限。"; return false; }
            float expected = a.Definition.radius + b.Definition.radius;
            float distance = Vector2.Distance(a.transform.position, b.transform.position);
            if (distance < expected - 0.01f) { reason = "细胞重叠，无法连接。"; return false; }
            if (distance > expected + 0.06f) { reason = "细胞太远，请拖到圆周接触处。"; return false; }
            edges.Add(new CellConnection(a, b));
            return true;
        }

        public bool Remove(CellConnection edge) => edges.Remove(edge);
        public void Disconnect(CellView cell) => edges.RemoveAll(edge => edge.Contains(cell));
        public void Clear() => edges.Clear();
    }
}
