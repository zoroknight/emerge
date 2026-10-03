using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Emerge.Presentation
{
    // uGUI InputField still asks BaseInput for IME properties; avoid the disabled legacy Input API.
    public sealed class ExperimentTextInput : BaseInput
    {
        private Keyboard keyboard;
        private string composition = "";
        private IMECompositionMode mode;
        private Vector2 cursor;
        public override string compositionString => composition;
        public override bool touchSupported => false;
        public override IMECompositionMode imeCompositionMode
        {
            get => mode;
            set { mode = value; Bind(); keyboard?.SetIMEEnabled(value == IMECompositionMode.On); }
        }
        public override Vector2 compositionCursorPos
        {
            get => cursor;
            set { cursor = value; Bind(); keyboard?.SetIMECursorPosition(value); }
        }
        protected override void OnEnable() { base.OnEnable(); Bind(); }
        private void Update() => Bind();
        private void Bind()
        {
            if (keyboard == Keyboard.current) return;
            if (keyboard != null) keyboard.onIMECompositionChange -= Changed;
            keyboard = Keyboard.current; composition = "";
            if (keyboard != null) keyboard.onIMECompositionChange += Changed;
        }
        private void Changed(IMECompositionString value) => composition = value.ToString();
        protected override void OnDisable()
        {
            if (keyboard != null) keyboard.onIMECompositionChange -= Changed;
            keyboard = null; composition = ""; base.OnDisable();
        }
    }
}
