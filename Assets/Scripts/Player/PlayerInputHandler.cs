// Assets/Scripts/Player/PlayerInputHandler.cs
using UnityEngine;
using UnityEngine.InputSystem;

namespace CarDerby.Player
{
    /// <summary>
    /// Reads input via the new Input System and forwards it to server-authoritative systems.
    /// Disabled automatically on non-owner NetworkBehaviours by PlayerNetwork.OnNetworkSpawn.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [SerializeField] private Car.CarController       _carController;
        [SerializeField] private Combat.WeaponController _weaponController;

        private static readonly int _groundMask = ~0;

        private void Awake()
        {
            if (_carController == null) _carController = GetComponentInChildren<Car.CarController>();
            // WeaponController не ищем здесь — оружие спавнится позже Awake
        }

        /// <summary>Вызывается PlayerSpawner после спавна оружия.</summary>
        public void SetWeaponController(Combat.WeaponController controller)
        {
            _weaponController = controller;
        }

        private void Update()
        {
            // Lazy fallback: подхватываем если ещё не установлен через SetWeaponController
            if (_weaponController == null)
                _weaponController = GetComponentInChildren<Combat.WeaponController>();

            var kb    = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null || mouse == null) return;

            // ── Движение ─────────────────────────────────────────────────────
            float throttle = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            float steering = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            bool  braking  = kb.spaceKey.isPressed;
            bool  nitro    = kb.leftShiftKey.isPressed;
            bool  drifting = kb.leftCtrlKey.isPressed;
            bool  firing   = mouse.leftButton.isPressed;

            // ── Прицеливание — горизонтальное направление камеры, точка далеко впереди
            var cam = Camera.main;
            float weaponYaw = 0f;

            if (cam != null)
            {
                Vector3 flatForward = cam.transform.forward;
                flatForward.y = 0f;
                if (flatForward.sqrMagnitude > 0.001f)
                {
                    flatForward.Normalize();
                    weaponYaw = Quaternion.LookRotation(flatForward).eulerAngles.y;

                    if (_weaponController != null)
                    {
                        Vector3 aimPoint = transform.position + flatForward * 100f;
                        _weaponController.AimAt(aimPoint);
                    }
                }
            }

            if (_carController != null)
                _carController.SubmitInputServerRpc(throttle, steering, braking, nitro, drifting, weaponYaw);

            if (firing && _weaponController != null)
                _weaponController.Fire();
        }
    }
}
