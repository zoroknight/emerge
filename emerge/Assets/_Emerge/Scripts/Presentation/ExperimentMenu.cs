using Emerge.Core;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Emerge.Presentation
{
    public sealed class ExperimentMenu : MonoBehaviour
    {
        private CellLabController lab;
        private GameObject overlay;
        private RectTransform rows;
        private InputField title, idea, notes;
        private Text controls, message;
        private Button update;
        private Toggle noContraction;
        private ExperimentEntry selected;
        private string displayedActiveId;
        public bool IsOpen => overlay != null && overlay.activeSelf;
        public InputField TitleField => title;
        public void Initialize(CellLabController controller)
        {
            lab = controller;
            var eventModule = EventSystem.current.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            eventModule.inputOverride = EventSystem.current.gameObject.AddComponent<ExperimentTextInput>();
            var entry = Button(lab.Panel.CanvasRoot, "组合实验 [B]", 1090, 658, 166, Open);
            overlay = Box(lab.Panel.CanvasRoot, "组合实验菜单", 0, 0, 1280, 720, new Color(0, 0, 0, .92f)).gameObject;
            var sheet = Box(overlay.transform, "实验库", 60, 72, 1160, 580, new Color(.06f, .12f, .17f));
            Label(sheet, "组合实验库 · 载入、修改、记录自己的想法", 24, 10, 930, 48, 26);
            Button(sheet, "关闭", 1030, 14, 100, Close);
            var viewport = Box(sheet, "列表", 24, 64, 350, 408, new Color(.03f,.08f,.12f));
            viewport.gameObject.AddComponent<RectMask2D>();
            rows = Box(viewport, "条目", 0, 0, 350, 408, Color.clear);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = rows;
            scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
            Label(sheet, "名称", 398, 64, 90, 30);
            title = Field(sheet, 486, 64, 640, 40, false);
            Label(sheet, "想法", 398, 116, 90, 30);
            idea = Field(sheet, 486, 114, 640, 100, true);
            controls = Label(sheet, "", 398, 228, 728, 64, 18);
            Label(sheet, "记录", 398, 302, 90, 30);
            notes = Field(sheet, 486, 300, 640, 94, true);
            noContraction = Box(sheet, "收缩对照", 398, 410, 32, 32, new Color(.16f,.28f,.35f)).gameObject.AddComponent<Toggle>();
            noContraction.targetGraphic = noContraction.GetComponent<Image>();
            noContraction.graphic = Box(noContraction.transform, "勾选", 7, 7, 18, 18, Color.cyan).GetComponent<Image>();
            Label(sheet, "关闭收缩响应（保持纤毛信号，载入后用于对照）", 444, 410, 680, 32, 18);
            Button(sheet, "载入选中实验", 24, 488, 180, LoadSelected);
            Button(sheet, "刷新列表", 216, 488, 158, RefreshRows);
            Button(sheet, "保存当前布局为新实验", 398, 456, 280, () => Save(false));
            update = Button(sheet, "更新选中的我的实验", 696, 456, 280, () => Save(true));
            message = Label(sheet, "", 398, 506, 728, 60, 16);
            Label(sheet, "保存的是当前场景布局与剩余资源、食物；\n关闭菜单后拖拽、Q/E 旋转、补边和改核心信号。", 24, 538, 350, 40, 14);
            overlay.SetActive(false);
        }
        public void Open()
        {
            if (!lab.IsEditing) lab.ToggleMode();
            lab.EndDrag(false); overlay.SetActive(true); RefreshRows();
            if (lab.Experiments.IsActive && displayedActiveId != lab.Experiments.Active.id)
                foreach (var entry in lab.Experiments.Entries) if (entry.data.id == lab.Experiments.Active.id) { Select(entry); break; }
            displayedActiveId = lab.Experiments.IsActive ? lab.Experiments.Active.id : null;
            if (selected == null && lab.Experiments.Entries.Count > 0) Select(lab.Experiments.Entries[0]);
            noContraction.SetIsOnWithoutNotify(lab.Experiments.DisableContraction);
            message.text = "先选条目再载入；保存按钮保存当前场景。\n" + lab.Experiments.ReadErrors;
        }
        public void Close() => overlay.SetActive(false);
        public void RefreshRows()
        {
            lab.Experiments.Refresh();
            foreach (Transform row in rows) { row.gameObject.SetActive(false); Destroy(row.gameObject); }
            int i = 0;
            foreach (var item in lab.Experiments.Entries)
            {
                var entry = item;
                Button(rows, (entry.IsCustom ? "我的 · " : "预设 · ") + entry.data.title, 6, i++ * 50 + 6, 338, () => Select(entry));
            }
            rows.sizeDelta = new Vector2(350, Mathf.Max(408, i * 50 + 12));
            rows.anchoredPosition = Vector2.zero;
        }
        private void Select(ExperimentEntry entry)
        {
            selected = entry; title.text = entry.data.title; idea.text = entry.data.idea; notes.text = entry.data.notes;
            noContraction.SetIsOnWithoutNotify(entry.data.disableContraction);
            controls.text = "操作：" + entry.data.controls; update.interactable = entry.IsCustom;
            message.text = entry.IsCustom ? "我的实验：可载入后修改，再更新此条目。" : "预设：载入后可保存为自己的副本。";
        }
        public void LoadSelected()
        {
            if (selected == null) return;
            if (lab.Experiments.Load(selected.data)) { lab.Experiments.DisableContraction = noContraction.isOn; Close(); }
            else message.text = lab.Message;
        }
        private void Save(bool overwrite)
        {
            if (overwrite && (selected == null || !selected.IsCustom)) return;
            bool previous = lab.Experiments.DisableContraction;
            lab.Experiments.DisableContraction = noContraction.isOn;
            if (lab.Experiments.Save(title.text, idea.text, notes.text, overwrite ? selected : null))
            {
                RefreshRows();
                foreach (var entry in lab.Experiments.Entries) if (entry.path == lab.Experiments.LastSavedPath) { Select(entry); break; }
            }
            else lab.Experiments.DisableContraction = previous;
            message.text = lab.Message;
        }
        private RectTransform Box(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(w,h); obj.GetComponent<Image>().color = color; return rect;
        }
        private Text Label(Transform parent, string text, float x, float y, float w, float h, int size = 19)
        {
            var obj = new GameObject("文字", typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1);
            rect.anchoredPosition = new Vector2(x,-y); rect.sizeDelta = new Vector2(w,h);
            var label = obj.GetComponent<Text>(); label.font = lab.Panel.UiFont; label.fontSize = size; label.color = Color.white;
            label.text = text; label.raycastTarget = false; label.verticalOverflow = VerticalWrapMode.Truncate; return label;
        }
        private Button Button(Transform parent, string text, float x, float y, float w, UnityAction action)
        {
            var rect = Box(parent,text,x,y,w,40,new Color(.12f,.25f,.32f)); var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>(); button.onClick.AddListener(action);
            var label = Label(rect,text,6,0,w-12,40,18); label.alignment = TextAnchor.MiddleCenter; return button;
        }
        private InputField Field(Transform parent,float x,float y,float w,float h,bool multiline)
        {
            var rect = Box(parent,"输入",x,y,w,h,new Color(.025f,.07f,.10f));
            var label = Label(rect,"",10,5,w-20,h-10,18); var field = rect.gameObject.AddComponent<InputField>();
            field.targetGraphic = rect.GetComponent<Image>(); field.textComponent = label;
            field.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            field.characterLimit = multiline ? 2000 : 80; return field;
        }
    }
}
