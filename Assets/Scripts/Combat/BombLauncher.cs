// Assets/Scripts/Combat/BombLauncher.cs
using UnityEngine;

namespace CarDerby.Combat
{
    /// <summary>
    /// Бомбомёт — одиночный выстрел с медленным кулдауном (FireRate в SO = 0.25 → раз в 4 сек).
    /// Снаряд-бомба BombProjectile использует гравитацию и взрывается с радиусом AoE.
    /// Стреляет вперёд под углом возвышения _launchAngle.
    ///
    /// Настройка в префабе:
    ///   _muzzlePoints[0] — точка вылета бомбы (или добавь дочерний «MuzzlePoint»).
    ///   _launchAngle     — угол возвышения (30° по умолчанию).
    ///   _launchEffect    — вспышка (опционально).
    /// </summary>
    public class BombLauncher : WeaponController
    {
        [Header("BombLauncher Settings")]
        [SerializeField] [Range(0f, 80f)] private float _launchAngle = 30f;

        [Header("BombLauncher FX")]
        [SerializeField] private ParticleSystem _launchEffect;
        [SerializeField] private AudioSource    _launchAudio;

        public override void Fire()
        {
            if (!IsOwner || !CanFire) return;

            _lastFireTime = Time.time; // устанавливаем до спавна

            Transform muzzle = GetNextMuzzle();

            // Бомба летит вперёд+вверх под углом
            Quaternion launchRot = muzzle.rotation * Quaternion.Euler(-_launchAngle, 0f, 0f);

            PlayMuzzleEffect(muzzle);

            // Роутим на сервер с нашей custom rotation
            RequestFire(muzzle.position, launchRot);
        }

        protected override void PlayMuzzleEffect(Transform muzzle)
        {
            if (_launchEffect != null) _launchEffect.Play();
            if (_launchAudio  != null) _launchAudio.PlayOneShot(_launchAudio.clip);
        }
    }
}
