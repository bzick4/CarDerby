// Assets/Scripts/Combat/RocketLauncher.cs
using UnityEngine;

namespace CarDerby.Combat
{
    /// <summary>
    /// Ракетница — одиночный выстрел с кулдауном (FireRate в SO = 0.5 → раз в 2 сек).
    /// Каждый выстрел из случайной точки массива _muzzlePoints.
    ///
    /// Настройка в префабе:
    ///   _muzzlePoints — добавь до 6 дочерних GameObjects «MuzzlePoint_1»…«MuzzlePoint_6»
    ///   и присвой их в инспекторе (или авто-поиск по имени).
    ///   _launchEffect — опциональный ParticleSystem вспышки пуска.
    ///   _launchAudio  — звук выстрела.
    /// </summary>
    public class RocketLauncher : WeaponController
    {
        [Header("RocketLauncher FX")]
        [SerializeField] private ParticleSystem _launchEffect;
        [SerializeField] private AudioSource    _launchAudio;

        // Всегда выбираем случайную точку
        protected override Transform GetNextMuzzle() => GetRandomMuzzle();

        protected override void PlayMuzzleEffect(Transform muzzle)
        {
            if (_launchEffect != null) _launchEffect.Play();
            if (_launchAudio  != null) _launchAudio.PlayOneShot(_launchAudio.clip);
        }
    }
}
