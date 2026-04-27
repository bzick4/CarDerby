// Assets/Scripts/SO/CarDataSO.cs
using UnityEngine;

namespace CarDerby.SO
{
    /// <summary>
    /// One asset per car model. Create via:
    /// Assets → Create → CarDerby → Car Data
    /// </summary>
    [CreateAssetMenu(fileName = "CarData_New", menuName = "CarDerby/Car Data")]
    public class CarDataSO : ScriptableObject
    {
        [Header("Identity")]
        public string      CarName;
        public Sprite      PreviewSprite;
        [TextArea] public string Description;

        [Header("Prefab")]
        [Tooltip("NetworkObject prefab — must be registered in NetworkManager.NetworkPrefabs")]
        public GameObject  NetworkPrefab;

        [Header("Stats")]
        public float MaxHealth       = 100f;
        public float SpeedMultiplier = 1f;    // applied on top of CarController._baseMaxSpeed
        public float MassKg          = 1500f;
        public float AccelMultiplier = 1f;
    }
}
