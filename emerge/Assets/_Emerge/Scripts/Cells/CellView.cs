using UnityEngine;

namespace Emerge.Cells
{
    public sealed class CellView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private GameObject selection;
        [SerializeField] private CellDefinition definition;
        [SerializeField] private bool applyDefinitionTint = true;

        public CellDefinition Definition => definition;

        public void Initialize(CellDefinition data)
        {
            definition = data;
            if (applyDefinitionTint) body.color = data.bodyColor;
            transform.localScale = Vector3.one * data.radius * 2f;
            SetSelected(false);
        }

        public void SetSelected(bool value) => selection.SetActive(value);
    }
}
