#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Emerge.Cells;
using UnityEngine;

namespace Emerge.Core
{
    public static class CellStressFixtures
    {
        public static void Build(CellLabController lab, string shape, int count)
        {
            lab.PrepareStressLab(count, 12f);
            float coreRadius = lab.CoreDefinition.radius, ciliaRadius = lab.CiliaDefinition.radius;
            Vector3 center = new Vector3(0, -1.92f, 0);
            if (shape == "chain")
            {
                float length = coreRadius + ciliaRadius + (count - 2) * ciliaRadius * 2;
                for (int i = 0; i < count; i++)
                {
                    float x = i == 0 ? 0 : coreRadius + ciliaRadius + (i - 1) * ciliaRadius * 2;
                    lab.CreateStressCell(i == 0 ? CellKind.Core : CellKind.Cilia, center + new Vector3(x - length / 2, 0, 0), 180);
                    if (i > 0) Connect(lab, i - 1, i);
                }
            }
            else if (shape == "symmetric")
            {
                lab.CreateStressCell(CellKind.Core, center, 270);
                int arm = (count - 2) / 2;
                for (int side = -1; side <= 1; side += 2)
                {
                    int previous = 0;
                    for (int i = 0; i < arm; i++)
                    {
                        float x = (coreRadius + ciliaRadius + i * ciliaRadius * 2) * side;
                        lab.CreateStressCell(CellKind.Cilia, center + new Vector3(x, 0, 0), 270);
                        int index = lab.Cells.Count - 1;
                        Connect(lab, previous, index); previous = index;
                    }
                }
                lab.CreateStressCell(CellKind.Cilia, center + new Vector3(0, coreRadius + ciliaRadius, 0), 270);
                Connect(lab, 0, count - 1);
            }
            else if (shape == "ring")
            {
                float low = (coreRadius + ciliaRadius) / 2 + 0.001f, high = count;
                for (int iteration = 0; iteration < 40; iteration++)
                {
                    float radius = (low + high) / 2;
                    float sum = 4 * Mathf.Asin((coreRadius + ciliaRadius) / (2 * radius)) +
                        (count - 2) * 2 * Mathf.Asin(ciliaRadius / radius);
                    if (sum > Mathf.PI * 2) low = radius; else high = radius;
                }
                float r = (low + high) / 2, angle = Mathf.PI / 2;
                for (int i = 0; i < count; i++)
                {
                    lab.CreateStressCell(i == 0 ? CellKind.Core : CellKind.Cilia, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * r,
                        angle * Mathf.Rad2Deg + 90);
                    if (i > 0) Connect(lab, i - 1, i);
                    float gap = i == 0 || i == count - 1 ? coreRadius + ciliaRadius : ciliaRadius * 2;
                    angle += 2 * Mathf.Asin(gap / (2 * r));
                }
                Connect(lab, count - 1, 0);
            }
            else throw new ArgumentException("Unknown stress shape: " + shape);
            if (lab.Cells.Count != count || lab.Graph.Component(lab.PrimaryCore).Count != count)
                throw new InvalidOperationException("Stress fixture is incomplete.");
            lab.Select(lab.PrimaryCore);
            lab.SetMessage("压力测试：" + shape + " / " + count + " 节点。测试结束后恢复普通实验室。");
        }

        private static void Connect(CellLabController lab, int a, int b)
        {
            if (!lab.Connect(lab.Cells[a], lab.Cells[b])) throw new InvalidOperationException("Stress fixture connection failed: " + lab.Message);
        }
    }
}
#endif
