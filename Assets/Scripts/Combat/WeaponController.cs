// Assets/Scripts/Combat/WeaponController.cs
using UnityEngine;
using Unity.Netcode;
using CarDerby.SO;

namespace CarDerby.Combat
{
    /// <summary>
    /// Abstract weapon — обычный MonoBehaviour (не NetworkBehaviour).
    ///
    /// NGO не поддерживает вложенные NetworkObject в динамических префабах,
    /// поэтому NetworkObject должен быть только на корневом объекте машины.
    /// WeaponController работает на стороне владельца (owner); RPCs для спавна
    /// снарядов идут через корневой NetworkObject машины.
    /// </summary>
    public abstract class WeaponController : MonoBehaviour, IWeapon
    {
        [SerializeField] protected WeaponDataSO _weaponData;
        [SerializeField] protected Transform    _weaponMount;   // вращается к курсору
        [SerializeField] protected Transform    _muzzlePoint;   // точка вылета снаряда

        // Корневой NetworkObject машины — находится автоматически при старте
        private NetworkObject _rootNetObj;

        private float _lastFireTime;

        // ── Свойства ─────────────────────────────────────────────────────────

        public float Damage   => _weaponData != null ? _weaponData.Damage  : 0f;
        public float FireRate => _weaponData != null ? _weaponData.FireRate : 1f;
        public bool  CanFire  => Time.time - _lastFireTime >= 1f / FireRate;

        /// <summary>true только на машине локального игрока.</summary>
        protected bool IsOwner => _rootNetObj != null && _rootNetObj.IsOwner;

        /// <summary>ClientId владельца машины.</summary>
        protected ulong OwnerClientId => _rootNetObj != null ? _rootNetObj.OwnerClientId : 0;

        /// <summary>true только на сервере/хосте.</summary>
        protected bool IsServer => _rootNetObj != null && _rootNetObj.IsOwnedByServer
                                   || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);

        // ── Lifecycle ────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            // Ищем NetworkObject на корневом объекте иерархии машины
            _rootNetObj = GetComponentInParent<NetworkObject>();
        }

        /// <summary>
        /// Вызывается PlayerSpawner сразу после спавна машины —
        /// подставляет нужный SO на основе выбора игрока в лобби.
        /// </summary>
        public void SetWeaponData(WeaponDataSO data) => _weaponData = data;

        // ── IWeapon ──────────────────────────────────────────────────────────

        public void AimAt(Vector3 worldPoint)
        {
            if (!IsOwner) return;

            if (_weaponMount != null)
            {
                // Считаем направление от позиции самой башни, а не корня оружия
                Vector3 pivot = _weaponMount.position;
                Vector3 dir   = worldPoint - pivot;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    _weaponMount.rotation = Quaternion.LookRotation(dir.normalized);
            }
        }

        public void Fire()
        {
            if (!IsOwner || !CanFire) return;

            _lastFireTime = Time.time;
            SpawnProjectile();
            PlayMuzzleEffect();
        }

        // ── Overrideable ─────────────────────────────────────────────────────

        /// <summary>
        /// Спавн снаряда на сервере. Вызывается только у владельца.
        /// Реализация должна использовать NetworkObject.Spawn() для синхронизации.
        /// </summary>
        protected abstract void SpawnProjectile();

        protected virtual void PlayMuzzleEffect() { }
    }
}
