// Assets/Scripts/Combat/BombProjectile.cs
using UnityEngine;

namespace CarDerby.Combat
{
    public sealed class BombProjectile : ProjectileBase
    {
        protected override void ConfigureRigidbody(Rigidbody rb)
        {
            rb.useGravity = true;
        }

        protected override void OnLifetimeExpired()
        {
            ApplySplashDamage(transform.position);
        }
    }
}
