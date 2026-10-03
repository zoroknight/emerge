using Emerge.Core;
using Emerge.Cells;
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
        public Font UiFont => font;
        public Transform CanvasRoot { get; private set; }
        public Button CoreButton { get; private set; }
        public Button CiliaButton { get; private set; }
        public Button AbsorberButton { get; private set; }
        public Button MembraneButton { get; private set; }
        public Button ContractorButton { get; private set; }
        private Button membraneTrialButton;
        public Button FoodButton { get; private set; }
        private Text resources;
        public Button ResetButton { get; private set; }
        public Button ClearButton { get; private set; }
        public Button ModeButton { get; private set; }
        public Button ExampleButton { get; private set; }
        public string ForceText => feedback != null ? feedback.text : "";
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
            CanvasRoot = canvasObject.transform;
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 1f;
            var bar = new GameObject("Toolbar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(canvasObject.transform, false);
            var rect = bar.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1); rect.sizeDelta = new Vector2(0, 196);
            rect.anchoredPosition = Vector2.zero;
            bar.GetComponent<Image>().color = new Color(0.04f, 0.1f, 0.14f, 0.96f);
            Label(bar.transform, "细胞实验室", new Vector2(24, -8), new Vector2(230, 52), 28);
            CoreButton = MakeButton(bar.transform, "添加核心 [1]", 260, lab.SpawnCore, 118);
            CiliaButton = MakeButton(bar.transform, "添加纤毛 [2]", 388, lab.SpawnCilia, 118);
            AbsorberButton = MakeButton(bar.transform, "添加吸收 [4]", 516, lab.SpawnAbsorber, 118);
            ResetButton = MakeButton(bar.transform, "重置 [退格]", 644, lab.ResetLab, 128);
            ClearButton = MakeButton(bar.transform, "清空", 782, lab.ClearLab, 90);
            ModeButton = MakeButton(bar.transform, "", 882, lab.ToggleMode, 140);
            modeLabel = ModeButton.GetComponentInChildren<Text>();
            ExampleButton = MakeButton(bar.transform, "示例 [3]", 1032, lab.LoadNextExample, 100);
            FoodButton = MakeButton(bar.transform, "投放营养 [5]", 1142, lab.SeedFood, 114);
            resources = Label(bar.transform, "", new Vector2(24, -155), new Vector2(1230, 30), 17);
            MembraneButton = MakeButton(bar.transform, "添加膜 [8]", 24, lab.SpawnMembrane, 118);
            MembraneButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(24, -66);
            status = Label(bar.transform, "", new Vector2(152, -72), new Vector2(145, 28), 16);
            details = Label(bar.transform, "", new Vector2(300, -72), new Vector2(930, 28), 18);
            feedback = Label(bar.transform, "", new Vector2(24, -112), new Vector2(750, 28), 17);
            membraneTrialButton = MakeButton(bar.transform, "膜对照 [9]", 640, lab.LoadNextMembraneTrial, 150);
            membraneTrialButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(640, -108);
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
            deleteButton.GetComponent<RectTransform>().sizeDelta = new Vector2(135, 40);
            ContractorButton = MakeButton(bar.transform, "添加收缩 [0]", 1130, lab.SpawnContractor, 126);
            ContractorButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(1130, -108);
            var footer = new GameObject("Footer", typeof(RectTransform), typeof(Image));
            footer.transform.SetParent(canvasObject.transform, false);
            var footerRect = footer.GetComponent<RectTransform>();
            footerRect.anchorMin = Vector2.zero; footerRect.anchorMax = new Vector2(1, 0);
            footerRect.pivot = new Vector2(0.5f, 0); footerRect.sizeDelta = new Vector2(0, 82);
            footerRect.anchoredPosition = Vector2.zero;
            footer.GetComponent<Image>().color = new Color(0.04f, 0.1f, 0.14f, 0.96f);
            footer.GetComponent<Image>().raycastTarget = false;
            help = Label(canvasObject.transform, "",
                new Vector2(24, -646), new Vector2(1040, 64), 16);
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
            status.text = (lab.IsEditing ? "编辑" : "游动") + " · " + lab.Cells.Count + "/" + lab.Capacity + " 细胞";
            string input = "";
            if (lab.Physics != null)
                for (int i = 0; i < 4; i++) if ((lab.Physics.ActiveMask & (1 << i)) != 0) input += ((Emerge.Cells.IntentChannel)i) + " ";
            details.text = lab.Selected == null ? "点击细胞查看信息。" :
                "已选：" + lab.Selected.Definition.displayName + "  |  半径：" + lab.Selected.Definition.radius.ToString("0.00") +
                "  |  朝向：" + lab.Selected.transform.eulerAngles.z.ToString("0") + "°" +
                "  |  连接：" + lab.Graph.Degree(lab.Selected) + "/" + lab.Selected.Definition.maxConnections +
                "  |  " + (lab.Selected == lab.PrimaryCore ? "主核心" : lab.IsCoreConnected(lab.Selected) ? "核心连通" : "无核心控制") +
                (lab.Selected.Definition.kind == CellKind.Cilia ? "  |  信号：" + ReceivedSignals(lab.Selected) +
                    "  |  " + (lab.IsEditing ? "预览：" : "响应：") +
                    (lab.IsEditing && lab.Physics != null ? lab.Physics.ActivationFor(lab.Selected) * lab.Physics.PreviewSupply() : lab.Selected.Activation).ToString("0.00") : "");
            if (lab.Selected != null && lab.Selected.Definition.absorptionRate > 0)
                details.text += "  |  消化 " + lab.Selected.Definition.absorptionRate.ToString("0.00") + "/秒" +
                    (lab.Selected.Definition.kind == CellKind.Absorber ? "  |  胃内 " + lab.Food.StoredCount(lab.Selected) + " 颗" : "");
            if (lab.Selected != null && lab.Selected.Definition.kind == CellKind.Contractor)
                details.text += "  |  收缩 " + lab.Selected.Contraction.ToString("P0") +
                    "  |  " + (lab.IsEditing ? "信号预览 " + lab.Physics.ActivationFor(lab.Selected).ToString("P0") : "响应 " + lab.Selected.Activation.ToString("P0"));
            if (lab.Selected != null && lab.Selected.Definition.kind == CellKind.Membrane)
                details.text += "  |  膜片被动阻挡";
            if (lab.Metabolism != null)
            {
                var state = lab.Metabolism; var data = state.Settings;
                float requested = lab.Physics != null ? lab.Physics.RequestedEnergyRate() : 0;
                float supply = lab.IsEditing && lab.Physics != null ? lab.Physics.PreviewSupply() : state.SupplyRatio;
                resources.text = "营养 " + state.Nutrients.ToString("0.00") + "/" + data.nutrientCapacity +
                    "  ·  能量 " + state.Energy.ToString("0.00") + "/" + data.energyCapacity +
                    "  ·  供能 " + (requested > 0 ? supply.ToString("P0") : "无活动需求") +
                    "  ·  捕获 " + (lab.Food != null ? lab.Food.CapturedTotal : 0).ToString("0.00") + " / 消化 " + state.Ingested.ToString("0.00") +
                    "  ·  产能 " + state.ProductionRate.ToString("0.00") + "/秒" +
                    "  ·  活动耗能 " + (requested * supply).ToString("0.00") + "/秒  ·  颗粒 " + (lab.Food != null ? lab.Food.Particles.Count : 0) + "/40";
                resources.color = requested > 0 && supply < 0.2f ? new Color(1, 0.65f, 0.35f) : new Color(0.75f, 0.95f, 0.78f);
            }
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
            feedbackRect.sizeDelta = new Vector2(configure ? 356 : 600, 28);
            membraneTrialButton.gameObject.SetActive(!configure);
            membraneTrialButton.interactable = lab.IsEditing;
            feedback.fontSize = configure ? 14 : 17;
            feedback.text = lab.Message;
            if (lab.Physics != null && (!lab.IsEditing || lab.Physics.ActiveMask != 0))
            {
                var force = CellForceSummary.Calculate(lab);
                string turn = Mathf.Abs(force.Torque) < 0.001f ? "平衡" : force.Torque > 0 ? "逆时针" : "顺时针";
                feedback.text = (lab.IsEditing ? "预览 " : "施力 ") + (input.Length == 0 ? "无" : input.TrimEnd()) +
                    " · 激活 " + force.ActiveCilia + " · 合力 " + (configure ? force.Force.magnitude.ToString("0.00") :
                    "(" + force.Force.x.ToString("0.00") + ", " + force.Force.y.ToString("0.00") + ")") +
                    " · " + turn + " " + Mathf.Abs(force.Torque).ToString("0.00");
            }
            CoreButton.interactable = CiliaButton.interactable = AbsorberButton.interactable = MembraneButton.interactable = ContractorButton.interactable = lab.IsEditing && lab.Cells.Count < lab.Capacity;
            FoodButton.interactable = lab.PrimaryCore != null && lab.Food != null && lab.Food.Particles.Count < Emerge.World.NutrientWorld.Capacity;
            ResetButton.interactable = ClearButton.interactable = lab.IsEditing;
            ExampleButton.interactable = lab.IsEditing && lab.Capacity >= 3;
            disconnectButton.interactable = lab.IsEditing && (lab.Selected != null || edge != null);
            deleteButton.interactable = lab.IsEditing && lab.Selected != null;
            modeLabel.text = lab.IsEditing ? "开始游动 [Tab]" : "返回编辑 [Tab]";
            help.text = lab.IsEditing ? "拖拽连接 | Shift 补边 | Q/E 旋转 | X 拆开 | Delete 删除 | 3 示例 | 4 吸收 | 8 膜 | 9 膜对照 | 0 收缩 | C 收缩示例\n5 近处营养、7 滤食营养、6 流场；只配置核心出口。吸收先捕获后消化，收缩细胞按信号缩短相邻连接。" :
                "按住 W / A / S / D 激活对应核心分支  |  当前输入：" + (input.Length == 0 ? "无" : input) + "  |  Tab 返回编辑\n吸收细胞内绿色颗粒逐渐消化；收缩示例按 A 缩短、松开复原。5 近处营养，7 滤食营养，6 水流显示。";
        }

        private string ReceivedSignals(CellView cell)
        {
            if (lab.Physics == null) return "无";
            lab.Physics.Signals.Refresh(lab.Graph, lab.Cells, lab.PrimaryCore);
            string value = "";
            for (int c = 0; c < 4; c++)
            {
                float strength = lab.Physics.Signals.Strength(lab.PrimaryCore, cell, c);
                if (strength > 0) value += ((IntentChannel)c) + " " + strength.ToString("P0") + " ";
            }
            return value.Length == 0 ? "无" : value.TrimEnd();
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
