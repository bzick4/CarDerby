using Unity.Netcode;
using UnityEngine;

public class WeaponAttachmentSystem : NetworkBehaviour
{
    private VehicleAttachmentSockets _sockets;

    private void Awake() => _sockets = GetComponent<VehicleAttachmentSockets>();

    [ServerRpc]
    public void AttachWeaponServerRpc(NetworkObjectReference weaponPrefabRef, WeaponMountType mountType)
    {
        if (!weaponPrefabRef.TryGet(out NetworkObject prefabNO)) return;

        Transform socket = _sockets.GetSocket(mountType);
        if (socket == null) return;

        // Используем наш новый пул
        var spawnedNO = NetworkedObjectPool.Instance.GetNetworkObject(prefabNO.gameObject, socket.position, socket.rotation);
        spawnedNO.transform.SetParent(socket, false);

        spawnedNO.Spawn(true); // Spawn с destroyWithOwner
    }
}