// Assets/Scripts/Car/ScoopModifier.cs
using UnityEngine;
using CarDerby.SO;

namespace CarDerby.Car
{
    /// <summary>
    /// Front scoop attachment. All tuning values come from a WeaponDataSO asset
    /// (IsFrontScoop = true) — nothing is hardcoded here.
    /// </summary>
    public class ScoopModifier : MonoBehaviour
    {
        [SerializeField] private WeaponDataSO _weaponData;

        private bool  _isEquipped;
        private float _lastHitTime;

        public bool  IsEquipped => _isEquipped;

        /// <summary>Speed multiplier applied to CarController when scoop is equipped.</summary>
        public float SpeedFactor => _isEquipped && _weaponData != null ? _weaponData.SpeedFactor : 1f;

        public void Equip()   => _isEquipped = true;
        public void Unequip() => _isEquipped = false;

        private void OnCollisionEnter(Collision collision)
        {
            if (!_isEquipped || _weaponData == null) return;

            float cooldown = _weaponData.CollisionCooldown;
            if (Time.time - _lastHitTime < cooldown) return;
            _lastHitTime = Time.time;

            var health = collision.gameObject.GetComponentInParent<Health.HealthSystem>();
            if (health == null) return;

            // Scale damage with impact speed for satisfying ramming hits
            float impactDamage = collision.relativeVelocity.magnitude * 2.5f + _weaponData.BonusDamage;

            var owner = GetComponentInParent<Unity.Netcode.NetworkObject>();
            ulong ownerId = owner != null ? owner.OwnerClientId : ulong.MaxValue;

            health.TakeDamage(impactDamage, ownerId);
        }
    }
}
