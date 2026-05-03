// Assets/Scripts/Combat/Projectiles.cs
using UnityEngine;

namespace CarDerby.Combat
{
    // ── Пуля (Minigun) ──────────────────────────────────────────────────────
    // Добавь этот скрипт на префаб Bullet в папке Ammo.
    public sealed class BulletProjectile : ProjectileBase { }

    // ── Ракета (RocketLauncher) ─────────────────────────────────────────────
    // Добавь этот скрипт на префаб Rocket_Ammo в папке Ammo.
    // При истечении времени — тоже взрыв.
    public sealed class RocketProjectile : ProjectileBase
    {
        protected override void OnLifetimeExpired()
        {
            ApplySplashDamage(transform.position);
        }
    }

    // ── Бомба (BombLauncher) ────────────────────────────────────────────────
    // Добавь этот скрипт на префаб Ammo_Bomb.
    // Использует гравитацию, взрыв при контакте и по таймеру.
    public sealed class BombProjectile : ProjectileBase
    {
        protected override void ConfigureRigidbody(Rigidbody rb)
        {
            rb.useGravity = true;   // бомба падает
        }

        protected override void OnLifetimeExpired()
        {
            ApplySplashDamage(transform.position);
        }
    }
}
