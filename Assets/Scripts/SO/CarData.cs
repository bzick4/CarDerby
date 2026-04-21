using UnityEngine;

[CreateAssetMenu(menuName = "Derby/CarData")]
public class CarData : ScriptableObject
{
    [SerializeField] private string _guid;

    public string Guid => _guid;
    public string CarName;
    public GameObject CarPrefab;
    public float MaxSpeed = 50f;
    public float Acceleration = 30f;
    public float Handling = 80f;

    public WeaponMountType[] AllowedMounts;   // теперь enum виден

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(_guid))
            _guid = System.Guid.NewGuid().ToString();
    }
}