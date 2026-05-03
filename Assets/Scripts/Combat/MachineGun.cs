// Assets/Scripts/Combat/MachineGun.cs
using UnityEngine;

namespace CarDerby.Combat
{
    /// <summary>
    /// Миниган — автоматический огонь пока зажата кнопка.
    /// • Два дула (_muzzlePoints[0] и [1]) чередуются при каждом выстреле.
    /// • Перегрев: нагревается за OverheatDuration секунд → останавливается.
    /// • Остывание: только после полного перегрева, занимает CooldownDuration секунд.
    /// • Пассивное охлаждение: когда не стреляет — тепло медленно спадает.
    ///
    /// Настройка в префабе:
    ///   _muzzlePoints — назначь два дочерних GameObjects «MuzzlePoint_L» и «MuzzlePoint_R»,
    ///   или оставь пустым — авто-поиск по имени «MuzzlePoint*».
    ///   _muzzleFlashes — по одному ParticleSystem на каждую точку (опционально).
    /// </summary>
    public class MachineGun : WeaponController
    {
        [Header("MachineGun FX")]
        [SerializeField] private ParticleSystem[] _muzzleFlashes; // индексы совпадают с _muzzlePoints
        [SerializeField] private AudioSource      _fireAudio;

        // ── Overheat state (только на владельце) ─────────────────────────────
        private float _heatLevel;      // 0..1
        private bool  _isOverheated;
        private bool  _firingThisFrame;

        public float HeatLevel    => _heatLevel;
        public bool  IsOverheated => _isOverheated;

        // ── IWeapon overrides ─────────────────────────────────────────────────

        public override bool CanFire => !_isOverheated && base.CanFire;

        public override void Fire()
        {
            _firingThisFrame = true;     // отмечаем для Update
            base.Fire();                 // base проверяет CanFire (включая _isOverheated)
        }

        // ── Heat tracking (только у владельца) ───────────────────────────────

        private void Update()
        {
            if (!IsOwner) return;

            float overheatTime = _weaponData != null ? _weaponData.OverheatDuration : 5f;
            float cooldownTime = _weaponData != null ? _weaponData.CooldownDuration : 2f;

            if (_firingThisFrame && !_isOverheated)
            {
                // Нагреваемся пока стреляем
                _heatLevel += Time.deltaTime / overheatTime;
                if (_heatLevel >= 1f)
                {
                    _heatLevel    = 1f;
                    _isOverheated = true;
                    Debug.Log("[MachineGun] Перегрев! Ждём остывания...");
                }
            }
            else if (_isOverheated)
            {
                // Принудительное остывание после перегрева
                _heatLevel -= Time.deltaTime / cooldownTime;
                if (_heatLevel <= 0f)
                {
                    _heatLevel    = 0f;
                    _isOverheated = false;
                    Debug.Log("[MachineGun] Остыл, можно стрелять.");
                }
            }
            else
            {
                // Пассивное охлаждение когда не стреляем (в два раза медленнее нагрева)
                _heatLevel = Mathf.Max(0f, _heatLevel - Time.deltaTime / (overheatTime * 2f));
            }

            _firingThisFrame = false; // сбрасываем флаг до следующего кадра
        }

        // ── Visual FX ────────────────────────────────────────────────────────

        protected override void PlayMuzzleEffect(Transform muzzle)
        {
            // Ищем ParticleSystem у той же точки стрельбы
            if (_muzzleFlashes != null)
            {
                for (int i = 0; i < _muzzleFlashes.Length; i++)
                {
                    if (_muzzleFlashes[i] != null &&
                        _muzzlePoints != null && i < _muzzlePoints.Length &&
                        _muzzlePoints[i] == muzzle)
                    {
                        _muzzleFlashes[i].Play();
                        break;
                    }
                }
            }

            if (_fireAudio != null && !_fireAudio.isPlaying)
                _fireAudio.Play();
        }
    }
}
