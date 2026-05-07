// Assets/Scripts/Car/CarPhysics.cs
using UnityEngine;
using CarDerby.SO;

namespace CarDerby.Car
{
    [System.Serializable]
    public class CarPhysicsSettings
    {
        public float MotorTorque      = 1500f;  // Нм на каждое ведущее колесо
        public float MaxSteerAngle    = 30f;    // градусы поворота передних колёс
        public float BrakeTorque      = 3000f;  // Нм торможения
        public float DownForce        = 500f;   // прижимная сила (устойчивость)
    }

    /// <summary>
    /// WheelCollider-based car physics.
    /// Назначь 4 WheelCollider'а и (опционально) 4 меша колёс в Inspector.
    /// CarController вызывает методы каждый FixedUpdate на сервере.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarPhysics : MonoBehaviour
    {
        [Header("Настройки")]
        [SerializeField] private CarPhysicsSettings _settings = new();

        [Header("WheelCollider'ы")]
        [SerializeField] private WheelCollider _frontLeft;
        [SerializeField] private WheelCollider _frontRight;
        [SerializeField] private WheelCollider _rearLeft;
        [SerializeField] private WheelCollider _rearRight;

        [Header("Меши колёс (необязательно — для визуала)")]
        [SerializeField] private Transform _meshFrontLeft;
        [SerializeField] private Transform _meshFrontRight;
        [SerializeField] private Transform _meshRearLeft;
        [SerializeField] private Transform _meshRearRight;

        private Rigidbody _rb;

        // CurrentSpeed в км/ч для UI (всегда положительный)
        public float CurrentSpeedKmh => _rb != null ? _rb.linearVelocity.magnitude * 3.6f : 0f;

        // Знаковая скорость: отрицательная при езде назад — для синхронизации вращения колёс
        public float CurrentSpeedKmhSigned => _rb != null
            ? Vector3.Dot(_rb.linearVelocity, transform.forward) * 3.6f
            : 0f;
        public float MaxSteerAngle   => _settings.MaxSteerAngle;
        public float MaxSpeedKmh     { get; private set; } = 120f;
        private float _baseMaxSpeedKmh = 120f;

        private float MaxSpeedMs => MaxSpeedKmh / 3.6f;

        /// <summary>Вызывается PlayerSpawner сразу после спавна — применяет данные из SO.</summary>
        public void Initialize(CarDataSO data)
        {
            if (data == null) return;

            // Rigidbody
            _rb.mass = data.MassKg;

            // Настройки физики
            _settings.MotorTorque   = data.MotorTorque;
            _settings.MaxSteerAngle = data.MaxSteerAngle;
            _settings.BrakeTorque   = data.BrakeTorque;
            _settings.DownForce     = data.DownForce;

            // Скорость
            _baseMaxSpeedKmh = data.MaxSpeedKmh;
            MaxSpeedKmh      = data.MaxSpeedKmh;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.centerOfMass = new Vector3(0f, -0.35f, 0f);
        }

        // Накопленный угол вращения колеса (градусы) — для анимации на клиенте
        private float _wheelRotationAngle;

        private void Update()
        {
            if (_frontLeft != null && _frontLeft.enabled)
            {
                // Сервер: берём позу из WheelCollider (учитывает подвеску)
                UpdateWheelMesh(_frontLeft,  _meshFrontLeft);
                UpdateWheelMesh(_frontRight, _meshFrontRight);
                UpdateWheelMesh(_rearLeft,   _meshRearLeft);
                UpdateWheelMesh(_rearRight,  _meshRearRight);
            }
            else
            {
                // Клиент: анимируем меши вручную по NetworkVariable данным
                UpdateWheelMeshesClient();
            }
        }

        /// <summary>Вызывается на клиенте — анимирует колёса без WheelCollider.</summary>
        public void UpdateWheelMeshesClient(float steerAngle = 0f, float speedKmh = 0f)
        {
            // Вращение колёс по скорости
            float wheelRadius  = (_frontLeft != null) ? _frontLeft.radius : 0.35f;
            float speedMs      = speedKmh / 3.6f;
            float degreesPerSec = (speedMs / (2f * Mathf.PI * wheelRadius)) * 360f;
            _wheelRotationAngle += degreesPerSec * Time.deltaTime;

            // Передние колёса — поворот руля + вращение
            ApplyWheelTransform(_meshFrontLeft,  steerAngle,  _wheelRotationAngle);
            ApplyWheelTransform(_meshFrontRight, steerAngle,  _wheelRotationAngle);
            // Задние колёса — только вращение
            ApplyWheelTransform(_meshRearLeft,   0f,          _wheelRotationAngle);
            ApplyWheelTransform(_meshRearRight,  0f,          _wheelRotationAngle);
        }

        private void ApplyWheelTransform(Transform mesh, float steerAngle, float rotAngle)
        {
            if (mesh == null) return;
            mesh.localRotation = Quaternion.Euler(rotAngle, steerAngle, 0f);
        }

        // ── Управление ───────────────────────────────────────────────────────

        public void ApplyThrottle(float input, float speedCap)
        {
            bool reversing = input < 0f;
            const float ReverseCapMs = 15f / 3.6f;
            float cap = reversing
                ? ReverseCapMs
                : Mathf.Min(speedCap, MaxSpeedMs);

            // Текущая скорость вдоль оси машины (отрицательная = едем назад)
            float forwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);
            float currentSpeed = reversing ? -forwardSpeed : forwardSpeed;

            float torque = 0f;
            if (currentSpeed < cap)
                torque = input * _settings.MotorTorque;

            // Задний привод
            if (_rearLeft  != null) _rearLeft.motorTorque  = torque;
            if (_rearRight != null) _rearRight.motorTorque = torque;

            // Если нет газа — убираем мотор чтобы не мешал торможению
            if (_frontLeft  != null) _frontLeft.motorTorque  = 0f;
            if (_frontRight != null) _frontRight.motorTorque = 0f;

            // Снимаем тормоза
            SetBrakeTorque(0f);

            // Прижимная сила — устойчивость на скорости
            _rb.AddForce(-transform.up * _settings.DownForce * _rb.linearVelocity.magnitude);
        }

        /// <summary>Вызывается каждый FixedUpdate — жёстко ограничивает скорость назад.</summary>
        public void ClampReverseSpeed()
        {
            const float ReverseCapMs = 15f / 3.6f;
            float fwdSpd = Vector3.Dot(_rb.linearVelocity, transform.forward);
            if (fwdSpd < -ReverseCapMs)
                _rb.linearVelocity -= transform.forward * (fwdSpd + ReverseCapMs);
        }

        public void ApplySteering(float input)
        {
            float angle = input * _settings.MaxSteerAngle;
            if (_frontLeft  != null) _frontLeft.steerAngle  = angle;
            if (_frontRight != null) _frontRight.steerAngle = angle;
        }

        public void ApplyBrake(float strength)
        {
            // Убираем газ с задних колёс
            if (_rearLeft  != null) _rearLeft.motorTorque  = 0f;
            if (_rearRight != null) _rearRight.motorTorque = 0f;

            SetBrakeTorque(_settings.BrakeTorque * strength);
        }

        /// <summary>Применяет штраф скорости от надетого оружия.</summary>
        public void SetWeaponSpeedPenalty(float penaltyPercent)
        {
            float multiplier = 1f - Mathf.Clamp01(penaltyPercent / 100f);
            MaxSpeedKmh = _baseMaxSpeedKmh * multiplier;
        }

        /// <summary>Вызывается DriftSystem для снижения бокового сцепления.</summary>
        public void SetLateralFrictionMultiplier(float multiplier)
        {
            SetWheelFriction(_rearLeft,  multiplier);
            SetWheelFriction(_rearRight, multiplier);
        }

        // ── Вспомогательные ──────────────────────────────────────────────────

        private void SetBrakeTorque(float torque)
        {
            if (_frontLeft  != null) _frontLeft.brakeTorque  = torque;
            if (_frontRight != null) _frontRight.brakeTorque = torque;
            if (_rearLeft   != null) _rearLeft.brakeTorque   = torque;
            if (_rearRight  != null) _rearRight.brakeTorque  = torque;
        }

        private void SetWheelFriction(WheelCollider wheel, float multiplier)
        {
            if (wheel == null) return;
            var friction = wheel.sidewaysFriction;
            friction.stiffness = Mathf.Clamp(multiplier, 0.1f, 1f);
            wheel.sidewaysFriction = friction;
        }

        private void UpdateWheelMesh(WheelCollider collider, Transform mesh)
        {
            // На клиенте WheelCollider отключён — GetWorldPose вернёт мусор, пропускаем
            if (collider == null || mesh == null || !collider.enabled) return;
            collider.GetWorldPose(out Vector3 pos, out Quaternion rot);
            mesh.SetPositionAndRotation(pos, rot);
        }
    }
}
