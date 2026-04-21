using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class RoofGunController : NetworkBehaviour
{
    [SerializeField] private Transform _gunTransform;
    [SerializeField] private InputActionReference _aimAction;
    [SerializeField] private InputActionReference _fireAction;

    private void OnNetworkSpawn()
    {
        if (!IsLocalPlayer) return;
        _aimAction.action.Enable();
        _fireAction.action.Enable();
        _aimAction.action.performed += ctx => Aim(ctx.ReadValue<Vector2>());
        _fireAction.action.performed += _ => FireServerRpc();
    }

    private void Aim(Vector2 input) => _gunTransform.Rotate(Vector3.up * input.x * 120f * Time.deltaTime);

    [ServerRpc]
    private void FireServerRpc() 
    {
        // Здесь будет пулл projectile — по запросу дам полный ProjectilePool
    }
}