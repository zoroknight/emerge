using Emerge.Cells;
using UnityEngine;

namespace Emerge.World
{
    // Nutrients are advected tracers. A membrane is a finite capsule along the cell's local Y.
    // Sweep the entire displacement, then keep its tangential part; never teleport through a wall.
    public static class MembraneTransport
    {
        public const float HalfThickness = 0.08f;
        private const float Skin = 0.0001f;
        public static void Geometry(CellView cell, bool preview, out Vector2 a, out Vector2 b)
        {
            Vector2 center = preview || !cell.Body.simulated ? (Vector2)cell.transform.position : cell.Body.worldCenterOfMass;
            float angle = (preview || !cell.Body.simulated ? cell.transform.eulerAngles.z : cell.Body.rotation) * Mathf.Deg2Rad;
            Vector2 tangent = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * cell.Definition.radius;
            a = center - tangent; b = center + tangent;
        }
        public static Vector2 Closest(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 edge = b - a;
            return a + edge * Mathf.Clamp01(Vector2.Dot(p - a, edge) / edge.sqrMagnitude);
        }
        public static Vector2 SurfaceVelocity(System.Collections.Generic.IReadOnlyList<CellView> cells, Vector2 point, Vector2 velocity, bool preview)
        {
            foreach (var cell in cells)
            {
                if (cell.Definition.kind != CellKind.Membrane) continue;
                Geometry(cell, preview, out var a, out var b);
                Vector2 offset = point - Closest(point, a, b);
                float radius = HalfThickness + NutrientParticle.Radius;
                if (offset.sqrMagnitude > (radius + Skin * 2) * (radius + Skin * 2) || offset.sqrMagnitude < 0.00000001f) continue;
                Vector2 normal = offset.normalized;
                velocity -= normal * Mathf.Min(0, Vector2.Dot(velocity, normal));
            }
            return velocity;
        }
        public static Vector2 Move(System.Collections.Generic.IReadOnlyList<CellView> cells, Vector2 start, Vector2 displacement, out int contacts)
        {
            contacts = 0;
            float radius = HalfThickness + NutrientParticle.Radius;
            Vector2 position = start;
            // Resolve an initially embedded tracer (e.g. the body moved over it).
            for (int pass = 0; pass < 4; pass++)
                foreach (var cell in cells)
                {
                    if (cell.Definition.kind != CellKind.Membrane) continue;
                    Geometry(cell, false, out var a, out var b);
                    Vector2 offset = position - Closest(position, a, b);
                    if (offset.sqrMagnitude >= radius * radius) continue;
                    Vector2 normal = offset.sqrMagnitude > 0.00000001f ? offset.normalized :
                        new Vector2((b - a).y, -(b - a).x).normalized;
                    position += normal * (radius + Skin - offset.magnitude); contacts++;
                }
            for (int pass = 0; pass < 8 && displacement.sqrMagnitude > 0.0000000001f; pass++)
            {
                float first = 1; Vector2 hitNormal = Vector2.zero;
                foreach (var cell in cells)
                {
                    if (cell.Definition.kind != CellKind.Membrane) continue;
                    Geometry(cell, false, out var a, out var b);
                    if (Sweep(position, displacement, a, b, radius, out float t, out Vector2 normal) && t < first)
                    { first = t; hitNormal = normal; }
                }
                position += displacement * first;
                if (hitNormal == Vector2.zero) break;
                position += hitNormal * Skin; contacts++;
                displacement *= 1 - first;
                displacement -= hitNormal * Mathf.Min(0, Vector2.Dot(displacement, hitNormal));
                // At corners, any residual after the iteration cap is discarded rather than crossing.
            }
            return position;
        }
        public static bool Sweep(Vector2 p, Vector2 d, Vector2 a, Vector2 b, float radius, out float time, out Vector2 normal)
        {
            time = 2; normal = Vector2.zero;
            Vector2 tangent = (b - a).normalized, side = new Vector2(tangent.y, -tangent.x);
            float x = Vector2.Dot(p - a, side), dx = Vector2.Dot(d, side);
            for (int sign = -1; sign <= 1; sign += 2)
            {
                if (dx * sign >= -0.0000001f) continue;
                float t = (sign * radius - x) / dx;
                float along = Vector2.Dot(p + d * t - a, tangent);
                if (t >= 0 && t <= 1 && along >= 0 && along <= (b - a).magnitude && t < time)
                { time = t; normal = side * sign; }
            }
            EndHit(p, d, a, radius, ref time, ref normal);
            EndHit(p, d, b, radius, ref time, ref normal);
            return time <= 1;
        }
        private static void EndHit(Vector2 p, Vector2 d, Vector2 center, float radius, ref float time, ref Vector2 normal)
        {
            float dd = d.sqrMagnitude;
            if (dd < 0.0000000001f) return;
            Vector2 offset = p - center;
            float q = Vector2.Dot(offset, d), discriminant = q * q - dd * (offset.sqrMagnitude - radius * radius);
            if (discriminant < 0) return;
            float t = (-q - Mathf.Sqrt(discriminant)) / dd;
            if (t < 0 || t > 1 || t >= time) return;
            Vector2 n = (p + d * t - center).normalized;
            if (Vector2.Dot(d, n) >= -0.0000001f) return;
            time = t; normal = n;
        }
    }
}
