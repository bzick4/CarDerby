// using UnityEngine;
// using UnityEngine.UI;

// /// <summary>
// /// Управление выбором машины в лобби
// /// </summary>
// public class VehicleSelectionUI : MonoBehaviour
// {
//     [System.Serializable]
//     public class VehicleOption
//     {
//         public string vehicleId;
//         public string vehicleName;
//         public Button selectButton;
//         public Image previewImage;
//     }

//     [SerializeField] private VehicleOption[] _vehicleOptions;
//     [SerializeField] private Button _playButton;
//     [SerializeField] private Text _selectedVehicleText;

//     private string _selectedVehicle;

//     private void Start()
//     {
//         // Подключаем кнопки выбора машин
//         foreach (var option in _vehicleOptions)
//         {
//             string vehicleId = option.vehicleId; // Для замыкания
//             option.selectButton.onClick.AddListener(() => SelectVehicle(vehicleId, option.vehicleName));
//         }

//         // Кнопка "Играть"
//         _playButton.onClick.AddListener(PlayGame);

//         // По умолчанию первая машина
//         if (_vehicleOptions.Length > 0)
//         {
//             SelectVehicle(_vehicleOptions[0].vehicleId, _vehicleOptions[0].vehicleName);
//         }
//     }

//     /// <summary>
//     /// Выбирает машину
//     /// </summary>
//     private void SelectVehicle(string vehicleId, string vehicleName)
//     {
//         _selectedVehicle = vehicleId;
//         _selectedVehicleText.text = $"Выбрана: {vehicleName}";

//         Debug.Log($"Выбрана машина: {vehicleId} ({vehicleName})");
//     }

//     /// <summary>
//     /// Начинает игру с выбранной машиной
//     /// </summary>
//     private void PlayGame()
//     {
//         if (string.IsNullOrEmpty(_selectedVehicle))
//         {
//             Debug.LogWarning("Машина не выбрана!");
//             return;
//         }

//         // Загружаем игровую сцену
//         LobbySelectionManager.Instance?.LoadGameScene(_selectedVehicle);
//     }
// }
