// Assets/Scripts/Health/VisualFeedbackController.cs
using UnityEngine;

namespace CarDerby.Health
{
    /// <summary>
    /// Drives damage-state particle effects on all clients.
    /// Subscribes to HealthSystem.OnHealthChanged; no NetworkBehaviour required.
    /// </summary>
    public class VisualFeedbackController : MonoBehaviour
    {
        [SerializeField] private HealthSystem    _healthSystem;
        [SerializeField] private ParticleSystem  _smokeParticles; // < 50 % HP
        [SerializeField] private ParticleSystem  _fireParticles;  // < 15 % HP
        [SerializeField] private GameObject      _deathExplosionPrefab;

        private void Awake()
        {
            _healthSystem.OnHealthChanged += HandleHealthChanged;
            _healthSystem.OnDeath         += HandleDeath;
        }

        private void OnDestroy()
        {
            _healthSystem.OnHealthChanged -= HandleHealthChanged;
            _healthSystem.OnDeath         -= HandleDeath;
        }

        private void HandleHealthChanged(float current, float max)
        {
            float pct = max > 0f ? current / max : 0f;
            SetParticle(_smokeParticles, pct is > 0f and < 0.5f);
            SetParticle(_fireParticles,  pct is > 0f and < 0.15f);
        }

        private void HandleDeath(ulong _)
        {
            SetParticle(_smokeParticles, false);
            SetParticle(_fireParticles,  false);

            if (_deathExplosionPrefab != null)
                Instantiate(_deathExplosionPrefab, transform.position, Quaternion.identity);
        }

        private static void SetParticle(ParticleSystem ps, bool active)
        {
            if (ps == null) return;
            if (active  && !ps.isPlaying) ps.Play();
            if (!active && ps.isPlaying)  ps.Stop();
        }
    }
}
