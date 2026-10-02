using Emerge.Core;
using UnityEngine;

namespace Emerge.Cells
{
    public struct CellForceSummary
    {
        public Vector2 Center, Force;
        public float Torque;
        public int ActiveCilia;

        public static CellForceSummary Calculate(CellLabController lab)
        {
            var result = new CellForceSummary();
            if (lab.Physics == null) return result;
            var body = lab.Graph.Component(lab.PrimaryCore);
            float mass = 0;
            foreach (var cell in body)
            {
                Vector2 position = lab.IsEditing ? (Vector2)cell.transform.position : cell.Body.worldCenterOfMass;
                result.Center += position * cell.Body.mass; mass += cell.Body.mass;
            }
            if (mass == 0) return result;
            result.Center /= mass;
            float supply = lab.IsEditing ? lab.Physics.PreviewSupply() : 1;
            foreach (var cell in body)
            {
                float activation = lab.IsEditing ? lab.Physics.ActivationFor(cell) * supply : cell.Activation;
                if (cell.Definition.kind == CellKind.Cilia && activation > 0) result.ActiveCilia++;
                Vector2 force = lab.IsEditing ? cell.ForceFor(activation) : cell.LastForce;
                Vector2 position = lab.IsEditing ? (Vector2)cell.transform.position : cell.Body.worldCenterOfMass;
                Vector2 arm = position - result.Center;
                result.Force += force;
                result.Torque += arm.x * force.y - arm.y * force.x;
            }
            return result;
        }
    }
}
