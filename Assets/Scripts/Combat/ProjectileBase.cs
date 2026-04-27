// Assets/Scripts/Combat/ProjectileBase.cs
using UnityEngine;
using Unity.Netcode;

namespace CarDerby.Combat
{
    /// <summary>
    /// Server-side projectile. Spawned by WeaponController on the server,
    /// travels forward, despawns on hit or lifetime expiry.
    /// Damage is injected via Initialize() so every weapon SO can set its own value.
    /// </summary>
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody))]
    public abstract class ProjectileBase : NetworkBehaviour
    {
        protected abstract float Speed    { get; }
        protected abstract float Lifetime { get; }

        private float  _damage;
        private ulong  _ownerClientId;
        private float  _spawnTime;
        private Rigidbody _rb;

        /// <summary>Call immediately after NetworkObject.Spawn() on the server.</summary>
        public void Initialize(ulong ownerClientId, float damage)
        {
            _ownerClientId = ownerClientId;
            _damage        = damage;
        }

        public override void OnNetworkSpawn()
        {
            _rb = GetComponent<Rigidbody>();
            _spawnTime = Time.time;

            if (IsServer)
            {
                _rb.useGravity    = false;
                _rb.linearVelocity = transform.forward * Speed;
            }
        }

        private void Update()
        {
            if (!IsServer) return;
            if (Time.time - _spawnTime > Lifetime)
                NetworkObject.Despawn();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            if (other.transform.IsChildOf(transform.root)) return;

            var health = other.GetComponentInParent<Health.HealthSystem>();
            if (health != null)
                health.TakeDamage(_damage, _ownerClientId);

            NetworkObject.Despawn();
        }
    }
}
