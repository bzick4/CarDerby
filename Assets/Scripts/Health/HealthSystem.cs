// Assets/Scripts/Health/HealthSystem.cs
using System;
using UnityEngine;
using Unity.Netcode;

namespace CarDerby.Health
{
    /// <summary>
    /// Server-authoritative HP component.
    /// currentHealth is a NetworkVariable so every client can read it for UI.
    /// ClientRpcs drive visual feedback on all clients (smoke, fire, death).
    /// </summary>
    public class HealthSystem : NetworkBehaviour
    {
        [SerializeField] private float _maxHealth = 100f;

        private readonly NetworkVariable<float> _currentHealth = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public float CurrentHealth  => _currentHealth.Value;
        public float MaxHealth      => _maxHealth;
        public float HealthPercent  => _maxHealth > 0f ? _currentHealth.Value / _maxHealth : 0f;
        public bool  IsDead         => _currentHealth.Value <= 0f;

        // Clients subscribe to these for UI / VFX updates.
        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action<ulong>        OnDeath;         // (killerClientId)

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                _currentHealth.Value = _maxHealth;

            // Mirror NetworkVariable changes into the C# event for local subscribers.
            _currentHealth.OnValueChanged += (_, newVal) =>
                OnHealthChanged?.Invoke(newVal, _maxHealth);
        }

        // ── Server-only public API ────────────────────────────────────────────

        /// <param name="sourceClientId">Who dealt the damage; ulong.MaxValue = environment.</param>
        public void TakeDamage(float amount, ulong sourceClientId = ulong.MaxValue)
        {
            if (!IsServer || IsDead) return;

            _currentHealth.Value = Mathf.Max(0f, _currentHealth.Value - amount);

            // RPC ensures clients that miss the NetworkVariable delta still react.
            TriggerFeedbackClientRpc(_currentHealth.Value);

            if (_currentHealth.Value <= 0f)
                HandleDeathServerSide(sourceClientId);
        }

        public void Heal(float amount)
        {
            if (!IsServer) return;
            _currentHealth.Value = Mathf.Min(_maxHealth, _currentHealth.Value + amount);
        }

        // ── Internal ─────────────────────────────────────────────────────────

        private void HandleDeathServerSide(ulong killerId)
        {
            OnDeath?.Invoke(killerId);
            HandleDeathClientRpc(killerId);
        }

        [ClientRpc]
        private void TriggerFeedbackClientRpc(float newHealth)
        {
            OnHealthChanged?.Invoke(newHealth, _maxHealth);
        }

        [ClientRpc]
        private void HandleDeathClientRpc(ulong killerId)
        {
            OnDeath?.Invoke(killerId);
        }
    }
}
