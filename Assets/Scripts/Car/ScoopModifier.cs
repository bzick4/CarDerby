// Assets/Scripts/Car/ScoopModifier.cs
using UnityEngine;
using CarDerby.SO;

namespace CarDerby.Car
{
    /// <summary>
    /// Front scoop attachment. All tuning values come from WeaponDataSO (IsFrontScoop = true).
    ///
    /// Damage model:
    ///   - Enemy HP loss  = MaxScoopDamage × (currentSpeed / maxSpeed)
    ///   - Scoop HP loss  = random(ScoopDamageMinPercent, ScoopDamageMaxPercent) % of ScoopMaxHealth
    ///   - When scoop HP reaches 0 it breaks: no more damage, SpeedFactor returns to 1.
    /// </summary>
    public class ScoopModifier : MonoBehaviour
    {
        [SerializeField] private WeaponDataSO _weaponData;

        private bool  _isEquipped;
        private bool  _isBroken;
        private float _lastHitTime;
        private float _currentHp;

        public bool IsEquipped => _isEquipped && !_isBroken;

        /// <summary>Speed cap multiplier for CarController. Returns 1 if scoop is broken or not equipped.</summary>
        public float SpeedFactor => IsEquipped && _weaponData != null ? _weaponData.SpeedFactor : 1f;

        /// <summary>0–1 fraction of scoop HP remaining (for UI).</summary>
        public float HpPercent => _weaponData != null && _weaponData.ScoopMaxHealth > 0f
            ? _currentHp / _weaponData.ScoopMaxHealth
            : 0f;

        public void SetWeaponData(WeaponDataSO data)
        {
            _weaponData = data;
            _currentHp  = data != null ? data.ScoopMaxHealth : 100f;
            _isBroken   = false;
        }

        public void Equip()   => _isEquipped = true;
        public void Unequip() => _isEquipped = false;

        private void Detach()
        {
            // Inherit world velocity from the car before unparenting
            var carRb = GetComponentInParent<Rigidbody>();
            transform.SetParent(null, worldPositionStays: true);

            var rb = GetComponent<Rigidbody>();
            if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = false;
            if (carRb != null)
                rb.linearVelocity = carRb.linearVelocity;
        }

        /// <summary>Called by CarController.OnCollisionEnter — child colliders don't receive collision events directly.</summary>
        public void HandleCollision(Collision collision)
        {
            if (!IsEquipped || _weaponData == null) return;
            if (Unity.Netcode.NetworkManager.Singleton == null ||
                !Unity.Netcode.NetworkManager.Singleton.IsServer) return;

            // Урон только когда враг находится спереди ковша
            // Проверяем позицию врага, а не точку контакта — при лобовом таране точка прямо на ковше и вектор ≈ 0
            Vector3 toEnemy = collision.transform.position - transform.position;
            if (Vector3.Dot(toEnemy.normalized, transform.forward) < 0.4f) return;

            if (Time.time - _lastHitTime < _weaponData.CollisionCooldown) return;
            _lastHitTime = Time.time;

            var health = collision.gameObject.GetComponentInParent<Health.HealthSystem>();
            if (health == null) return;

            var carPhysics = GetComponentInParent<CarPhysics>();
            float speedPercent = (carPhysics != null && carPhysics.MaxSpeedKmh > 0f)
                ? Mathf.Clamp01(carPhysics.CurrentSpeedKmh / carPhysics.MaxSpeedKmh)
                : 1f;

            float enemyDamage = _weaponData.MaxScoopDamage * speedPercent;
            var netObj = GetComponentInParent<Unity.Netcode.NetworkObject>();
            ulong ownerId = netObj != null ? netObj.OwnerClientId : ulong.MaxValue;
            health.TakeDamage(enemyDamage, ownerId);

            float wearPercent = Random.Range(_weaponData.ScoopDamageMinPercent, _weaponData.ScoopDamageMaxPercent);
            float scoopDamage = _weaponData.ScoopMaxHealth * (wearPercent / 100f);
            _currentHp = Mathf.Max(0f, _currentHp - scoopDamage);

            Debug.Log($"[Scoop] Hit: enemy -{enemyDamage:F1} HP | scoop -{scoopDamage:F1} HP | left: {_currentHp:F1} (speed {speedPercent*100f:F0}%)");

            if (_currentHp <= 0f)
            {
                _isBroken = true;
                Debug.Log("[Scoop] Ковш сломан.");
                Detach();
            }
        }
    }
}
