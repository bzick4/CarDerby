using Unity.Netcode;
using UnityEngine;

public class GameSceneInitializer : MonoBehaviour   // ← Изменили с NetworkBehaviour на MonoBehaviour
{
    [SerializeField] private CarData[] _availableCars;

    private void Start()
    {
        // Запускаем только на локальном клиенте после загрузки сцены
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsClient)
            return;

        Debug.Log("[GameSceneInitializer] Сцена загружена. Запускаем спавн локального игрока...");

        SpawnLocalPlayerCar();
    }

    private void SpawnLocalPlayerCar()
    {
        if (_availableCars == null || _availableCars.Length == 0)
        {
            Debug.LogError("[GameSceneInitializer] _availableCars пустой!");
            return;
        }

        // Пока берём первую машину для теста
        CarData selectedCar = _availableCars[0];

        if (selectedCar.CarPrefab == null)
        {
            Debug.LogError($"[GameSceneInitializer] У машины {selectedCar.CarName} нет префаба!");
            return;
        }

        GameObject carInstance = Instantiate(selectedCar.CarPrefab);
        NetworkObject networkObject = carInstance.GetComponent<NetworkObject>();

        if (networkObject == null)
        {
            Debug.LogError($"[GameSceneInitializer] На префабе {selectedCar.CarName} нет NetworkObject!");
            return;
        }

        // Спавним как Player Object
        networkObject.SpawnAsPlayerObject(NetworkManager.Singleton.LocalClientId, true);

        VehicleController controller = carInstance.GetComponent<VehicleController>();
        if (controller != null)
            controller.SetCarData(selectedCar);

        Debug.Log($"[GameSceneInitializer] УСПЕШНО заспавнена машина: {selectedCar.CarName} для локального игрока");
    }
}