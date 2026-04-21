using UnityEngine;

[CreateAssetMenu(menuName = "Derby/WeaponData")]
public class WeaponData : ScriptableObject
{
    public string WeaponName;
    public GameObject WeaponPrefab;
    public int Damage = 25;
    public float FireRate = 0.5f;
    public WeaponMountType MountType;        // теперь компилируется
}