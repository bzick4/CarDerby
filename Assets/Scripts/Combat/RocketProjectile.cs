// Assets/Scripts/Combat/RocketProjectile.cs
namespace CarDerby.Combat
{
    public sealed class RocketProjectile : ProjectileBase
    {
        protected override void OnLifetimeExpired()
        {
            ApplySplashDamage(transform.position);
        }
    }
}
