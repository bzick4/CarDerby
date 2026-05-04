// Assets/Scripts/Combat/ProjectileBase.cs
using UnityEngine;
using Unity.Netcode;

namespace CarDerby.Combat
{
    /// <summary>
    /// Серверный снаряд. Спавнится WeaponController на сервере,
    /// летит вперёд, дестроится при попадании или истечении времени жизни.
    ///
    /// Подклассы переопределяют OnHit() для особого поведения
    /// (взрыв с радиусом, гравитация и т.д.).
    /// </summary>
    [RequireComponent(typeof(NetworkObject), typeof(Rigidbody))]
    public abstract class ProjectileBase : NetworkBehaviour
    {
        private float  _speed    = 30f;
        private float  _lifetime = 2f;
        private float  _damage;
        private float  _explosionRadius;
        private ulong  _ownerClientId;
        private float  _spawnTime;

        protected Rigidbody Rb { get; private set; }

        /// <summary>Вызывается сразу после NetworkObject.Spawn() на сервере.</summary>
        public void Initialize(ulong ownerClientId, float damage,
                               float speed = 30f, float lifetime = 2f, float explosionRadius = 0f)
        {
            _ownerClientId  = ownerClientId;
            _damage         = damage;
            _speed          = speed;
            _lifetime       = lifetime;
            _explosionRadius = explosionRadius;
        }

        public override void OnNetworkSpawn()
        {
            Rb = GetComponent<Rigidbody>();
            _spawnTime = Time.time;

            // Gravity и velocity применяем НА ВСЕХ клиентах — визуально пуля летит у всех.
            // Физика клиента независима от сервера, но за 1-2 секунды жизни снаряда
            // расхождение незаметно. Урон считается только на сервере.
            ConfigureRigidbody(Rb);
            Launch();
        }

        /// <summary>Применяет начальную скорость. Вызывается из OnNetworkSpawn и как резерв из Start.</summary>
        private void Launch()
        {
            if (Rb == null) return;
            Rb.isKinematic      = false;
            Rb.linearVelocity   = transform.forward * _speed;
        }

        /// <summary>Настройка физики — переопредели в подклассе (напр. включить гравитацию).</summary>
        protected virtual void ConfigureRigidbody(Rigidbody rb)
        {
            rb.useGravity = false;
        }

        private void Update()
        {
            if (!IsServer) return;
            if (Time.time - _spawnTime > _lifetime)
            {
                OnLifetimeExpired();
                if (NetworkObject.IsSpawned) NetworkObject.Despawn();
            }
        }

        /// <summary>Вызывается при истечении времени жизни (до Despawn). Переопредели для взрыва.</summary>
        protected virtual void OnLifetimeExpired() { }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;
            // Не бьём свою машину
            if (other.transform.IsChildOf(transform.root)) return;

            OnHit(other);

            if (NetworkObject.IsSpawned) NetworkObject.Despawn();
        }

        /// <summary>Переопредели для нестандартного поведения при попадании.</summary>
        protected virtual void OnHit(Collider other)
        {
            if (_explosionRadius > 0f)
                ApplySplashDamage(transform.position);
            else
                ApplyDirectDamage(other);
        }

        protected void ApplyDirectDamage(Collider other)
        {
            var health = other.GetComponentInParent<Health.HealthSystem>();
            if (health != null)
                health.TakeDamage(_damage, _ownerClientId);
        }

        protected void ApplySplashDamage(Vector3 center)
        {
            var hits = Physics.OverlapSphere(center, _explosionRadius);
            foreach (var hit in hits)
            {
                var health = hit.GetComponentInParent<Health.HealthSystem>();
                if (health == null) continue;

                // Урон убывает с расстоянием
                float dist   = Vector3.Distance(center, hit.transform.position);
                float factor = 1f - Mathf.Clamp01(dist / _explosionRadius);
                health.TakeDamage(_damage * factor, _ownerClientId);
            }
        }
    }
}
