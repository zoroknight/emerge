using System;
using Emerge.Cells;
using UnityEngine;

namespace Emerge.Core
{
    [Serializable] public class ExperimentCell { public CellKind kind; public Vector2 position; public float rotation, contraction; }
    [Serializable] public class ExperimentLink { public int a, b; public IntentChannel channel; }
    [Serializable] public class ExperimentFood { public Vector2 position; public float amount = 0.4f; public int captor = -1; }
    [Serializable] public class ExperimentFlow { public Vector2 position; public float rotation, radius = 7, speed; public bool fade; }
    [Serializable] public class ExperimentData
    {
        public int version = 1;
        public string id, title;
        [TextArea(2, 5)] public string idea, controls, notes;
        public float nutrients = 2, energy = 8;
        public bool disableContraction;
        public Vector2 ambient;
        public ExperimentCell[] cells = new ExperimentCell[0];
        public ExperimentLink[] links = new ExperimentLink[0];
        public ExperimentFood[] food = new ExperimentFood[0];
        public ExperimentFlow[] flows = new ExperimentFlow[0];
    }
    [CreateAssetMenu(menuName = "Emerge/组合实验", fileName = "我的组合实验")]
    public sealed class CellExperiment : ScriptableObject { public ExperimentData data = new ExperimentData(); }
}
