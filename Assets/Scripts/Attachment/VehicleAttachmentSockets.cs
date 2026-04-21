using UnityEngine;
using Unity.Netcode;

public class VehicleAttachmentSockets : NetworkBehaviour
{
    // [SerializeField] private Transform _frontLeftWeaponSocket;
    // [SerializeField] private Transform _frontRightWeaponSocket;
    // [SerializeField] private Transform _rearLeftWeaponSocket;
    // [SerializeField] private Transform _rearRightWeaponSocket;
    [SerializeField] private Transform _roofGunSocket;
    [SerializeField] private Transform _scoopSocket;

    // public Transform FrontLeftWeaponSocket => _frontLeftWeaponSocket;
    // public Transform FrontRightWeaponSocket => _frontRightWeaponSocket;
    // public Transform RearLeftWeaponSocket => _rearLeftWeaponSocket;
    // public Transform RearRightWeaponSocket => _rearRightWeaponSocket;
    public Transform RoofGunSocket => _roofGunSocket;
    public Transform ScoopSocket => _scoopSocket;

    public Transform GetSocket(WeaponMountType type) => type switch
    {
        // WeaponMountType.FrontLeft => _frontLeftWeaponSocket,
        // WeaponMountType.FrontRight => _frontRightWeaponSocket,
        // WeaponMountType.RearLeft => _rearLeftWeaponSocket,
        // WeaponMountType.RearRight => _rearRightWeaponSocket,
        WeaponMountType.Roof => _roofGunSocket,
        WeaponMountType.Scoop => _scoopSocket,
        _ => null
    };
}