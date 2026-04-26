using Unity.Netcode;
using UnityEngine;

public class WeaponAttachmentSystem : NetworkBehaviour
{
    [SerializeField] private WeaponData[] _availableWeapons;

    private VehicleAttachmentSockets _sockets;

    private void Awake() => _sockets = GetComponent<VehicleAttachmentSockets>();

    // Вызывается с индексом оружия из _availableWeapons
    [ServerRpc]
    public void AttachWeaponServerRpc(int weaponIndex, WeaponMountType mountType)
    {
        if (_availableWeapons == null || weaponIndex < 0 || weaponIndex >= _availableWeapons.Length)
        {
            Debug.LogError($"[WeaponAttachmentSystem] Неверный индекс оружия: {weaponIndex}");
            return;
        }

        WeaponData weapon = _availableWeapons[weaponIndex];
        if (weapon?.WeaponPrefab == null)
        {
            Debug.LogError($"[WeaponAttachmentSystem] У оружия {weapon?.WeaponName} нет префаба!");
            return;
        }

        Transform socket = _sockets?.GetSocket(mountType);
        if (socket == null)
        {
            Debug.LogError($"[WeaponAttachmentSystem] Сокет {mountType} не найден!");
            return;
        }

        var no = NetworkedObjectPool.Instance.GetNetworkObject(weapon.WeaponPrefab, socket.position, socket.rotation);
        if (no == null) return;

        no.transform.SetParent(socket, false);

        if (!no.IsSpawned)
            no.Spawn(true);
    }
}
