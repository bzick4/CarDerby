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

        private void Awake()
        {
            if (_carController == null) _carController = GetComponentInChildren<Car.CarController>();
        }

        public void SetWeaponController(Combat.WeaponController controller)
        {
            _weaponController = controller;
        }

        private void Update()
        {
            if (_weaponController == null)
                _weaponController = GetComponentInChildren<Combat.WeaponController>();

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
                // Рейкаст через текущую позицию курсора-прицела
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

        /// <summary>
        /// Рейкаст из камеры через позицию курсора.
        /// Собственная машина пропускается, триггеры игнорируются.
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
