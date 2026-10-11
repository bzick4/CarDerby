// Assets/Scripts/UI/LobbyUI.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace CarDerby.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class LobbyUI : MonoBehaviour
    {
        [Header("Networking")]
        [SerializeField] private Networking.LobbyManager             _lobby;

        [Header("Loadout")]
        [SerializeField] private Customization.CarSelectionSystem    _cars;
        [SerializeField] private Customization.WeaponSelectionSystem _weapons;

        [Header("Car preview")]
        [SerializeField] private Transform _spawnPoint;

        [Header("Scenes")]
        [SerializeField] private string _gameSceneName = "GameScene";
        [SerializeField] private string _menuSceneName = "MainMenu";

        // ── Runtime state ────────────────────────────────────────────────────
        private UIDocument  _doc;
        private bool        _isReady;
        private bool        _bound;
        private float       _readyCooldownUntil = -1f;
        private GameObject  _previewCar;
        private GameObject  _previewWeapon;
        private GameObject  _previewScoop;

        // Cached delegates
        private System.Action _onCarPrev, _onCarNext, _onCarConfirm, _onCarBack;
        private System.Action _onWeaponPrev, _onWeaponNext, _onWeaponConfirm, _onWeaponBack;
        private System.Action _onScoopPrev, _onScoopNext, _onScoopConfirm, _onScoopBack;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_bound) return;
            _bound = true;

            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;

            // ── Step 1: Car ──────────────────────────────────────────────────
            _onCarPrev    = () => { _cars?.SelectPrev(); SpawnPreviewCar(); UpdateSelectionCarLabel(); };
            _onCarNext    = () => { _cars?.SelectNext(); SpawnPreviewCar(); UpdateSelectionCarLabel(); };
            _onCarConfirm = () =>
            {
                _doc.rootVisualElement.Q("car-selection-panel")   .AddToClassList("hidden");
                _doc.rootVisualElement.Q("weapon-selection-panel").RemoveFromClassList("hidden");
                // Оружие спавним только при входе на шаг 2
                UpdatePreviewWeapon();
                UpdateSelectionWeaponLabel();
            };
            // Назад с шага 1 → главное меню
            _onCarBack = () =>
            {
                NetworkManager.Singleton?.Shutdown();
                SceneManager.LoadScene(_menuSceneName);
            };
            Bind(root, "car-prev-btn",    _onCarPrev);
            Bind(root, "car-next-btn",    _onCarNext);
            Bind(root, "car-confirm-btn", _onCarConfirm);
            Bind(root, "car-back-btn",    _onCarBack);

            // ── Step 2: Weapon ───────────────────────────────────────────────
            _onWeaponPrev    = () => { _weapons?.SelectWeaponPrev(); UpdateSelectionWeaponLabel(); UpdatePreviewWeapon(); };
            _onWeaponNext    = () => { _weapons?.SelectWeaponNext(); UpdateSelectionWeaponLabel(); UpdatePreviewWeapon(); };
            _onWeaponConfirm = () =>
            {
                _doc.rootVisualElement.Q("weapon-selection-panel").AddToClassList("hidden");
                _doc.rootVisualElement.Q("scoop-selection-panel") .RemoveFromClassList("hidden");
                // Ковш спавним только при входе на шаг 3
                UpdatePreviewScoop();
                UpdateSelectionScoopLabel();
            };
            // Назад с шага 2 → выбор авто (сброс оружия)
            _onWeaponBack = () =>
            {
                _weapons?.SelectWeapon(0);          // сброс к первому оружию
                DestroyPreviewWeapon();              // убираем модель с машины
                _doc.rootVisualElement.Q("weapon-selection-panel").AddToClassList("hidden");
                _doc.rootVisualElement.Q("car-selection-panel")   .RemoveFromClassList("hidden");
                UpdateSelectionCarLabel();
            };
            Bind(root, "weapon-prev-btn",    _onWeaponPrev);
            Bind(root, "weapon-next-btn",    _onWeaponNext);
            Bind(root, "weapon-confirm-btn", _onWeaponConfirm);
            Bind(root, "weapon-back-btn",    _onWeaponBack);

            // ── Step 3: Scoop ────────────────────────────────────────────────
            _onScoopPrev    = () => { _weapons?.SelectScoopPrev(); UpdateSelectionScoopLabel(); UpdatePreviewScoop(); };
            _onScoopNext    = () => { _weapons?.SelectScoopNext(); UpdateSelectionScoopLabel(); UpdatePreviewScoop(); };
            _onScoopConfirm = () =>
            {
                _doc.rootVisualElement.Q("scoop-selection-panel").AddToClassList("hidden");
                _doc.rootVisualElement.Q("lobby-panel")          .RemoveFromClassList("hidden");
            };
            // Назад с шага 3 → выбор оружия (сброс ковша)
            _onScoopBack = () =>
            {
                _weapons?.SelectScoop(-1);          // сброс: без ковша
                DestroyPreviewScoop();               // убираем модель с машины
                _doc.rootVisualElement.Q("scoop-selection-panel")  .AddToClassList("hidden");
                _doc.rootVisualElement.Q("weapon-selection-panel") .RemoveFromClassList("hidden");
                UpdateSelectionWeaponLabel();
            };
            Bind(root, "scoop-prev-btn",    _onScoopPrev);
            Bind(root, "scoop-next-btn",    _onScoopNext);
            Bind(root, "scoop-confirm-btn", _onScoopConfirm);
            Bind(root, "scoop-back-btn",    _onScoopBack);

            // ── Lobby: gamemode ──────────────────────────────────────────────
            var gamemodeDropdown = root.Q<DropdownField>("gamemode-dropdown");
            var gamemodeLabel    = root.Q<Label>("gamemode-label");
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            if (gamemodeDropdown != null)
            {
                gamemodeDropdown.SetEnabled(isHost);
                if (isHost)
                    gamemodeDropdown.RegisterValueChangedCallback(e => _lobby?.SetGameModeServerRpc(e.newValue));
            }
            // Лейбл "MODE:" виден всем
            if (gamemodeLabel != null) gamemodeLabel.style.display = DisplayStyle.Flex;

            // ── Lobby: footer buttons ────────────────────────────────────────
            Bind(root, "leave-btn", OnLeaveClicked);
            Bind(root, "ready-btn", ToggleReady);
            Bind(root, "start-btn", OnStartClicked);
            var startBtn = root.Q<Button>("start-btn");
            if (startBtn != null) startBtn.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;

            // ── Lobby: chat ──────────────────────────────────────────────────
            var chatSendBtn = root.Q<Button>("chat-send-btn");
            var chatInput   = root.Q<TextField>("chat-input");
            if (chatSendBtn != null) chatSendBtn.clicked += SendChatMessage;
            if (chatInput   != null)
                chatInput.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                        SendChatMessage();
                });

            if (_lobby != null)
            {
                _lobby.Players.OnListChanged += OnPlayersListChanged;
                _lobby.OnChatMessage         += OnChatMessageReceived;
            }

            // ── NGO scene-load → уничтожаем превью до сканирования ───────────
            if (NetworkManager.Singleton?.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnLoad += OnNetworkSceneLoad;

            SpawnPreviewCar();            // шаг 1: только машина, без оружия
            UpdateSelectionCarLabel();

            RefreshPlayerList();

            StartCoroutine(SendNicknameWhenReady());
        }

        private void Update()
        {
            if (_doc == null || NetworkManager.Singleton == null) return;
            bool isHost   = NetworkManager.Singleton.IsHost;
            var  startBtn = _doc.rootVisualElement.Q<Button>("start-btn");
            if (startBtn == null) return;
            startBtn.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
            if (isHost) startBtn.SetEnabled(_lobby != null && _lobby.AllPlayersReady());
        }

        private void OnDisable()
        {
            if (!_bound) return;
            _bound = false;

            if (_lobby != null)
            {
                _lobby.Players.OnListChanged -= OnPlayersListChanged;
                _lobby.OnChatMessage         -= OnChatMessageReceived;
            }

            if (NetworkManager.Singleton?.SceneManager != null)
                NetworkManager.Singleton.SceneManager.OnLoad -= OnNetworkSceneLoad;

            DestroyPreviewCar();

            var root = _doc?.rootVisualElement;
            if (root == null) return;

            Unbind(root, "car-prev-btn",    _onCarPrev);
            Unbind(root, "car-next-btn",    _onCarNext);
            Unbind(root, "car-confirm-btn", _onCarConfirm);
            Unbind(root, "car-back-btn",    _onCarBack);
            Unbind(root, "weapon-prev-btn",    _onWeaponPrev);
            Unbind(root, "weapon-next-btn",    _onWeaponNext);
            Unbind(root, "weapon-confirm-btn", _onWeaponConfirm);
            Unbind(root, "weapon-back-btn",    _onWeaponBack);
            Unbind(root, "scoop-prev-btn",    _onScoopPrev);
            Unbind(root, "scoop-next-btn",    _onScoopNext);
            Unbind(root, "scoop-confirm-btn", _onScoopConfirm);
            Unbind(root, "scoop-back-btn",    _onScoopBack);
            Unbind(root, "leave-btn", OnLeaveClicked);
            Unbind(root, "ready-btn", ToggleReady);
            Unbind(root, "start-btn", OnStartClicked);

            var chatSendBtn = root.Q<Button>("chat-send-btn");
            if (chatSendBtn != null) chatSendBtn.clicked -= SendChatMessage;
        }

        // ── NGO scene event ──────────────────────────────────────────────────

        private void OnNetworkSceneLoad(ulong clientId, string sceneName,
                                        LoadSceneMode loadSceneMode, AsyncOperation asyncOp)
            => DestroyPreviewCar();

        // ── Preview car ──────────────────────────────────────────────────────

        private void SpawnPreviewCar()
        {
            DestroyPreviewCar();
            if (_cars == null || _spawnPoint == null) return;
            var prefab = _cars.GetPrefab(_cars.SelectedIndex);
            if (prefab == null) return;

            _previewCar = Instantiate(prefab, _spawnPoint.position, _spawnPoint.rotation);
            DisableGameplayComponents(_previewCar);
            // Оружие и ковш НЕ спавним здесь — только при входе на шаг 2 / шаг 3
        }

        private void DestroyPreviewCar()
        {
            if (_previewCar != null) { Destroy(_previewCar); _previewCar = null; }
            _previewWeapon = null;
            _previewScoop  = null;
        }

        private void DestroyPreviewWeapon()
        {
            if (_previewWeapon != null) { Destroy(_previewWeapon); _previewWeapon = null; }
        }

        private void DestroyPreviewScoop()
        {
            if (_previewScoop != null) { Destroy(_previewScoop); _previewScoop = null; }
        }

        private void UpdatePreviewWeapon()
        {
            DestroyPreviewWeapon();
            if (_previewCar == null) return;
            var prefab = _weapons?.SelectedWeapon?.WeaponPrefab;
            if (prefab == null) return;

            var slot  = _previewCar.GetComponentInChildren<Car.CarWeaponSlot>();
            var mount = slot != null ? slot.transform : _previewCar.transform;
            _previewWeapon = Instantiate(prefab, mount);
            _previewWeapon.transform.localPosition = Vector3.zero;
            _previewWeapon.transform.localRotation = Quaternion.identity;
            DisableGameplayComponents(_previewWeapon);
        }

        private void UpdatePreviewScoop()
        {
            DestroyPreviewScoop();
            if (_previewCar == null) return;
            var prefab = _weapons?.SelectedScoop?.WeaponPrefab;
            if (prefab == null) return;  // None — без ковша

            var slot  = _previewCar.GetComponentInChildren<Car.CarScoopSlot>();
            var mount = slot != null ? slot.transform : _previewCar.transform;
            _previewScoop = Instantiate(prefab, mount);
            _previewScoop.transform.localPosition = Vector3.zero;
            _previewScoop.transform.localRotation = Quaternion.identity;
            DisableGameplayComponents(_previewScoop);
        }

        private static void DisableGameplayComponents(GameObject go)
        {
            foreach (var b in go.GetComponentsInChildren<Behaviour>(true))
                b.enabled = false;
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
                rb.isKinematic = true;
            foreach (var wc in go.GetComponentsInChildren<WheelCollider>(true))
                wc.enabled = false;
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                col.enabled = false;
        }

        // ── Selection labels ─────────────────────────────────────────────────

        private void UpdateSelectionCarLabel()
        {
            var lbl = _doc?.rootVisualElement.Q<Label>("car-name-label");
            if (lbl != null) lbl.text = _cars?.SelectedCar?.CarName ?? "—";
        }

        private void UpdateSelectionWeaponLabel()
        {
            if (_weapons == null)
            { Debug.LogError("[LobbyUI] _weapons не назначен в Inspector!"); return; }
            var lbl = _doc?.rootVisualElement.Q<Label>("weapon-name-label");
            if (lbl != null) lbl.text = _weapons.SelectedWeapon?.WeaponName ?? "—";
        }

        private void UpdateSelectionScoopLabel()
        {
            if (_weapons == null) return;
            var lbl = _doc?.rootVisualElement.Q<Label>("scoop-name-label");
            if (lbl != null) lbl.text = _weapons.SelectedScoop?.WeaponName ?? "None";
        }

        // ── Chat ─────────────────────────────────────────────────────────────

        private void SendChatMessage()
        {
            if (_lobby == null || !_lobby.IsSpawned) return;
            var input = _doc?.rootVisualElement.Q<TextField>("chat-input");
            if (input == null || string.IsNullOrWhiteSpace(input.value)) return;
            _lobby.SendChatServerRpc(input.value.Trim());
            input.value = "";
            input.Focus();
        }

        private void OnChatMessageReceived(string sender, string message)
        {
            var chatView = _doc?.rootVisualElement.Q<ScrollView>("chat-messages");
            if (chatView == null) return;

            var row = new VisualElement();
            row.AddToClassList("chat-msg-row");

            var senderLbl = new Label($"{sender}:");
            senderLbl.AddToClassList("chat-sender");

            var textLbl = new Label(message);
            textLbl.AddToClassList("chat-text");

            row.Add(senderLbl);
            row.Add(textLbl);
            chatView.Add(row);

            // Прокручиваем вниз
            chatView.schedule.Execute(() => chatView.ScrollTo(row));
        }

        // ── Nickname ─────────────────────────────────────────────────────────

        private IEnumerator SendNicknameWhenReady()
        {
            yield return new WaitUntil(() => _lobby != null && _lobby.IsSpawned);
            string nick = Networking.AuthManager.Instance != null
                ? Networking.AuthManager.Instance.PlayerNickname
                : PlayerPrefs.GetString("PlayerNickname",
                    $"Player_{NetworkManager.Singleton.LocalClientId}");
            if (!string.IsNullOrWhiteSpace(nick))
                _lobby.SetNicknameServerRpc(nick);
        }

        // ── Player list ──────────────────────────────────────────────────────

        private void OnPlayersListChanged(NetworkListEvent<Networking.LobbyPlayerData> _)
            => RefreshPlayerList();

        private void RefreshPlayerList()
        {
            if (_doc == null || _lobby == null) return;
            var root     = _doc.rootVisualElement;
            var listView = root.Q<ScrollView>("player-list");
            if (listView == null) return;
            listView.Clear();

            int count = _lobby.PlayerCount;
            for (int i = 0; i < count; i++)
            {
                var  data    = _lobby.GetPlayer(i);
                bool isLocal = NetworkManager.Singleton != null &&
                               data.ClientId == NetworkManager.Singleton.LocalClientId;
                bool ready   = isLocal ? _isReady : data.IsReady;

                var row = new VisualElement();
                row.AddToClassList("player-row");

                var leftCol = new VisualElement();
                leftCol.AddToClassList("player-row-left");

                var nameLabel = new Label(data.DisplayName.ToString());
                nameLabel.AddToClassList("player-row-name");

                string carName = (_cars != null && data.CarIndex >= 0 && data.CarIndex < _cars.Cars.Count)
                    ? (_cars.Cars[data.CarIndex]?.CarName ?? "—") : "—";
                var carLabel = new Label(carName);
                carLabel.AddToClassList("player-row-car");

                leftCol.Add(nameLabel);
                leftCol.Add(carLabel);

                var statusLabel = new Label(ready ? "Ready" : "Not Ready");
                statusLabel.AddToClassList("player-row-status");
                statusLabel.AddToClassList(ready ? "is-ready" : "not-ready");

                row.Add(leftCol);
                row.Add(statusLabel);
                listView.Add(row);
            }

            var statusLbl = root.Q<Label>("lobby-status");
            if (statusLbl != null) statusLbl.text = $"{count} / {_lobby.MaxPlayers}  players";
        }

        // ── Ready ────────────────────────────────────────────────────────────

        private void ToggleReady()
        {
            if (Time.unscaledTime < _readyCooldownUntil) return;
            _readyCooldownUntil = Time.unscaledTime + 0.5f;

            if (_lobby == null) { Debug.LogWarning("[LobbyUI] _lobby не назначен!"); return; }
            if (!_lobby.IsSpawned)
            {
                Debug.LogWarning("[LobbyUI] LobbyManager.IsSpawned = false.");
                return;
            }

            _isReady = !_isReady;

            string nick = Networking.AuthManager.Instance != null
                ? Networking.AuthManager.Instance.PlayerNickname
                : PlayerPrefs.GetString("PlayerNickname",
                    $"Player_{NetworkManager.Singleton.LocalClientId}");
            if (!string.IsNullOrWhiteSpace(nick)) _lobby.SetNicknameServerRpc(nick);

            _lobby.SetReadyServerRpc(_isReady);
            _lobby.SetLoadoutServerRpc(
                _cars?.SelectedIndex ?? 0,
                IndexOf(_weapons?.RoofWeapons, _weapons?.SelectedWeapon),
                IndexOf(_weapons?.Scoops,      _weapons?.SelectedScoop));

            RefreshPlayerList();

            var btn = _doc.rootVisualElement.Q<Button>("ready-btn");
            if (btn == null) return;
            btn.text = _isReady ? "CANCEL" : "READY";
            if (_isReady) btn.AddToClassList("is-ready");
            else          btn.RemoveFromClassList("is-ready");
        }

        // ── Leave / Start ─────────────────────────────────────────────────────

        private void OnLeaveClicked()
        {
            NetworkManager.Singleton?.Shutdown();
            SceneManager.LoadScene(_menuSceneName);
        }

        private void OnStartClicked()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost) return;

            Gameplay.MatchData.Clear();
            for (int i = 0; i < _lobby.PlayerCount; i++)
            {
                var p = _lobby.GetPlayer(i);
                Gameplay.MatchData.SetPlayerLoadout(p.ClientId, p.CarIndex, p.WeaponIndex, p.ScoopIndex);
            }

            var status = NetworkManager.Singleton.SceneManager
                .LoadScene(_gameSceneName, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started)
                Debug.LogError($"[LobbyUI] Не удалось загрузить {_gameSceneName}: {status}");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static void Bind(VisualElement root, string name, System.Action handler)
        {
            var btn = root.Q<Button>(name);
            if (btn != null) btn.clicked += handler;
            else Debug.LogWarning($"[LobbyUI] Кнопка '{name}' не найдена в UXML");
        }

        private static void Unbind(VisualElement root, string name, System.Action handler)
        {
            var btn = root.Q<Button>(name);
            if (btn != null) btn.clicked -= handler;
        }

        private static int IndexOf<T>(IReadOnlyList<T> list, T item) where T : class
        {
            if (list == null) return -1;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == item) return i;
            return -1;
        }
    }
}
