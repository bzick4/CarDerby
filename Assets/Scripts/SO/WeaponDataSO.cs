// Assets/Scripts/SO/WeaponDataSO.cs
using UnityEngine;

namespace CarDerby.SO
{
    public enum FireMode
    {
        SemiAuto,   // Ракетница, бомбомёт — один выстрел, потом кулдаун
        Automatic,  // Миниган — держишь кнопку, перегревается
    }

    /// <summary>
    /// One asset per weapon or scoop.
    /// Create via: Assets → Create → CarDerby → Weapon Data
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData_New", menuName = "CarDerby/Weapon Data")]
    public class WeaponDataSO : ScriptableObject
    {
        [Header("Identity")]
        public string WeaponName;
        public Sprite Icon;
        [TextArea]
        public string Description;

        [Header("Prefab — модель пушки (дочерний объект WeaponSlot на машине)")]
        public GameObject WeaponPrefab;

        [Header("Type")]
        public bool IsFrontScoop;

        // ── Weapon Stats ───────────────────────────────────────────────────────
        [Header("Fire Mode")]
        public FireMode Mode     = FireMode.SemiAuto;

        [Header("Weapon Stats")]
        [Tooltip("Урон за попадание")]
        public float Damage      = 15f;
        [Tooltip("SemiAuto: выстрелов в секунду. Ракетница=0.5, Бомбомёт=0.25, Миниган=10")]
        public float FireRate    = 8f;

        [Header("Automatic (Minigun)")]
        [Tooltip("Секунды непрерывной стрельбы до перегрева")]
        public float OverheatDuration = 5f;
        [Tooltip("Секунды остывания после перегрева")]
        public float CooldownDuration = 2f;

        [Header("Projectile")]
        [Tooltip("NetworkObject-префаб снаряда, зарегистрированный в NetworkManager")]
        public GameObject ProjectilePrefab;
        [Tooltip("Скорость снаряда (м/с). Пуля≈60, Ракета≈40, Бомба≈15")]
        public float ProjectileSpeed    = 60f;
        [Tooltip("Время жизни снаряда (сек). 60м/с × 1.5с = 90м")]
        public float ProjectileLifetime = 1.5f;
        [Tooltip("Радиус взрыва (0 = прямой урон без AoE)")]
        public float ExplosionRadius    = 0f;

        // ── Movement Penalty ──────────────────────────────────────────────────
        [Header("Movement Penalty")]
        [Tooltip("На сколько % снижает максимальную скорость машины (0 = нет штрафа, 30 = −30%)")]
        [Range(0f, 100f)]
        public float SpeedPenaltyPercent = 0f;

        // ── Scoop Stats ────────────────────────────────────────────────────────
        [Header("Scoop Stats  (front scoops only)")]
        [Tooltip("Максимальный урон противнику при 100% скорости")]
        public float MaxScoopDamage    = 15f;
        [Tooltip("Множитель скорости машины пока ковш цел (0.7 = −30%)")]
        public float SpeedFactor       = 0.70f;
        [Tooltip("Кулдаун между засчитанными ударами (сек)")]
        public float CollisionCooldown = 0.40f;
        [Tooltip("HP самого ковша")]
        public float ScoopMaxHealth    = 100f;
        [Tooltip("Мин. урон ковшу за удар (% от ScoopMaxHealth)")]
        [Range(0f, 100f)]
        public float ScoopDamageMinPercent = 5f;
        [Tooltip("Макс. урон ковшу за удар (% от ScoopMaxHealth)")]
        [Range(0f, 100f)]
        public float ScoopDamageMaxPercent = 10f;
        [Tooltip("На сколько % увеличивает макс. здоровье машины (0 = нет бонуса)")]
        [Range(0f, 100f)]
        public float BonusHealthPercent = 0f;
    }
}
