using Emerge.Core;
using UnityEngine;

namespace Emerge.Presentation
{
    public sealed class CellFlowView : MonoBehaviour
    {
        private CellLabController lab;
        private Camera worldCamera;
        private readonly LineRenderer[] samples = new LineRenderer[48];
        public void Initialize(CellLabController controller, Camera sceneCamera, Material material)
        {
            lab = controller; worldCamera = sceneCamera;
            for (int i = 0; i < samples.Length; i++)
            {
                var obj = new GameObject("局部流速"); obj.transform.SetParent(transform, false);
                var line = obj.AddComponent<LineRenderer>(); line.sharedMaterial = material;
                line.positionCount = 5; line.useWorldSpace = true; line.sortingOrder = -2;
                line.startWidth = line.endWidth = 0.025f;
                line.startColor = line.endColor = new Color(0.25f, 0.65f, 0.75f, 0.55f);
                samples[i] = line;
            }
        }
        private void LateUpdate()
        {
            if (lab == null) return;
            float supply = lab.IsEditing ? lab.Physics.PreviewSupply() : 1;
            for (int i = 0; i < samples.Length; i++)
            {
                Vector2 point = worldCamera.ViewportToWorldPoint(new Vector3((i % 8 + 0.5f) / 8,
                    0.08f + (i / 8 + 0.5f) / 6 * (CellLabController.ArenaTop - 0.08f), -worldCamera.transform.position.z));
                Vector2 velocity = lab.Food.FlowVelocityAt(point, lab.IsEditing, supply);
                var line = samples[i]; line.enabled = lab.ShowFlow && velocity.sqrMagnitude > 0.0025f;
                if (!line.enabled) continue;
                Vector2 direction = velocity.normalized, tip = point + direction * Mathf.Min(0.65f, velocity.magnitude * 0.25f);
                Vector2 side = new Vector2(-direction.y, direction.x) * 0.07f;
                line.SetPositions(new Vector3[] { point, tip, tip - direction * 0.12f + side, tip, tip - direction * 0.12f - side });
            }
        }
    }
}
