// Assets/Scripts/Combat/BombProjectile.cs
namespace CarDerby.Combat
{
    public sealed class BombProjectile : ProjectileBase
    {
        protected override void OnHit(UnityEngine.Collider other)
        {
            ApplySplashDamage(transform.position);
        }

        protected override void OnLifetimeExpired()
        {
            ApplySplashDamage(transform.position);
        }
    }
}
