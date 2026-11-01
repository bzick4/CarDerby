// Assets/Scripts/UI/MobileInputUI.cs
using UnityEngine;
using UnityEngine.UIElements;

namespace CarDerby.UI
{
    /// <summary>
    /// Virtual joystick + buttons for mobile play.
    /// Initialized by GameHUD once the UIDocument root is available.
    /// Read by PlayerInputHandler when running on a touchscreen device.
    /// </summary>
    public class MobileInputUI : MonoBehaviour
    {
        // ── Read by PlayerInputHandler ───────────────────────────────────────
        public float Throttle  { get; private set; }
        public float Steering  { get; private set; }
        public bool  IsBraking { get; private set; }
        public bool  IsNitro   { get; private set; }
        public bool  IsFiring  { get; private set; }

        private const float JoystickRadius = 45f;

        private VisualElement _container;
        private VisualElement _joystickZone;
        private VisualElement _joystickKnob;

        private int     _joystickPointerId = -1;
        private Vector2 _joystickCenter;

        // ── Init called by GameHUD ───────────────────────────────────────────

        private void Start()
        {
            var root = GetComponent<UnityEngine.UIElements.UIDocument>()?.rootVisualElement;
            if (root != null) Init(root);
        }

        public void Init(VisualElement hudRoot)
        {
            _container    = hudRoot.Q<VisualElement>("mobile-controls");
            _joystickZone = hudRoot.Q<VisualElement>("joystick-zone");
            _joystickKnob = hudRoot.Q<VisualElement>("joystick-knob");

            if (_container == null)
            {
                Debug.LogError("[MobileInputUI] 'mobile-controls' не найден в UIDocument. Проверь HUD.uxml.");
                return;
            }

            BindJoystick();
            BindButton("brake-btn", v => IsBraking = v);
            BindButton("nitro-btn", v => IsNitro   = v);
            BindButton("fire-btn",  v => IsFiring  = v);
        }

        public void Show()
        {
            if (_container == null)
            {
                var root = GetComponent<UnityEngine.UIElements.UIDocument>()?.rootVisualElement;
                if (root != null) Init(root);
            }
            _container?.RemoveFromClassList("hidden");
            Debug.Log("[MobileInputUI] Show() called, container=" + (_container != null ? "OK" : "NULL"));
        }

        public void Hide() => _container?.AddToClassList("hidden");

        // ── Joystick ─────────────────────────────────────────────────────────

        private void BindJoystick()
        {
            if (_joystickZone == null) return;

            _joystickZone.RegisterCallback<PointerDownEvent>(OnJoystickDown,   TrickleDown.TrickleDown);
            _joystickZone.RegisterCallback<PointerMoveEvent>(OnJoystickMove,   TrickleDown.TrickleDown);
            _joystickZone.RegisterCallback<PointerUpEvent>(OnJoystickUp,       TrickleDown.TrickleDown);
            _joystickZone.RegisterCallback<PointerCancelEvent>(_ => ResetJoystick(), TrickleDown.TrickleDown);
        }

        private void OnJoystickDown(PointerDownEvent e)
        {
            if (_joystickPointerId >= 0) return;
            _joystickPointerId = e.pointerId;
            _joystickZone.CapturePointer(e.pointerId);
            _joystickCenter = _joystickZone.worldBound.center;
            UpdateJoystick(e.position);
            e.StopPropagation();
        }

        private void OnJoystickMove(PointerMoveEvent e)
        {
            if (e.pointerId != _joystickPointerId) return;
            UpdateJoystick(e.position);
            e.StopPropagation();
        }

        private void OnJoystickUp(PointerUpEvent e)
        {
            if (e.pointerId != _joystickPointerId) return;
            _joystickZone.ReleasePointer(e.pointerId);
            ResetJoystick();
            e.StopPropagation();
        }

        private void UpdateJoystick(Vector2 panelPos)
        {
            Vector2 delta = panelPos - _joystickCenter;
            float   len   = delta.magnitude;
            if (len > JoystickRadius)
                delta = delta / len * JoystickRadius;

            // Panel Y grows downward: negative delta.y = upward drag = throttle forward
            Steering = delta.x / JoystickRadius;
            Throttle = -delta.y / JoystickRadius;

            if (_joystickKnob != null)
                _joystickKnob.style.translate = new StyleTranslate(new Translate(delta.x, delta.y, 0));
        }

        private void ResetJoystick()
        {
            _joystickPointerId = -1;
            Throttle = 0f;
            Steering = 0f;
            if (_joystickKnob != null)
                _joystickKnob.style.translate = new StyleTranslate(new Translate(0, 0, 0));
        }

        // ── Generic button binder ────────────────────────────────────────────

        private void BindButton(string name, System.Action<bool> setter)
        {
            var btn = _container?.Q<VisualElement>(name);
            if (btn == null) return;

            btn.RegisterCallback<PointerDownEvent>(_ => setter(true));
            btn.RegisterCallback<PointerUpEvent>(_ => setter(false));
            btn.RegisterCallback<PointerCancelEvent>(_ => setter(false));
            btn.RegisterCallback<PointerLeaveEvent>(_ => setter(false));
        }
    }
}
