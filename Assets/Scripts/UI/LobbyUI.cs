// Assets/Scripts/UI/LobbyUI.cs
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
        [SerializeField] private Networking.LobbyManager            _lobby;
        [SerializeField] private Customization.CarSelectionSystem    _cars;
        [SerializeField] private Customization.WeaponSelectionSystem _weapons;
        [SerializeField] private string _gameSceneName = "GameScene";

        private UIDocument _doc;
        private bool       _isReady;
        private bool       _bound;                    // защита от двойной подписки
        private float      _readyCooldownUntil = -1f; // time-based гард от двойного срабатывания

        // Храним лямбды чтобы корректно отписаться
        private System.Action _onCarPrev, _onCarNext;
        private System.Action _onWeaponPrev, _onWeaponNext;
        private System.Action _onScoopPrev, _onScoopNext;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void OnEnable()
        {
            if (_bound) return;          // UIDocument иногда вызывает OnEnable дважды
            _bound = true;

            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;

            // Car selection
            _onCarPrev = () => { _cars.SelectPrev(); UpdateCarLabel(); };
            _onCarNext = () => { _cars.SelectNext(); UpdateCarLabel(); };
            root.Q<Button>("car-prev-btn").clicked += _onCarPrev;
            root.Q<Button>("car-next-btn").clicked += _onCarNext;

            // Weapon selection
            _onWeaponPrev = () => { CycleWeapon(-1); UpdateWeaponLabel(); };
            _onWeaponNext = () => { CycleWeapon(+1); UpdateWeaponLabel(); };
            root.Q<Button>("weapon-prev-btn").clicked += _onWeaponPrev;
            root.Q<Button>("weapon-next-btn").clicked += _onWeaponNext;

            // Scoop selection
            _onScoopPrev = () => { CycleScoop(-1); UpdateScoopLabel(); };
            _onScoopNext = () => { CycleScoop(+1); UpdateScoopLabel(); };
            root.Q<Button>("scoop-prev-btn").clicked += _onScoopPrev;
            root.Q<Button>("scoop-next-btn").clicked += _onScoopNext;

            // Game mode — виден и доступен только хосту
            var gamemodeRow  = root.Q<DropdownField>("gamemode-dropdown");
            var gamemodeLabel = root.Q<Label>("gamemode-label");
            bool isHost = NetworkManager.Singleton.IsHost;
            if (gamemodeRow  != null) gamemodeRow.style.display  = isHost ? DisplayStyle.Flex : DisplayStyle.None;
            if (gamemodeLabel != null) gamemodeLabel.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
            if (isHost)
            {
                gamemodeRow.RegisterValueChangedCallback(e => _lobby.SetGameModeServerRpc(e.newValue));
            }

            // Nickname field — загружаем сохранённый ник, отправляем на сервер только когда лобби готово
            var nickField = root.Q<TextField>("nickname-field");
            if (nickField != null)
            {
                string saved = PlayerPrefs.GetString("PlayerNickname", $"Player_{NetworkManager.Singleton.LocalClientId}");
                nickField.value = saved;

                nickField.RegisterValueChangedCallback(e =>
                {
                    string nick = e.newValue.Trim();
                    if (nick.Length == 0) return;
                    PlayerPrefs.SetString("PlayerNickname", nick);
                    PlayerPrefs.Save();
                    if (_lobby != null && _lobby.IsSpawned)
                        _lobby.SetNicknameServerRpc(nick);
                });
            }

            root.Q<Button>("ready-btn").clicked += ToggleReady;
            root.Q<Button>("start-btn").clicked += OnStartClicked;
            root.Q<Button>("start-btn").style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;

            // Подписываемся напрямую на NetworkList — самый надёжный путь
            _lobby.Players.OnListChanged += OnPlayersListChanged;

            RefreshPlayerList();
            UpdateCarLabel();
            UpdateWeaponLabel();
            UpdateScoopLabel();
        }

        private void Update()
        {
            if (_doc == null || NetworkManager.Singleton == null) return;

            bool isHost = NetworkManager.Singleton.IsHost;
            var root     = _doc.rootVisualElement;
            var startBtn = root.Q<Button>("start-btn");
            if (startBtn == null) return;

            // Видимость — только для хоста
            startBtn.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;

            // Доступность — когда все готовы (читаем свежее состояние каждый кадр)
            if (isHost)
            {
                startBtn.SetEnabled(_lobby != null && _lobby.AllPlayersReady());
            }
        }

        private void OnDisable()
        {
            if (!_bound) return;
            _bound = false;

            _lobby.Players.OnListChanged -= OnPlayersListChanged;

            // Отписываем кнопки — без этого при повторном OnEnable накапливались дубли
            var root = _doc?.rootVisualElement;
            if (root == null) return;

            root.Q<Button>("car-prev-btn").clicked    -= _onCarPrev;
            root.Q<Button>("car-next-btn").clicked    -= _onCarNext;
            root.Q<Button>("weapon-prev-btn").clicked -= _onWeaponPrev;
            root.Q<Button>("weapon-next-btn").clicked -= _onWeaponNext;
            root.Q<Button>("scoop-prev-btn").clicked  -= _onScoopPrev;
            root.Q<Button>("scoop-next-btn").clicked  -= _onScoopNext;
            root.Q<Button>("ready-btn").clicked       -= ToggleReady;
            root.Q<Button>("start-btn").clicked       -= OnStartClicked;
        }

        // ── NetworkList callback ─────────────────────────────────────────────

        private void OnPlayersListChanged(NetworkListEvent<Networking.LobbyPlayerData> _)
        {
            RefreshPlayerList();
        }

        // ── Player list ──────────────────────────────────────────────────────

        private void RefreshPlayerList()
        {
            var root     = _doc.rootVisualElement;
            var listView = root.Q<ScrollView>("player-list");
            listView.Clear();

            int count = _lobby.PlayerCount;

            for (int i = 0; i < count; i++)
            {
                var data = _lobby.GetPlayer(i);

                // Для локального игрока используем локальный _isReady как источник правды —
                // это убирает задержку ожидания сетевого round-trip
                bool isLocal = data.ClientId == NetworkManager.Singleton.LocalClientId;
                bool ready   = isLocal ? _isReady : data.IsReady;

                var row = new VisualElement();
                row.AddToClassList("player-row");

                var nameLabel = new Label(data.DisplayName.ToString());
                nameLabel.AddToClassList("player-row-name");

                var statusLabel = new Label(ready ? "Ready" : "Not Ready");
                statusLabel.AddToClassList("player-row-status");
                statusLabel.AddToClassList(ready ? "is-ready" : "not-ready");

                row.Add(nameLabel);
                row.Add(statusLabel);
                listView.Add(row);
            }

            root.Q<Label>("lobby-status").text = $"{count} / {_lobby.MaxPlayers}  players";
            // Кнопка Start обновляется в Update() — там нет race condition с RPC
        }

        // ── Loadout cycling ──────────────────────────────────────────────────

        private void UpdateCarLabel() =>
            _doc.rootVisualElement.Q<Label>("car-name-label").text =
                _cars.SelectedCar?.CarName ?? "—";

        private void CycleWeapon(int dir)
        {
            if (dir > 0) _weapons.SelectWeaponNext(); else _weapons.SelectWeaponPrev();
        }
        private void UpdateWeaponLabel() =>
            _doc.rootVisualElement.Q<Label>("weapon-name-label").text =
                _weapons.SelectedWeapon?.WeaponName ?? "—";

        private void CycleScoop(int dir)
        {
            if (dir > 0) _weapons.SelectScoopNext(); else _weapons.SelectScoopPrev();
        }
        private void UpdateScoopLabel() =>
            _doc.rootVisualElement.Q<Label>("scoop-name-label").text =
                _weapons.SelectedScoop?.WeaponName ?? "None";

        // ── Ready ────────────────────────────────────────────────────────────

        private void ToggleReady()
        {
            // Кулдаун 500 мс — блокирует повторные вызовы между фреймами
            if (Time.unscaledTime < _readyCooldownUntil) return;
            _readyCooldownUntil = Time.unscaledTime + 0.5f;

            if (!_lobby.IsSpawned) return;

            _isReady = !_isReady;

            // Отправляем ник при каждом нажатии Ready — гарантирует что лобби уже заспавнено
            var nf = _doc.rootVisualElement.Q<TextField>("nickname-field");
            if (nf != null)
            {
                string nick = nf.value.Trim();
                if (nick.Length > 0) _lobby.SetNicknameServerRpc(nick);
            }

            _lobby.SetReadyServerRpc(_isReady);
            _lobby.SetLoadoutServerRpc(
                _cars.SelectedIndex,
                IndexOf(_weapons.RoofWeapons, _weapons.SelectedWeapon),
                IndexOf(_weapons.Scoops,      _weapons.SelectedScoop));

            // Обновляем UI немедленно не дожидаясь сетевого ответа
            RefreshPlayerList();

            var btn = _doc.rootVisualElement.Q<Button>("ready-btn");
            btn.text = _isReady ? "CANCEL" : "READY";
            if (_isReady) btn.AddToClassList("is-ready");
            else          btn.RemoveFromClassList("is-ready");
        }

        // ── Start ────────────────────────────────────────────────────────────

        private void OnStartClicked()
        {
            if (!NetworkManager.Singleton.IsHost) return;

            // Сохраняем выбор каждого игрока в статический контейнер
            // (переживает смену сцены, не нужен DontDestroyOnLoad)
            Gameplay.MatchData.Clear();
            for (int i = 0; i < _lobby.PlayerCount; i++)
            {
                var p = _lobby.GetPlayer(i);
                Gameplay.MatchData.SetPlayerLoadout(p.ClientId, p.CarIndex, p.WeaponIndex, p.ScoopIndex);
            }

            // Загружаем через NGO SceneManager — все клиенты переедут автоматически
            var status = NetworkManager.Singleton.SceneManager.LoadScene(
                _gameSceneName, LoadSceneMode.Single);

            if (status != SceneEventProgressStatus.Started)
                Debug.LogError($"[LobbyUI] Не удалось загрузить {_gameSceneName}: {status}");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static int IndexOf<T>(IReadOnlyList<T> list, T item) where T : class
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == item) return i;
            return -1;
        }
    }
}
