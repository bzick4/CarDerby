// Assets/Scripts/Car/NitroSystem.cs
using UnityEngine;

namespace CarDerby.Car
{
    /// <summary>
    /// Temporary speed multiplier with a cooldown.
    /// Runs on the server (owned by CarController); VFX plays on all clients
    /// via a NetworkVariable-driven state in CarController.
    /// </summary>
    public class NitroSystem : MonoBehaviour
    {
        [SerializeField] private float _multiplier  = 1.8f;
        [SerializeField] private float _duration    = 2f;
        [SerializeField] private float _cooldown    = 5f;
        [SerializeField] private ParticleSystem _exhaustParticles;

        private float _activeUntil;
        private float _readyAt;

        public bool  IsActive  => Time.time < _activeUntil;
        public bool  IsReady   => Time.time >= _readyAt && !IsActive;
        public float SpeedMultiplier => IsActive ? _multiplier : 1f;

        public void TryActivate()
        {
            if (!IsReady) return;
            _activeUntil = Time.time + _duration;
            _readyAt     = _activeUntil + _cooldown;

            if (_exhaustParticles != null) _exhaustParticles.Play();
        }

        private void Update()
        {
            if (!IsActive && _exhaustParticles != null && _exhaustParticles.isPlaying)
                _exhaustParticles.Stop();
        }
    }
}
