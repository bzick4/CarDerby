// Assets/Scripts/Combat/BombLauncher.cs
using UnityEngine;

namespace CarDerby.Combat
{
    /// <summary>
    /// Бомбомёт — аналог ракетницы: одиночный выстрел с AoE взрывом при попадании.
    /// FireRate в SO задаёт кулдаун (например 0.25 = раз в 4 сек).
    /// </summary>
    public class BombLauncher : WeaponController
    {
        [Header("BombLauncher FX")]
        [SerializeField] private ParticleSystem _launchEffect;
        [SerializeField] private AudioSource    _launchAudio;

        protected override void PlayMuzzleEffect(Transform muzzle)
        {
            if (_launchEffect != null) _launchEffect.Play();
            if (_launchAudio  != null) _launchAudio.PlayOneShot(_launchAudio.clip);
        }
    }
}
