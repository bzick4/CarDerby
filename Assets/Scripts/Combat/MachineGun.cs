// Assets/Scripts/Combat/MachineGun.cs
using UnityEngine;
using Unity.Netcode;

namespace CarDerby.Combat
{
    /// <summary>
    /// Rapid-fire weapon. Все характеристики из WeaponDataSO.
    /// SpawnProjectile вызывается только у владельца, но спавнит через сервер.
    /// </summary>
    public class MachineGun : WeaponController
    {
        [SerializeField] private ParticleSystem _muzzleFlash;
        [SerializeField] private AudioSource    _fireAudio;

        protected override void SpawnProjectile()
        {
            // Только сервер/хост может спавнить NetworkObject
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            if (_weaponData == null || _weaponData.ProjectilePrefab == null) return;

            if (_muzzlePoint == null)
            {
                Debug.LogError("[MachineGun] MuzzlePoint не назначен.");
                return;
            }

            var obj    = Instantiate(_weaponData.ProjectilePrefab, _muzzlePoint.position, _muzzlePoint.rotation);
            var netObj = obj.GetComponent<NetworkObject>();

            if (netObj == null)
            {
                Debug.LogError("[MachineGun] ProjectilePrefab не имеет NetworkObject.");
                Destroy(obj);
                return;
            }

            netObj.SpawnWithOwnership(OwnerClientId, destroyWithScene: true);

            var projectile = obj.GetComponent<ProjectileBase>();
            if (projectile != null)
                projectile.Initialize(OwnerClientId, Damage);
        }

        protected override void PlayMuzzleEffect()
        {
            if (_muzzleFlash != null) _muzzleFlash.Play();
            if (_fireAudio   != null) _fireAudio.PlayOneShot(_fireAudio.clip);
        }
    }

    // ── Bullet ───────────────────────────────────────────────────────────────

    public sealed class BulletProjectile : ProjectileBase
    {
        protected override float Speed    => 40f;
        protected override float Lifetime => 1.5f;
    }
}
