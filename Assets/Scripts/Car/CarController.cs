// Assets/Scripts/Car/CarController.cs
using UnityEngine;
using Unity.Netcode;

namespace CarDerby.Car
{
    /// <summary>
    /// Orchestrates all car sub-systems.
    /// Input flows: Owner → SubmitInputServerRpc → server FixedUpdate → physics moves → NetworkTransform syncs.
    ///
    /// На не-серверных клиентах:
    ///   - Rigidbody → isKinematic = true  (позицией управляет NetworkTransform)
    ///   - WheelColliders → disabled        (иначе трение блокирует NetworkTransform)
    ///   - Скорость читается из _netSpeed   (NetworkVariable, синхронизируется сервером)
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class CarController : NetworkBehaviour
    {
        [SerializeField] private CarPhysics    _physics;
        [SerializeField] private NitroSystem   _nitro;
        [SerializeField] private DriftSystem   _drift;
        [SerializeField] private ScoopModifier _scoop; // можно оставить пустым — PlayerSpawner пропишет динамически

        [SerializeField] private float _baseMaxSpeed = 25f;

        // Synced input — все клиенты могут читать для анимаций/VFX
        private readonly NetworkVariable<float> _netThrottle   = new(0f,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _netSteering   = new(0f,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool>  _netBraking    = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _netWeaponYaw  = new(0f,  NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Скорость синхронизируется сервером → клиент читает для HUD
        private readonly NetworkVariable<float> _netSpeed = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public float ThrottleValue => _netThrottle.Value;
        public float SteeringValue => _netSteering.Value;
        public bool  IsBraking     => _netBraking.Value;

        /// <summary>Скорость в км/ч — актуально на всех клиентах через NetworkVariable. Всегда ≥ 0 (для HUD).</summary>
        public float SpeedKmh => Mathf.Abs(_netSpeed.Value);

        /// <summary>Угол поворота оружия по Y (мировые градусы) — синхронизируется владельцем.</summary>
        public float WeaponYaw => _netWeaponYaw.Value;

        public void SetScoop(ScoopModifier scoop) { _scoop = scoop; }

        private void OnCollisionEnter(Collision collision)
        {
            _scoop?.HandleCollision(collision);
        }

        // ── NGO lifecycle ────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                var rb = GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = true;

                foreach (var wc in GetComponentsInChildren<WheelCollider>())
                    wc.enabled = false;
            }
        }

        private void Update()
        {
            // Анимация колёс на клиенте через NetworkVariables
            if (!IsServer && _physics != null)
            {
                float steerAngle = _netSteering.Value * _physics.MaxSteerAngle;
                _physics.UpdateWheelMeshesClient(steerAngle, _netSpeed.Value);
            }
        }

        // ── Client → Server ──────────────────────────────────────────────────

        /// <summary>
        /// Клиент-владелец запрашивает у сервера спавн снаряда.
        /// Вызывается из WeaponController.RequestFire() когда клиент не является сервером.
        /// </summary>
        [ServerRpc]
        public void FireProjectileServerRpc(
            Vector3    muzzlePos,
            Quaternion muzzleRot,
            float      damage,
            float      speed,
            float      lifetime,
            float      explosionRadius = 0f)
        {
            // Ищем оружие на этой машине (у сервера тоже есть локальная копия оружия)
            var weapon = GetComponentInChildren<Combat.WeaponController>();
            if (weapon != null)
            {
                // Используем данные от клиента (он знает своё дуло точнее)
                var data = weapon.WeaponData;
                if (data?.ProjectilePrefab != null)
                {
                    var obj    = Instantiate(data.ProjectilePrefab, muzzlePos, muzzleRot);
                    var netObj = obj.GetComponent<NetworkObject>();
                    if (netObj == null) { Destroy(obj); return; }

                    // Initialize ДО Spawn — OnNetworkSpawn увидит правильную скорость
                    var proj = obj.GetComponent<Combat.ProjectileBase>();
                    proj?.Initialize(OwnerClientId, damage, speed, lifetime, explosionRadius);

                    netObj.SpawnWithOwnership(OwnerClientId, destroyWithScene: true);
                }
            }
        }

        [ServerRpc]
        public void SubmitInputServerRpc(float throttle, float steering, bool braking, bool nitro, bool drifting, float weaponYaw = 0f)
        {
            _netThrottle.Value  = throttle;
            _netSteering.Value  = steering;
            _netBraking.Value   = braking;
            _netWeaponYaw.Value = weaponYaw;

            if (nitro)    _nitro.TryActivate();
            if (drifting) _drift.BeginDrift();
            else          _drift.EndDrift();

            _drift.SetSteering(steering);
        }

        // ── Server physics tick ──────────────────────────────────────────────

        private void FixedUpdate()
        {
            if (!IsServer) return;

            float speedCap = _baseMaxSpeed * _nitro.SpeedMultiplier * (_scoop != null ? _scoop.SpeedFactor : 1f);

            if (!_netBraking.Value)
                _physics.ApplyThrottle(_netThrottle.Value, speedCap);
            else
                _physics.ApplyBrake(1f);

            _physics.ApplySteering(_netSteering.Value);
            _physics.ClampReverseSpeed();
            _drift.TickDrift();

            // Синхронизируем знаковую скорость: отрицательная при езде назад → клиент правильно анимирует колёса
            _netSpeed.Value = _physics.CurrentSpeedKmhSigned;
        }
    }
}
