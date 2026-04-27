// Assets/Scripts/Combat/MachineGun.cs
using UnityEngine;
using Unity.Netcode;

namespace CarDerby.Combat
{
    /// <summary>
    /// Rapid-fire weapon. All stats (damage, fire rate, projectile prefab)
    /// come from the WeaponDataSO assigned in the Inspector — nothing is hardcoded here.
    /// </summary>
    public class MachineGun : WeaponController
    {
        [SerializeField] private ParticleSystem _muzzleFlash;
        [SerializeField] private AudioSource    _fireAudio;

        protected override void SpawnProjectile()
        {
            if (_weaponData == null || _weaponData.ProjectilePrefab == null)
            {
                Debug.LogError("[MachineGun] WeaponDataSO or ProjectilePrefab is not assigned.");
                return;
            }

            var obj = Instantiate(
                _weaponData.ProjectilePrefab,
                _muzzlePoint.position,
                _muzzlePoint.rotation);

            var netObj = obj.GetComponent<NetworkObject>();
            netObj.SpawnWithOwnership(OwnerClientId, destroyWithScene: true);

            // Pass damage from SO so the projectile doesn't need its own hardcoded value
            var projectile = obj.GetComponent<ProjectileBase>();
            projectile.Initialize(OwnerClientId, Damage);
        }

        protected override void PlayMuzzleEffect()
        {
            if (_muzzleFlash != null) _muzzleFlash.Play();
            if (_fireAudio   != null) _fireAudio.PlayOneShot(_fireAudio.clip);
        }
    }

    // ── Bullet projectile ─────────────────────────────────────────────────────

    /// <summary>
    /// Generic fast bullet. Speed and lifetime are the only values it owns;
    /// damage is injected by MachineGun via ProjectileBase.Initialize().
    /// </summary>
    public sealed class BulletProjectile : ProjectileBase
    {
        protected override float Speed    => 40f;
        protected override float Lifetime => 1.5f;
    }
}
