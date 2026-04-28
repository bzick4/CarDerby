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
        [SerializeField] private Camera                  _playerCamera;

        private static readonly int _groundMask = ~0;

        private void Awake()
        {
            if (_carController == null) _carController = GetComponentInChildren<Car.CarController>();
            if (_playerCamera  == null) _playerCamera  = GetComponentInChildren<Camera>();
            // WeaponController не ищем здесь — оружие спавнится позже чем Awake
        }

        private void Update()
        {
            // Оружие появляется после спавна машины — подхватываем лениво
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

            if (_carController != null)
                _carController.SubmitInputServerRpc(throttle, steering, braking, nitro, drifting);

            // ── Прицеливание ──────────────────────────────────────────────────
            if (_weaponController != null && _playerCamera != null)
            {
                Vector2 screenPos = mouse.position.ReadValue();
                Ray ray = _playerCamera.ScreenPointToRay(screenPos);
                if (Physics.Raycast(ray, out RaycastHit hit, 200f, _groundMask))
                    _weaponController.AimAt(hit.point);
            }

            if (firing && _weaponController != null)
                _weaponController.Fire();
        }
    }
}
