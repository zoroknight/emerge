#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Emerge.Cells;
using UnityEngine;

namespace Emerge.Core
{
    public static class CellGraphSmokeCheck
    {
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("T03: " + message); }

        public static void Run(CellLabController lab)
        {
            lab.ResetLab();
            var core = lab.Cells[0]; var cilia = lab.Cells[1];
            core.transform.position = Vector3.zero; cilia.transform.position = new Vector3(3, 0, 0);
            lab.BeginDrag(cilia, cilia.transform.position);
            lab.MoveDrag(new Vector3(1.5f, 0.8f, 0)); lab.EndDrag();
            Require(lab.Graph.Edges.Count == 1 && lab.IsCoreConnected(cilia), "Continuous-angle snap did not connect.");
            Require(Mathf.Abs(Vector2.Distance(core.transform.position, cilia.transform.position) - 1.3f) < 0.001f && cilia.transform.position.y > 0.2f,
                "Snap radius or free angle incorrect.");
            Require(!lab.Connect(core, cilia) && !lab.Connect(core, core) && lab.Graph.Edges.Count == 1, "Duplicate / self edge allowed.");
            var coreBefore = core.transform.position; var ciliaBefore = cilia.transform.position;
            lab.BeginDrag(cilia, ciliaBefore); lab.MoveDrag(ciliaBefore + new Vector3(-1, -1, 0)); lab.EndDrag();
            Require(Vector3.Distance(core.transform.position, coreBefore + new Vector3(-1, -1, 0)) < 0.001f &&
                Vector3.Distance(cilia.transform.position, ciliaBefore + new Vector3(-1, -1, 0)) < 0.001f, "Connected body did not drag as a group.");
            lab.ToggleMode(); lab.DisconnectSelected(); lab.DeleteSelected();
            Require(lab.Cells.Count == 2 && lab.Graph.Edges.Count == 1, "Swim allowed topology changes.");
            lab.ToggleMode(); lab.DisconnectSelected();
            Require(!lab.IsCoreConnected(cilia) && lab.Graph.Edges.Count == 0, "Detached fragment retained control.");

            lab.ResetLab(); core = lab.Cells[0]; cilia = lab.Cells[1];
            core.transform.position = Vector3.zero; cilia.transform.position = new Vector3(3, 0, 0);
            Require(!lab.Connect(core, cilia), "Long-distance edge allowed.");
            lab.BeginDrag(cilia, cilia.transform.position); lab.MoveDrag(Vector3.zero); lab.EndDrag();
            Require(cilia.transform.position == new Vector3(3, 0, 0), "Drag allowed overlap.");
            cilia.transform.position = new Vector3(0.5f, 0, 0);
            Require(!lab.Connect(core, cilia), "Overlapping edge allowed.");

            lab.ResetLab(); lab.SpawnCilia();
            core = lab.Cells[0]; cilia = lab.Cells[1]; var third = lab.Cells[2];
            core.transform.position = Vector3.zero; cilia.transform.position = new Vector3(1.3f, 0, 0);
            float x = (2 * 1.3f * 1.3f - 1.16f * 1.16f) / (2 * 1.3f);
            third.transform.position = new Vector3(x, Mathf.Sqrt(1.3f * 1.3f - x * x), 0);
            Require(lab.Connect(core, cilia) && lab.Connect(cilia, third) && lab.Connect(third, core), "Ring construction failed.");
            Require(lab.Graph.Edges.Count == 3 && lab.Graph.Component(core).Count == 3, "Cycle traversal failed.");
            lab.Graph.Remove(lab.Graph.Edges[0]);
            Require(lab.IsCoreConnected(cilia), "Cycle lost valid alternative path.");

            lab.ResetLab(); lab.SpawnCilia(); core = lab.Cells[0]; cilia = lab.Cells[1]; third = lab.Cells[2];
            core.transform.position = Vector3.zero; cilia.transform.position = new Vector3(1.3f, 0, 0); third.transform.position = new Vector3(2.46f, 0, 0);
            Require(lab.Connect(core, cilia) && lab.Connect(cilia, third) && lab.IsCoreConnected(third), "Chain failed.");
            lab.Select(cilia); lab.DeleteSelected();
            Require(lab.Cells.Count == 2 && lab.Graph.Edges.Count == 0 && !lab.IsCoreConnected(third), "Bridge deletion retained dangling edges / control.");
            lab.Select(core); lab.DeleteSelected();
            Require(lab.PrimaryCore == null && !lab.IsCoreConnected(third), "Deleting the only core retained control.");

            lab.ResetLab();
            for (int i = 0; i < 5; i++) lab.SpawnCilia();
            core = lab.Cells[0]; cilia = lab.Cells[1];
            core.transform.position = new Vector3(-5, 0, 0); cilia.transform.position = Vector3.zero;
            for (int i = 0; i < 5; i++)
            {
                var leaf = lab.Cells[i + 2]; float angle = i * Mathf.PI * 2 / 5;
                leaf.transform.position = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * 1.16f;
                bool success = lab.Connect(cilia, leaf);
                Require(success == (i < 4), "Node connection limit incorrect.");
            }
            Require(lab.Graph.Degree(cilia) == 4 && !lab.IsCoreConnected(cilia), "Disconnected star gained core control.");
            lab.ClearLab();
            Require(lab.Graph.Edges.Count == 0 && lab.PrimaryCore == null, "Clear left graph state.");
            lab.ResetLab();
            Require(lab.Cells.Count == 2 && lab.Graph.Edges.Count == 0 && lab.PrimaryCore == lab.Cells[0], "Reset left graph state.");
            Debug.Log("CELL_LAB_T03_GRAPH_PASS: free-angle snap, group drag, overlap/duplicate/self/distance/degree guards, chain, cycle, bridge deletion and core control.");
        }

        public static void Preview(CellLabController lab)
        {
            lab.ResetLab(); lab.SpawnCilia(); lab.SpawnCilia();
            var core = lab.Cells[0]; core.transform.position = new Vector3(-1.7f, 0.4f, 0);
            var a = lab.Cells[1]; a.transform.position = core.transform.position + new Vector3(1.3f, 0, 0);
            var b = lab.Cells[2]; b.transform.position = a.transform.position + new Vector3(1.16f, 0, 0);
            lab.Cells[3].transform.position = new Vector3(3.5f, -1.8f, 0);
            lab.Connect(core, a); lab.Connect(a, b); lab.Select(b);
            lab.SetMessage("绿色连接与主核心连通；右下方细胞尚未连接。拖近后松开即可融合。");
        }
    }
}
#endif
