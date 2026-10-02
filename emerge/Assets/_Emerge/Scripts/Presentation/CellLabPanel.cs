using Emerge.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Emerge.Presentation
{
    public sealed class CellLabPanel : MonoBehaviour
    {
        [SerializeField] private Font chineseFont;
        private CellLabController lab;
        private Text status;
        private Text details;
        private Font font;
        public Button CoreButton { get; private set; }
        public Button CiliaButton { get; private set; }
        public Button ResetButton { get; private set; }
        public Button ClearButton { get; private set; }
        public Button ModeButton { get; private set; }
        private Text modeLabel;
        private Text help;
        private Text feedback;
        private Button disconnectButton;
        private Button deleteButton;
        public Button[] SignalButtons { get; } = new Button[4];

        public void Initialize(CellLabController controller)
        {
            lab = controller;
            font = chineseFont;
            if (font == null) throw new System.InvalidOperationException("实验室未配置中文字体，请检查 CellLabPanel 的字体引用。");
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
            rect.pivot = new Vector2(0.5f, 1); rect.sizeDelta = new Vector2(0, 154);
            rect.anchoredPosition = Vector2.zero;
            bar.GetComponent<Image>().color = new Color(0.04f, 0.1f, 0.14f, 0.96f);
            Label(bar.transform, "细胞实验室", new Vector2(24, -8), new Vector2(230, 52), 28);
            CoreButton = MakeButton(bar.transform, "添加核心 [1]", 260, lab.SpawnCore);
            CiliaButton = MakeButton(bar.transform, "添加纤毛 [2]", 440, lab.SpawnCilia);
            ResetButton = MakeButton(bar.transform, "重置 [退格]", 620, lab.ResetLab);
            ClearButton = MakeButton(bar.transform, "清空", 800, lab.ClearLab);
            ModeButton = MakeButton(bar.transform, "", 980, lab.ToggleMode);
            modeLabel = ModeButton.GetComponentInChildren<Text>();
            status = Label(bar.transform, "", new Vector2(24, -72), new Vector2(270, 28), 18);
            details = Label(bar.transform, "", new Vector2(300, -72), new Vector2(930, 28), 18);
            feedback = Label(bar.transform, "", new Vector2(24, -112), new Vector2(750, 28), 17);
            for (int i = 0; i < 4; i++)
            {
                int channel = i;
                SignalButtons[i] = MakeButton(bar.transform, "", 24 + i * 100, () => lab.SetCoreChannel(channel), 90);
                SignalButtons[i].GetComponent<RectTransform>().anchoredPosition = new Vector2(24 + i * 100, -108);
            }
            disconnectButton = MakeButton(bar.transform, "拆开 [X]", 800, lab.DisconnectSelected);
            deleteButton = MakeButton(bar.transform, "删除 [Delete]", 980, lab.DeleteSelected);
            disconnectButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(800, -108);
            deleteButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(980, -108);
            var footer = new GameObject("Footer", typeof(RectTransform), typeof(Image));
            footer.transform.SetParent(canvasObject.transform, false);
            var footerRect = footer.GetComponent<RectTransform>();
            footerRect.anchorMin = Vector2.zero; footerRect.anchorMax = new Vector2(1, 0);
            footerRect.pivot = new Vector2(0.5f, 0); footerRect.sizeDelta = new Vector2(0, 82);
            footerRect.anchoredPosition = Vector2.zero;
            footer.GetComponent<Image>().color = new Color(0.04f, 0.1f, 0.14f, 0.96f);
            footer.GetComponent<Image>().raycastTarget = false;
            help = Label(canvasObject.transform, "",
                new Vector2(24, -646), new Vector2(1230, 64), 17);
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
            status.text = (lab.IsEditing ? "编辑模式" : "游动模式") + " · 细胞：" + lab.Cells.Count + " / " + lab.Capacity;
            string input = "";
            if (lab.Physics != null)
                for (int i = 0; i < 4; i++) if ((lab.Physics.ActiveMask & (1 << i)) != 0) input += ((Emerge.Cells.IntentChannel)i) + " ";
            details.text = lab.Selected == null ? "点击细胞查看信息。" :
                "已选：" + lab.Selected.Definition.displayName + "  |  半径：" + lab.Selected.Definition.radius.ToString("0.00") +
                "  |  朝向：" + lab.Selected.transform.eulerAngles.z.ToString("0") + "°" +
                "  |  连接：" + lab.Graph.Degree(lab.Selected) + "/" + lab.Selected.Definition.maxConnections +
                "  |  " + (lab.Selected == lab.PrimaryCore ? "主核心" : lab.IsCoreConnected(lab.Selected) ? "核心连通" : "无核心控制") +
                (lab.Selected.Definition.kind == Emerge.Cells.CellKind.Cilia ? "  |  响应：" + lab.Selected.Activation.ToString("0.00") : "");
            var edge = lab.SelectedConnection;
            bool configure = lab.IsCoreExit(edge);
            int mask = lab.ConnectionChannels(edge);
            if (edge != null) details.text = configure ?
                "主核心直连出口  |  信号：" + edge.CoreChannel + "  |  后续细胞自动响应与传递" :
                "后续连接  |  自动继承：" + Channels(mask) + "  |  无需配置通道或损耗";
            for (int i = 0; i < 4; i++)
            {
                SignalButtons[i].gameObject.SetActive(configure);
                SignalButtons[i].interactable = lab.IsEditing;
                SignalButtons[i].GetComponentInChildren<Text>().text = ((Emerge.Cells.IntentChannel)i) + ((mask & (1 << i)) != 0 ? " 已选" : "");
            }
            var feedbackRect = feedback.GetComponent<RectTransform>();
            feedbackRect.anchoredPosition = new Vector2(configure ? 424 : 24, -112);
            feedbackRect.sizeDelta = new Vector2(configure ? 356 : 750, 28);
            feedback.fontSize = configure ? 14 : 17;
            feedback.text = lab.Message;
            CoreButton.interactable = CiliaButton.interactable = lab.IsEditing && lab.Cells.Count < lab.Capacity;
            ResetButton.interactable = ClearButton.interactable = lab.IsEditing;
            disconnectButton.interactable = lab.IsEditing && (lab.Selected != null || edge != null);
            deleteButton.interactable = lab.IsEditing && lab.Selected != null;
            modeLabel.text = lab.IsEditing ? "开始游动 [Tab]" : "返回编辑 [Tab]";
            help.text = lab.IsEditing ? "拖近后松开连接  |  Shift + 点击补边  |  Q / E、滚轮旋转  |  X 拆开  |  Delete 删除  |  Tab 游动\n只在主核心直连的连接桥选择 W / A / S / D；后续自动跟随，每过一个细胞保留 90%。" :
                "按住 W / A / S / D 激活对应核心分支  |  当前输入：" + (input.Length == 0 ? "无" : input) + "  |  Tab 返回编辑\n橙色箭头变亮表示正在施力；松键后停止施力，惯性在阻尼作用下逐渐减弱。";
        }

        public static string Channels(int mask)
        {
            string value = "";
            for (int i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) value += ((Emerge.Cells.IntentChannel)i) + " ";
            return value.Length == 0 ? "无" : value.TrimEnd();
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

        private Button MakeButton(Transform parent, string title, float x, UnityEngine.Events.UnityAction action, float width = 165)
        {
            var obj = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
            obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -20); rect.sizeDelta = new Vector2(width, 40);
            var image = obj.GetComponent<Image>(); image.color = new Color(0.16f, 0.35f, 0.39f);
            var button = obj.GetComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(action);
            var label = Label(obj.transform, title, Vector2.zero, rect.sizeDelta, 16);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
    }
}
