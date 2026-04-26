using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

public class RoofGunController : NetworkBehaviour
{
    [SerializeField] private Transform _gunTransform;
    [SerializeField] private Transform _muzzlePoint;
    [SerializeField] private InputActionReference _aimAction;
    [SerializeField] private InputActionReference _fireAction;
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float _fireRate = 0.5f;
    [SerializeField] private float _aimSpeed = 120f;

    private float _lastFireTime;

    public override void OnNetworkSpawn()
    {
        if (!IsLocalPlayer) return;

        _aimAction.action.Enable();
        _fireAction.action.Enable();
        _aimAction.action.performed += ctx => _aimInput = ctx.ReadValue<Vector2>();
        _aimAction.action.canceled += _ => _aimInput = Vector2.zero;
        _fireAction.action.performed += _ => TryFire();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsLocalPlayer) return;

        _aimAction.action.performed -= ctx => _aimInput = ctx.ReadValue<Vector2>();
        _aimAction.action.canceled -= _ => _aimInput = Vector2.zero;
        _fireAction.action.performed -= _ => TryFire();
        _aimAction.action.Disable();
        _fireAction.action.Disable();
    }

    private Vector2 _aimInput;

    private void Update()
    {
        if (!IsLocalPlayer || _gunTransform == null) return;
        _gunTransform.Rotate(Vector3.up * _aimInput.x * _aimSpeed * Time.deltaTime);
    }

    private void TryFire()
    {
        if (Time.time - _lastFireTime < _fireRate) return;
        _lastFireTime = Time.time;

        Transform origin = _muzzlePoint != null ? _muzzlePoint : _gunTransform != null ? _gunTransform : transform;
        FireServerRpc(origin.position, origin.rotation);
    }

    [ServerRpc]
    private void FireServerRpc(Vector3 position, Quaternion rotation)
    {
        if (_projectilePrefab == null)
        {
            Debug.LogWarning("[RoofGunController] _projectilePrefab не назначен!");
            return;
        }

        var no = NetworkedObjectPool.Instance.GetNetworkObject(_projectilePrefab, position, rotation);
        if (no == null) return;

        if (!no.IsSpawned)
            no.Spawn(true);
    }
}
