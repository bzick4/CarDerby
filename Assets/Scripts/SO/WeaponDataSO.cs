// Assets/Scripts/SO/WeaponDataSO.cs
using UnityEngine;

namespace CarDerby.SO
{
 
   /// <summary>
    /// One asset per weapon or scoop.
    /// Create via: Assets → Create → CarDerby → Weapon Data
    ///
    /// Roof weapons:  fill Weapon Stats block, leave Scoop Stats at defaults.
    /// Front scoops:  tick IsFrontScoop, fill Scoop Stats, leave Weapon Stats.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData_New", menuName = "CarDerby/Weapon Data")]
    public class WeaponDataSO : ScriptableObject
    {
        [Header("Identity")]
        public string  WeaponName;
        public Sprite  Icon;
        [TextArea]
        public string  Description;

        [Header("Prefab — gun model (child of car's weapon mount)")]
        public GameObject WeaponPrefab;

        [Header("Type")]
        public bool IsFrontScoop;

        // ── Roof weapon stats ──────────────────────────────────────────────────
        [Header("Weapon Stats  (roof weapons only)")]
        public float      Damage    = 15f;
        public float      FireRate  = 8f;   // shots per second
        [Tooltip("NetworkObject prefab registered in NetworkManager.NetworkPrefabs")]
        public GameObject ProjectilePrefab;

        // ── Scoop stats ────────────────────────────────────────────────────────
        [Header("Scoop Stats  (front scoops only)")]
        [Tooltip("Extra damage added to every ram hit")]
        public float BonusDamage       = 30f;
        [Tooltip("Max speed multiplier while scoop is equipped (0.7 = -30 %)")]
        public float SpeedFactor       = 0.70f;
        [Tooltip("Minimum seconds between successive collision hits")]
        public float CollisionCooldown = 0.40f;
    }
}
