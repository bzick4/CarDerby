using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Collections;
using System.Collections;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class LobbySelectionManager : MonoBehaviour
{
    [SerializeField] private CarData[] _availableCars;
    [SerializeField] private string _gameSceneName = "GameScene";

    private UIDocument _uiDocument;
    private ListView _carListView;
    private Label _statusLabel;
    private CarData _selectedCar;
    private bool _isTransitioning = false;

    private void Awake()
    {
        _uiDocument = GetComponent<UIDocument>();

        if (_uiDocument == null)
            Debug.LogError("[Lobby] UIDocument не найден на объекте LobbyUI!");
    }

    private async void Start()
    {
        // Инициализация Unity Services
        try
        {
            await UnityServices.InitializeAsync();
            Debug.Log("[Lobby] UnityServices успешно инициализированы.");

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log("[Lobby] Анонимный вход выполнен.");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[Lobby] Ошибка инициализации сервисов: {e.Message}");
            return;
        }

        StartCoroutine(SetupUIAfterDelay());
    }

    private IEnumerator SetupUIAfterDelay()
    {
        yield return null;
        yield return null;

        if (_uiDocument == null || _uiDocument.rootVisualElement == null)
        {
            Debug.LogError("[Lobby] rootVisualElement == null.");
            yield break;
        }

        var root = _uiDocument.rootVisualElement;

        _carListView = root.Q<ListView>("CarListView");
        _statusLabel = root.Q<Label>("StatusLabel");

        if (_carListView == null)
        {
            Debug.LogError("[Lobby] ListView 'CarListView' не найден в UXML!");
            yield break;
        }

        // Кнопки
        var hostBtn = root.Q<Button>("HostButton");
        var joinBtn = root.Q<Button>("JoinButton");

        if (hostBtn != null) hostBtn.clicked += async () => await StartHostAsync();
        else Debug.LogError("[Lobby] Кнопка HostButton не найдена!");

        if (joinBtn != null) joinBtn.clicked += JoinSelected;
        else Debug.LogError("[Lobby] Кнопка JoinButton не найдена!");

        // Список автомобилей
        _carListView.makeItem = MakeCarItem;
        _carListView.bindItem = BindCarItem;
        _carListView.itemsSource = _availableCars;
        _carListView.selectionChanged += OnCarSelectionChanged;

        if (_availableCars != null && _availableCars.Length > 0)
            _carListView.selectedIndex = 0;
    }

    private VisualElement MakeCarItem()
    {
        var template = Resources.Load<VisualTreeAsset>("Lobby/CarListItem");
        return template != null ? template.Instantiate() : new Label("Шаблон не найден");
    }

    private void BindCarItem(VisualElement element, int index)
    {
        if (index < 0 || index >= _availableCars.Length || _availableCars[index] == null) return;

        var label = element.Q<Label>("CarName");
        if (label != null)
            label.text = _availableCars[index].CarName;
    }

    private void OnCarSelectionChanged(IEnumerable<object> selectedItems)
    {
        var e = selectedItems.GetEnumerator();
        if (e.MoveNext() && e.Current is CarData car)
        {
            _selectedCar = car;
            PlayerSessionData.SelectedCarGuid = car.Guid;
            if (_statusLabel != null)
                _statusLabel.text = $"Выбран: {car.CarName}";
        }
    }

    private async Task StartHostAsync()
{
    if (_isTransitioning || _selectedCar == null)
    {
        if (_statusLabel != null) _statusLabel.text = "Сначала выбери автомобиль!";
        return;
    }

    _isTransitioning = true;

    if (_statusLabel != null)
        _statusLabel.text = "Запускаем хост...";

    bool success = NetworkManager.Singleton.StartHost();

    if (!success)
    {
        _isTransitioning = false;
        if (_statusLabel != null) _statusLabel.text = "Не удалось запустить хост";
        return;
    }

    // Ждём один кадр, чтобы NetworkManager полностью инициализировался
    await Task.Delay(100);

    if (_statusLabel != null)
        _statusLabel.text = "Загружаем игровую сцену...";

    // Загружаем сцену через NetworkSceneManager
    NetworkManager.Singleton.SceneManager.LoadScene(_gameSceneName, LoadSceneMode.Single);
}

private void JoinSelected()
{
    if (_isTransitioning) return;

    _isTransitioning = true;

    if (_statusLabel != null)
        _statusLabel.text = "Подключаемся...";

    bool success = NetworkManager.Singleton.StartClient();

    if (!success)
    {
        _isTransitioning = false;
        if (_statusLabel != null) _statusLabel.text = "Не удалось подключиться";
    }
    // Клиент НЕ загружает сцену сам — хост сделает это за всех
}

    // Безопасная очистка
    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            // Если где-то ещё осталась подписка — убираем
            Debug.Log("[Lobby] OnDestroy: отписываемся от событий");
        }
    }
}