// Assets/Scripts/Player/PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarDerby.Player
{
    public class PlayerInputHandler : MonoBehaviour
    {
        [SerializeField] private Car.CarController       _carController;
        [SerializeField] private Combat.WeaponController _weaponController;
        [SerializeField] private LayerMask _aimMask = ~0;

        // Force mobile controls in editor for testing
        [SerializeField] private bool _forceMobile;

        private bool           _isMobile;
        private UI.MobileInputUI _mobileInput;

        private void Awake()
        {
            if (_carController == null)
                _carController = GetComponentInChildren<Car.CarController>();
        }

        private void Start()
        {
            _isMobile = _forceMobile
                || SystemInfo.deviceType == DeviceType.Handheld
#if UNITY_ANDROID || UNITY_IOS
                || true
#endif
                ;

            if (_isMobile)
                ActivateMobileControls();
        }

        public void SetWeaponController(Combat.WeaponController controller)
        {
            _weaponController = controller;
        }

        private void Update()
        {
            if (_weaponController == null)
                _weaponController = GetComponentInChildren<Combat.WeaponController>();

            // Detect Unity Remote connecting after Start()
            if (!_isMobile && UnityEngine.InputSystem.Touchscreen.current != null)
            {
                _isMobile = true;
                ActivateMobileControls();
            }

            if (_isMobile)
                UpdateMobile();
            else
                UpdateKeyboard();
        }

        private void ActivateMobileControls()
        {
            _mobileInput = _mobileInput ?? FindAnyObjectByType<UI.MobileInputUI>();
            if (_mobileInput == null)
                Debug.LogError("[PlayerInputHandler] MobileInputUI not found! Add it to the HUD GameObject.");
            else
                _mobileInput.Show();
        }

        // ── Keyboard + mouse ─────────────────────────────────────────────────

        private void UpdateKeyboard()
        {
            var kb    = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            float throttle = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            float steering = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            bool  braking  = kb.spaceKey.isPressed;
            bool  nitro    = kb.leftShiftKey.isPressed;
            bool  drifting = kb.leftCtrlKey.isPressed;
            bool  firing   = mouse.leftButton.isPressed;

            var cam = Camera.main;
            float weaponYaw = 0f;

            if (cam != null)
            {
                Vector3 aimPoint = GetAimPoint(cam);

                Vector3 flatToAim = aimPoint - transform.position;
                flatToAim.y = 0f;
                if (flatToAim.sqrMagnitude > 0.001f)
                    weaponYaw = Quaternion.LookRotation(flatToAim.normalized).eulerAngles.y;

                _weaponController?.AimAt(aimPoint);
            }

            if (_carController != null)
                _carController.SubmitInputServerRpc(throttle, steering, braking, nitro, drifting, weaponYaw);

            if (firing && _weaponController != null)
                _weaponController.Fire();
        }

        // ── Touch / mobile ───────────────────────────────────────────────────

        private void UpdateMobile()
        {
            if (_mobileInput == null) return;

            float throttle = _mobileInput.Throttle;
            float steering = _mobileInput.Steering;
            bool  braking  = _mobileInput.IsBraking;
            bool  nitro    = _mobileInput.IsNitro;
            bool  firing   = _mobileInput.IsFiring;

            // Aim forward along the car's current heading
            float weaponYaw = transform.eulerAngles.y;
            Vector3 aimPoint = transform.position + transform.forward * 20f;
            _weaponController?.AimAt(aimPoint);

            if (_carController != null)
                _carController.SubmitInputServerRpc(throttle, steering, braking, nitro, false, weaponYaw);

            if (firing && _weaponController != null)
                _weaponController.Fire();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Raycast from camera through cursor position.
        /// Own car is skipped; triggers are ignored.
        /// </summary>
        private Vector3 GetAimPoint(Camera cam)
        {
            Vector2 screenPos = Mouse.current.position.ReadValue();
            Ray     ray       = cam.ScreenPointToRay(screenPos);

            var hits = Physics.RaycastAll(ray, 500f, _aimMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(transform)) continue;
                return hit.point;
            }

            return ray.GetPoint(300f);
        }
    }
}
