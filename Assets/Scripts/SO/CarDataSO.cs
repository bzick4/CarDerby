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

        [Header("Health")]
        public float MaxHealth = 100f;

        [Header("Physics — Rigidbody")]
        public float MassKg    = 1500f;

        [Header("Physics — Engine")]
        public float MaxSpeedKmh   = 120f;   // максимальная скорость км/ч
        public float MotorTorque   = 1500f;  // Нм на каждое ведущее колесо
        public float BrakeTorque   = 3000f;

        [Header("Physics — Handling")]
        public float MaxSteerAngle = 30f;    // градусы поворота передних колёс
        public float DownForce     = 500f;   // прижимная сила
    }
}
