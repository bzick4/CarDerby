using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class CarSpawner : NetworkBehaviour
{
    [SerializeField] private CarData[] _availableCars;

    [ServerRpc(RequireOwnership = false)]
    public void SpawnSelectedCarServerRpc(FixedString64Bytes carGuid, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        CarData car = FindCarByGuid(carGuid.ToString());
        if (car == null)
        {
            Debug.LogWarning($"[CarSpawner] GUID '{carGuid}' не найден, берём первую машину.");
            car = _availableCars != null && _availableCars.Length > 0 ? _availableCars[0] : null;
        }

        if (car == null || car.CarPrefab == null)
        {
            Debug.LogError($"[CarSpawner] Нет префаба для клиента {clientId}!");
            return;
        }

        var instance = Instantiate(car.CarPrefab);
        var no = instance.GetComponent<NetworkObject>();

        if (no == null)
        {
            Debug.LogError($"[CarSpawner] На префабе {car.CarName} нет NetworkObject!");
            Destroy(instance);
            return;
        }

        no.SpawnAsPlayerObject(clientId, true);

        var controller = instance.GetComponent<VehicleController>();
        controller?.SetCarData(car);

        Debug.Log($"[CarSpawner] Заспавнена {car.CarName} для клиента {clientId}");
    }

    private CarData FindCarByGuid(string guid)
    {
        if (string.IsNullOrEmpty(guid) || _availableCars == null) return null;
        foreach (var car in _availableCars)
            if (car != null && car.Guid == guid) return car;
        return null;
    }
}
