// Assets/Scripts/Combat/WeaponController.cs
using UnityEngine;
using Unity.Netcode;
using CarDerby.SO;

namespace CarDerby.Combat
{
    /// <summary>
    /// Abstract server-authoritative weapon.
    /// All stats (damage, fire rate, projectile prefab) come from a WeaponDataSO asset —
    /// no hardcoded values in code. Swap the SO asset to change weapon behaviour.
    ///
    /// Owner client → AimAtServerRpc / FireServerRpc → server resolves → ClientRpc for VFX.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public abstract class WeaponController : NetworkBehaviour, IWeapon
    {
        [SerializeField] protected WeaponDataSO _weaponData;
        [SerializeField] protected Transform    _weaponMount;  // rotates toward cursor
        [SerializeField] protected Transform    _muzzlePoint;  // projectile spawn point

        private readonly NetworkVariable<Quaternion> _aimRotation = new(
            Quaternion.identity,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private float _lastFireTime;

        // ── IWeapon — driven by SO ───────────────────────────────────────────

        public float Damage   => _weaponData != null ? _weaponData.Damage   : 0f;
        public float FireRate => _weaponData != null ? _weaponData.FireRate  : 1f;
        public bool  CanFire  => Time.time - _lastFireTime >= 1f / FireRate;

        public void AimAt(Vector3 worldPoint)
        {
            if (!IsOwner) return;
            AimAtServerRpc(worldPoint);
        }

        public void Fire()
        {
            if (!IsOwner || !CanFire) return;
            FireServerRpc();
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            _aimRotation.OnValueChanged += OnAimChanged;
        }

        public override void OnNetworkDespawn()
        {
            _aimRotation.OnValueChanged -= OnAimChanged;
        }

        private void OnAimChanged(Quaternion _, Quaternion current)
        {
            if (_weaponMount != null)
                _weaponMount.localRotation = current;
        }

        // ── RPCs ─────────────────────────────────────────────────────────────

        [ServerRpc]
        private void AimAtServerRpc(Vector3 worldPoint)
        {
            Vector3 dir = worldPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                _aimRotation.Value = Quaternion.LookRotation(dir.normalized);
        }

        [ServerRpc]
        public void FireServerRpc()
        {
            if (!CanFire) return;
            _lastFireTime = Time.time;
            SpawnProjectile();
            OnFiredClientRpc();
        }

        [ClientRpc]
        private void OnFiredClientRpc() => PlayMuzzleEffect();

        // ── Overrideable ─────────────────────────────────────────────────────

        protected abstract void SpawnProjectile();
        protected virtual  void PlayMuzzleEffect() { }
    }
}
