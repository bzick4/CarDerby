using Unity.Netcode;
using Unity.Collections;
using UnityEngine;

public class CarSpawner : NetworkBehaviour
{
    [SerializeField] private CarData[] _availableCars;   // ← протяни сюда все 3 машины

    // [ServerRpc(RequireOwnership = false)]
    // public void SpawnSelectedCarServerRpc(FixedString64Bytes carGuid)
    // {
    //     CarData selectedCar = FindCarByGuid(carGuid);
    //     if (selectedCar == null || selectedCar.CarPrefab == null)
    //     {
    //         Debug.LogError($"[CarSpawner] Не найдена машина с GUID: {carGuid}");
    //         return;
    //     }

    //     // Спавним выбранный префаб машины
    //     GameObject carInstance = Instantiate(selectedCar.CarPrefab);
    //     NetworkObject networkObject = carInstance.GetComponent<NetworkObject>();

    //     if (networkObject == null)
    //     {
    //         Debug.LogError($"[CarSpawner] На префабе {selectedCar.CarName} отсутствует NetworkObject!");
    //         Destroy(carInstance);
    //         return;
    //     }

    //     // Важно! Спавним как Player Object
    //     networkObject.SpawnAsPlayerObject(NetworkManager.Singleton.LocalClientId, true);

    //     // Передаём данные машине
    //     VehicleController controller = carInstance.GetComponent<VehicleController>();
    //     if (controller != null)
    //         controller.SetCarData(selectedCar);

    //     Debug.Log($"[CarSpawner] Успешно заспавнена машина: {selectedCar.CarName} для клиента {NetworkManager.Singleton.LocalClientId}");
    // }

    [ServerRpc(RequireOwnership = false)]
public void SpawnSelectedCarServerRpc(FixedString64Bytes carGuid)
{
    // Временный тест — всегда спавним первую машину
    if (_availableCars == null || _availableCars.Length == 0)
    {
        Debug.LogError("[CarSpawner] Нет машин в _availableCars!");
        return;
    }

    CarData selectedCar = _availableCars[0];   // берём первую машину

    GameObject carInstance = Instantiate(selectedCar.CarPrefab);
    NetworkObject no = carInstance.GetComponent<NetworkObject>();

    if (no == null)
    {
        Debug.LogError($"[CarSpawner] На префабе {selectedCar.CarName} нет NetworkObject!");
        return;
    }

    no.SpawnAsPlayerObject(NetworkManager.Singleton.LocalClientId, true);

    var controller = carInstance.GetComponent<VehicleController>();
    if (controller != null)
        controller.SetCarData(selectedCar);

    Debug.Log($"[TEST] Заспавнена машина: {selectedCar.CarName} (принудительно первая)");
}

    private CarData FindCarByGuid(FixedString64Bytes guid)
    {
        foreach (var car in _availableCars)
        {
            if (car != null && car.Guid == guid.ToString())
                return car;
        }
        return null;
    }
}