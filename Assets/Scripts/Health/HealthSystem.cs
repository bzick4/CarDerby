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
        // Оба значения — NetworkVariable, чтобы клиенты видели данные из SO
        private readonly NetworkVariable<float> _maxHealth = new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> _currentHealth = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public float CurrentHealth  => _currentHealth.Value;
        public float MaxHealth      => _maxHealth.Value;
        public float HealthPercent  => _maxHealth.Value > 0f ? _currentHealth.Value / _maxHealth.Value : 0f;
        public bool  IsDead         => _currentHealth.Value <= 0f;

        // Clients subscribe to these for UI / VFX updates.
        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action<ulong>        OnDeath;         // (killerClientId)

        /// <summary>Вызывается PlayerSpawner до SpawnWithOwnership — задаёт MaxHealth из SO.</summary>
        public void Initialize(float maxHealth)
        {
            // Сохраняем до спавна; OnNetworkSpawn подхватит это значение
            _pendingMaxHealth = Mathf.Max(1f, maxHealth);
        }

        private float _pendingMaxHealth = 100f;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _maxHealth.Value     = _pendingMaxHealth;
                _currentHealth.Value = _pendingMaxHealth;
            }

            // Mirror NetworkVariable changes into the C# event for local subscribers.
            _currentHealth.OnValueChanged += (_, newVal) =>
                OnHealthChanged?.Invoke(newVal, _maxHealth.Value);
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
            _currentHealth.Value = Mathf.Min(_maxHealth.Value, _currentHealth.Value + amount);
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
            OnHealthChanged?.Invoke(newHealth, _maxHealth.Value);
        }

        [ClientRpc]
        private void HandleDeathClientRpc(ulong killerId)
        {
            OnDeath?.Invoke(killerId);
        }
    }
}
