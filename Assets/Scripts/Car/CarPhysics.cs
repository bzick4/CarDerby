// Assets/Scripts/Car/CarPhysics.cs
using UnityEngine;

namespace CarDerby.Car
{
    [System.Serializable]
    public class CarPhysicsSettings
    {
        public float AccelerationForce    = 3000f;
        public float SteeringTorque       = 1400f;
        public float LateralDampening     = 60f;   // reduces sliding under normal grip
        public float BrakeStrength        = 4f;
        public float SteeringSpeedThreshold = 4f;  // full steering only above this speed
    }

    /// <summary>
    /// Arcade-style Rigidbody physics (FlatOut 2 feel).
    /// Runs on the server; CarController feeds input every FixedUpdate.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarPhysics : MonoBehaviour
    {
        [SerializeField] private CarPhysicsSettings _settings = new();

        private Rigidbody _rb;

        public float CurrentSpeed => _rb != null ? _rb.linearVelocity.magnitude : 0f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            // Lower centre of mass prevents roll-over
            _rb.centerOfMass = new Vector3(0f, -0.35f, 0f);
        }

        public void ApplyThrottle(float input, float speedCap)
        {
            // Project velocity onto forward axis to check speed cap
            float forwardSpeed = Vector3.Dot(_rb.linearVelocity, transform.forward);
            if (forwardSpeed < speedCap)
                _rb.AddForce(transform.forward * (input * _settings.AccelerationForce), ForceMode.Force);

            // Kill sideways drift under normal (non-drift) conditions
            Vector3 lateralVel = transform.right * Vector3.Dot(_rb.linearVelocity, transform.right);
            _rb.AddForce(-lateralVel * _settings.LateralDampening, ForceMode.Force);
        }

        public void ApplySteering(float input)
        {
            // Scale steering by speed so the car doesn't spin on the spot
            float speedFactor = Mathf.Clamp01(CurrentSpeed / _settings.SteeringSpeedThreshold);
            float torque      = input * _settings.SteeringTorque * speedFactor;
            _rb.AddTorque(Vector3.up * torque, ForceMode.Force);
        }

        public void ApplyBrake(float strength)
        {
            _rb.linearVelocity = Vector3.MoveTowards(
                _rb.linearVelocity,
                Vector3.zero,
                _settings.BrakeStrength * strength * Time.fixedDeltaTime * 10f);
        }

        /// <summary>
        /// Called by DriftSystem to reduce lateral friction.
        /// multiplier 1 = normal grip, 0 = ice.
        /// </summary>
        public void SetLateralFrictionMultiplier(float multiplier)
        {
            // Override damping for this fixed frame; DriftSystem calls every FixedUpdate
            Vector3 lateralVel = transform.right * Vector3.Dot(_rb.linearVelocity, transform.right);
            _rb.AddForce(-lateralVel * (_settings.LateralDampening * multiplier), ForceMode.Force);
        }
    }
}
