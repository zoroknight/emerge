using Emerge.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Emerge.Presentation
{
    public sealed class CellLabPanel : MonoBehaviour
    {
        private CellLabController lab;
        private Text status;
        private Text details;
        private Font font;
        public Button CoreButton { get; private set; }
        public Button CiliaButton { get; private set; }
        public Button ResetButton { get; private set; }
        public Button ClearButton { get; private set; }

        public void Initialize(CellLabController controller)
        {
            lab = controller;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Lab UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1f;
            var bar = new GameObject("Toolbar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(canvasObject.transform, false);
            var rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1); rect.sizeDelta = new Vector2(0, 120);
            rect.anchoredPosition = Vector2.zero;
            bar.GetComponent<Image>().color = new Color(0.04f, 0.1f, 0.14f, 0.96f);
            Label(bar.transform, "CELL LAB", new Vector2(24, -16), new Vector2(230, 36), 28);
            CoreButton = MakeButton(bar.transform, "Add core  [1]", 280, lab.SpawnCore);
            CiliaButton = MakeButton(bar.transform, "Add cilia  [2]", 460, lab.SpawnCilia);
            ResetButton = MakeButton(bar.transform, "Reset  [Backspace]", 640, lab.ResetLab);
            ClearButton = MakeButton(bar.transform, "Clear", 840, lab.ClearLab);
            status = Label(bar.transform, "", new Vector2(24, -72), new Vector2(270, 28), 18);
            details = Label(bar.transform, "", new Vector2(300, -72), new Vector2(930, 28), 18);
            Label(canvasObject.transform, "Click a cell to inspect  |  Core: glowing center  |  Cilia: three soft tails  |  Esc: quit",
                new Vector2(24, -680), new Vector2(1230, 28), 17);
            if (EventSystem.current == null)
            {
                var events = new GameObject("Lab EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            Refresh();
        }

        public void Refresh()
        {
            if (status == null) return;
            status.text = "Samples: " + lab.Cells.Count + " / " + lab.Capacity;
            details.text = lab.Selected == null ? "Select a sample to inspect." :
                "Selected: " + lab.Selected.Definition.displayName + "   |   Radius: " + lab.Selected.Definition.radius.ToString("0.00");
            CoreButton.interactable = CiliaButton.interactable = lab.Cells.Count < lab.Capacity;
        }

        private Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
        {
            var obj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = obj.GetComponent<Text>();
            text.font = font; text.fontSize = fontSize; text.text = value;
            text.color = new Color(0.82f, 0.94f, 0.95f); text.raycastTarget = false;
            return text;
        }

        private Button MakeButton(Transform parent, string title, float x, UnityEngine.Events.UnityAction action)
        {
            var obj = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -20); rect.sizeDelta = new Vector2(165, 40);
            var image = obj.GetComponent<Image>(); image.color = new Color(0.16f, 0.35f, 0.39f);
            var button = obj.GetComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(action);
            var label = Label(obj.transform, title, Vector2.zero, rect.sizeDelta, 16);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
    }
}
