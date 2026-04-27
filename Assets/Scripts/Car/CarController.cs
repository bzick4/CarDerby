// Assets/Scripts/Car/CarController.cs
using UnityEngine;
using Unity.Netcode;

namespace CarDerby.Car
{
    /// <summary>
    /// Orchestrates all car sub-systems.
    /// Input flows: Owner → SubmitInputServerRpc → server FixedUpdate → physics moves → NetworkTransform syncs.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class CarController : NetworkBehaviour
    {
        [SerializeField] private CarPhysics   _physics;
        [SerializeField] private NitroSystem  _nitro;
        [SerializeField] private DriftSystem  _drift;
        [SerializeField] private ScoopModifier _scoop;

        [SerializeField] private float _baseMaxSpeed = 25f;

        // Synced so spectators / remote clients can animate wheels correctly
        private readonly NetworkVariable<float> _netThrottle  = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<float> _netSteering  = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<bool>  _netBraking   = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Convenience accessors for animation / VFX on all clients
        public float ThrottleValue  => _netThrottle.Value;
        public float SteeringValue  => _netSteering.Value;
        public bool  IsBraking      => _netBraking.Value;

        // ── Client → Server ──────────────────────────────────────────────────

        [ServerRpc]
        public void SubmitInputServerRpc(float throttle, float steering, bool braking, bool nitro, bool drifting)
        {
            _netThrottle.Value = throttle;
            _netSteering.Value = steering;
            _netBraking.Value  = braking;

            if (nitro)    _nitro.TryActivate();
            if (drifting) _drift.BeginDrift();
            else          _drift.EndDrift();

            _drift.SetSteering(steering);
        }

        // ── Server physics tick ──────────────────────────────────────────────

        private void FixedUpdate()
        {
            if (!IsServer) return;

            float speedCap = _baseMaxSpeed * _nitro.SpeedMultiplier * _scoop.SpeedFactor;

            if (!_netBraking.Value)
                _physics.ApplyThrottle(_netThrottle.Value, speedCap);
            else
                _physics.ApplyBrake(1f);

            _physics.ApplySteering(_netSteering.Value);
            _drift.TickDrift();
        }
    }
}
