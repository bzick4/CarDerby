// Assets/Scripts/Combat/DamageSystem.cs
using UnityEngine;
using Unity.Netcode;

namespace CarDerby.Combat
{
    /// <summary>
    /// Central server-side damage router.
    /// Provides a single choke-point for any future anti-cheat validation
    /// (rate limiting, max-per-frame caps, etc.) without touching individual weapons.
    /// </summary>
    public class DamageSystem : NetworkBehaviour
    {
        public static DamageSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        // ── Server-only direct call (projectiles, collisions) ─────────────────

        public void ApplyDamage(Health.HealthSystem target, float amount, ulong sourceClientId)
        {
            if (!IsServer || target == null) return;
            target.TakeDamage(amount, sourceClientId);
        }

        // ── RPC entry point (kept for edge cases; projectiles prefer direct call) ──

        /// <param name="targetRef">NetworkObjectReference of the car being hit.</param>
        [ServerRpc(RequireOwnership = false)]
        public void ApplyDamageServerRpc(
            NetworkObjectReference targetRef,
            float amount,
            ulong sourceClientId)
        {
            if (!targetRef.TryGet(out NetworkObject netObj)) return;
            var health = netObj.GetComponentInChildren<Health.HealthSystem>();
            ApplyDamage(health, amount, sourceClientId);
        }
    }
}
