// Assets/Scripts/Combat/ShotgunController.cs
using UnityEngine;

namespace CarDerby.Combat
{
    /// <summary>
    /// Дробовик — один клик выстреливает из всех muzzle-точек одновременно.
    /// Кулдаун между залпами задаётся через FireRate в SO (0.2с = FireRate 5).
    /// </summary>
    public class ShotgunController : WeaponController
    {
        [Header("Shotgun FX")]
        [SerializeField] private ParticleSystem[] _muzzleFlashes;
        [SerializeField] private AudioSource      _fireAudio;

        public override void Fire()
        {
            if (!IsOwner || !CanFire) return;
            _lastFireTime = Time.time;

            int count = _muzzlePoints != null ? _muzzlePoints.Length : 0;

            if (count == 0)
            {
                PlayMuzzleEffect(transform);
                RequestFire(transform);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Transform muzzle = _muzzlePoints[i] != null ? _muzzlePoints[i] : transform;
                PlayMuzzleEffect(muzzle);
                RequestFire(muzzle);
            }
        }

        protected override void PlayMuzzleEffect(Transform muzzle)
        {
            if (_fireAudio != null) _fireAudio.PlayOneShot(_fireAudio.clip);

            if (_muzzleFlashes == null || _muzzleFlashes.Length == 0) return;
            int idx = System.Array.IndexOf(_muzzlePoints, muzzle);
            if (idx >= 0 && idx < _muzzleFlashes.Length && _muzzleFlashes[idx] != null)
                _muzzleFlashes[idx].Play();
        }
    }
}
