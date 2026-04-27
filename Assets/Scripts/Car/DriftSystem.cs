// Assets/Scripts/Car/DriftSystem.cs
using UnityEngine;

namespace CarDerby.Car
{
    /// <summary>
    /// Arcade drift: reduces lateral friction and applies yaw torque
    /// while the player holds the drift key.
    /// Runs on the server (FixedUpdate guarded by IsServer in CarController).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DriftSystem : MonoBehaviour
    {
        [SerializeField] private float        _yawTorque       = 900f;
        [SerializeField] private float        _gripMultiplier  = 0.18f; // fraction of normal grip kept
        [SerializeField] private TrailRenderer[] _tireTrails;

        private Rigidbody _rb;
        private bool  _isDrifting;
        private float _steeringInput;

        public bool IsDrifting => _isDrifting;

        private void Awake() => _rb = GetComponent<Rigidbody>();

        public void SetSteering(float steering) => _steeringInput = steering;

        public void BeginDrift()
        {
            if (_isDrifting) return;
            _isDrifting = true;
            SetTrails(true);
        }

        public void EndDrift()
        {
            if (!_isDrifting) return;
            _isDrifting = false;
            SetTrails(false);
        }

        // Called every FixedUpdate by CarController (server only).
        public void TickDrift()
        {
            if (!_isDrifting) return;

            // Bleed out most of the lateral velocity to create the slide feel
            Vector3 lat = transform.right * Vector3.Dot(_rb.linearVelocity, transform.right);
            _rb.linearVelocity -= lat * (1f - _gripMultiplier) * Time.fixedDeltaTime * 6f;

            // Extra yaw to whip the rear around
            _rb.AddTorque(Vector3.up * (_steeringInput * _yawTorque), ForceMode.Force);
        }

        private void SetTrails(bool on)
        {
            foreach (var t in _tireTrails)
                if (t != null) t.emitting = on;
        }
    }
}
