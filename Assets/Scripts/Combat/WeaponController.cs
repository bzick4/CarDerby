// Assets/Scripts/Combat/WeaponController.cs
using UnityEngine;
using Unity.Netcode;
using CarDerby.SO;

namespace CarDerby.Combat
{
    /// <summary>
    /// Базовый класс оружия — обычный MonoBehaviour (не NetworkBehaviour).
    ///
    /// Архитектура:
    ///   • Владелец вызывает Fire() и AimAt() каждый кадр через PlayerInputHandler.
    ///   • Fire() рассчитывает позицию дула и роутит запрос на сервер через CarController.FireProjectileServerRpc.
    ///   • Сервер спавнит NetworkObject-снаряд.
    ///   • Не-владельцы в LateUpdate применяют _netWeaponYaw из CarController для анимации поворота.
    ///
    /// Настройка в префабе оружия:
    ///   _weaponMount   — Transform, который вращается к цели (обычно корень префаба оружия).
    ///   _muzzlePoints  — точки вылета снаряда. Если пусто — авто-поиск дочерних "MuzzlePoint*".
    ///                    Если не нашёл — использует корень самого оружия.
    /// </summary>
    public abstract class WeaponController : MonoBehaviour, IWeapon
    {
        [SerializeField] protected WeaponDataSO _weaponData;
        [SerializeField] protected Transform    _weaponMount;  // вращается к цели
        [SerializeField] protected Transform[]  _muzzlePoints; // точки стрельбы

        // Кэш компонентов с корня машины
        private NetworkObject              _rootNetObj;
        private CarDerby.Car.CarController _carController;

        protected float   _lastFireTime;
        protected Vector3 _aimPoint;
        private int       _muzzleIndex;

        // ── Свойства ─────────────────────────────────────────────────────────

        public WeaponDataSO WeaponData => _weaponData;

        public float Damage   => _weaponData != null ? _weaponData.Damage   : 0f;
        public float FireRate => _weaponData != null ? _weaponData.FireRate  : 1f;

        /// <summary>Можно ли выстрелить прямо сейчас (кулдаун по FireRate).</summary>
        public virtual bool CanFire => Time.time - _lastFireTime >= 1f / Mathf.Max(0.01f, FireRate);

        protected bool  IsOwner       => _rootNetObj != null && _rootNetObj.IsOwner;
        protected bool  IsServer      => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        protected ulong OwnerClientId => _rootNetObj != null ? _rootNetObj.OwnerClientId : 0;

        // ── IWeapon ──────────────────────────────────────────────────────────

        public void SetWeaponData(WeaponDataSO data) => _weaponData = data;

        public void AimAt(Vector3 worldPoint)
        {
            _aimPoint = worldPoint;
            if (!IsOwner || _weaponMount == null) return;
            Vector3 dir = worldPoint - _weaponMount.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                _weaponMount.rotation = Quaternion.LookRotation(dir.normalized);
        }

        /// <summary>
        /// Вызывается PlayerInputHandler каждый кадр пока кнопка зажата.
        /// Базовая реализация: один выстрел с кулдауном по FireRate.
        /// </summary>
        public virtual void Fire()
        {
            if (!IsOwner || !CanFire) return;
            _lastFireTime = Time.time;

            Transform muzzle = GetNextMuzzle();
            PlayMuzzleEffect(muzzle);
            RequestFire(muzzle);
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        protected virtual void Awake()
        {
            _rootNetObj    = GetComponentInParent<NetworkObject>();
            _carController = GetComponentInParent<CarDerby.Car.CarController>();

            if (_muzzlePoints == null || _muzzlePoints.Length == 0)
                AutoFindMuzzlePoints();
        }

        /// <summary>
        /// Синхронизация поворота у не-владельцев через NetworkVariable.
        /// Вызывается каждый LateUpdate на всех клиентах кроме владельца.
        /// </summary>
        protected virtual void LateUpdate()
        {
            if (IsOwner) return;
            if (_weaponMount == null || _carController == null) return;
            _weaponMount.rotation = Quaternion.Euler(0f, _carController.WeaponYaw, 0f);
        }

        // ── Muzzle management ────────────────────────────────────────────────

        /// <summary>Round-robin по всем muzzle-точкам. Переопредели для random (RocketLauncher).</summary>
        protected virtual Transform GetNextMuzzle()
        {
            if (_muzzlePoints == null || _muzzlePoints.Length == 0) return transform;
            var m = _muzzlePoints[_muzzleIndex % _muzzlePoints.Length];
            _muzzleIndex = (_muzzleIndex + 1) % _muzzlePoints.Length;
            return m != null ? m : transform;
        }

        protected Transform GetRandomMuzzle()
        {
            if (_muzzlePoints == null || _muzzlePoints.Length == 0) return transform;
            return _muzzlePoints[Random.Range(0, _muzzlePoints.Length)] ?? transform;
        }

        private void AutoFindMuzzlePoints()
        {
            var found = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in GetComponentsInChildren<Transform>(includeInactive: true))
            {
                if (child == transform) continue;
                if (child.name.StartsWith("MuzzlePoint") ||
                    child.name.StartsWith("Muzzle"))
                    found.Add(child);
            }
            if (found.Count > 0)
            {
                _muzzlePoints = found.ToArray();
                Debug.Log($"[WeaponController] Auto-found {found.Count} muzzle points на '{gameObject.name}'");
            }
        }

        // ── Fire routing ─────────────────────────────────────────────────────

        /// <summary>Роутит выстрел из Transform дула к точке прицела (3D, учитывает высоту дула).</summary>
        protected void RequestFire(Transform muzzle)
        {
            Vector3 toTarget = _aimPoint - muzzle.position;
            Quaternion rot = toTarget.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(toTarget.normalized)
                : muzzle.rotation;
            RequestFire(muzzle.position, rot);
        }

        /// <summary>
        /// Роутит выстрел: если сервер — спавним прямо, иначе — ServerRpc через CarController.
        /// </summary>
        protected void RequestFire(Vector3 pos, Quaternion rot)
        {
            if (_weaponData == null || _weaponData.ProjectilePrefab == null) return;

            if (IsServer)
            {
                DoServerSpawn(pos, rot);
            }
            else
            {
                _carController?.FireProjectileServerRpc(
                    pos, rot,
                    Damage,
                    _weaponData.ProjectileSpeed,
                    _weaponData.ProjectileLifetime,
                    _weaponData.ExplosionRadius);
            }
        }

        /// <summary>Server-only: создаёт и спавнит снаряд.</summary>
        public void DoServerSpawn(Vector3 pos, Quaternion rot)
        {
            if (_weaponData?.ProjectilePrefab == null) return;

            var obj    = Instantiate(_weaponData.ProjectilePrefab, pos, rot);
            var netObj = obj.GetComponent<NetworkObject>();
            if (netObj == null) { Destroy(obj); return; }

            // Initialize ДО Spawn — OnNetworkSpawn увидит правильные значения скорости
            var proj = obj.GetComponent<ProjectileBase>();
            proj?.Initialize(OwnerClientId, Damage,
                             _weaponData.ProjectileSpeed,
                             _weaponData.ProjectileLifetime,
                             _weaponData.ExplosionRadius);

            netObj.SpawnWithOwnership(OwnerClientId, destroyWithScene: true);
        }

        // ── Overridable ──────────────────────────────────────────────────────

        /// <summary>Визуальный эффект выстрела (вспышка, звук). Запускается у владельца.</summary>
        protected virtual void PlayMuzzleEffect(Transform muzzle) { }
    }
}
