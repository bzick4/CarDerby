// Assets/Scripts/Player/PlayerInputHandler.cs
using UnityEngine;

namespace CarDerby.Player
{
    /// <summary>
    /// Reads raw Unity legacy input and forwards it to the server-authoritative systems.
    /// Disabled automatically on non-owner NetworkBehaviours by PlayerNetwork.OnNetworkSpawn.
    /// Swap Input.GetAxis calls for the new Input System here without touching any other class.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [SerializeField] private Car.CarController      _carController;
        [SerializeField] private Combat.WeaponController _weaponController;
        [SerializeField] private Camera                 _playerCamera;

        private static readonly int _groundMask = ~0; // raycast all layers; tighten if needed

        private void Update()
        {
            float throttle  = Input.GetAxis("Vertical");
            float steering  = Input.GetAxis("Horizontal");
            bool  braking   = Input.GetKey(KeyCode.Space);
            bool  nitro     = Input.GetKey(KeyCode.LeftShift);
            bool  drifting  = Input.GetKey(KeyCode.LeftControl);
            bool  firing    = Input.GetMouseButton(0);

            _carController.SubmitInputServerRpc(throttle, steering, braking, nitro, drifting);

            // Aim weapon toward world-space cursor hit point
            if (_playerCamera != null)
            {
                Ray ray = _playerCamera.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit, 200f, _groundMask))
                    _weaponController.AimAt(hit.point);
            }

            if (firing) _weaponController.Fire();
        }
    }
}
