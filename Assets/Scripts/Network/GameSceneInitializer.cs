using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class GameSceneInitializer : NetworkBehaviour
{
    [SerializeField] private CarData[] _availableCars;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Хост спавнит свою машину сразу
            SpawnCarForClient(NetworkManager.LocalClientId, PlayerSessionData.SelectedCarGuid);
        }
        else
        {
            // Клиент просит сервер заспавнить его машину
            RequestSpawnServerRpc(new FixedString64Bytes(PlayerSessionData.SelectedCarGuid));
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpawnServerRpc(FixedString64Bytes carGuid, ServerRpcParams rpcParams = default)
    {
        SpawnCarForClient(rpcParams.Receive.SenderClientId, carGuid.ToString());
    }

    private void SpawnCarForClient(ulong clientId, string carGuid)
    {
        CarData car = FindCarByGuid(carGuid);
        if (car == null)
        {
            Debug.LogWarning($"[GameSceneInitializer] GUID '{carGuid}' не найден, берём первую машину.");
            car = _availableCars != null && _availableCars.Length > 0 ? _availableCars[0] : null;
        }

        if (car == null || car.CarPrefab == null)
        {
            Debug.LogError($"[GameSceneInitializer] Нет префаба для клиента {clientId}!");
            return;
        }

        var instance = Instantiate(car.CarPrefab);
        var no = instance.GetComponent<NetworkObject>();

        if (no == null)
        {
            Debug.LogError($"[GameSceneInitializer] На префабе {car.CarName} нет NetworkObject!");
            Destroy(instance);
            return;
        }

        no.SpawnAsPlayerObject(clientId, true);

        var controller = instance.GetComponent<VehicleController>();
        controller?.SetCarData(car);

        Debug.Log($"[GameSceneInitializer] Заспавнена {car.CarName} для клиента {clientId}");
    }

    private CarData FindCarByGuid(string guid)
    {
        if (string.IsNullOrEmpty(guid) || _availableCars == null) return null;
        foreach (var c in _availableCars)
            if (c != null && c.Guid == guid) return c;
        return null;
    }
}
