// Assets/Scripts/UI/LobbyUI.cs
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using System.Collections.Generic;

namespace CarDerby.UI
{
    /// <summary>
    /// Drives Lobby.uxml.
    /// Subscribes to LobbyManager.OnLobbyChanged for list refresh.
    /// Car/weapon cycling calls CarSelectionSystem and WeaponSelectionSystem locally,
    /// then pushes the choice to the server via SetLoadoutServerRpc on ready.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private Networking.LobbyManager            _lobby;
        [SerializeField] private Customization.CarSelectionSystem    _cars;
        [SerializeField] private Customization.WeaponSelectionSystem _weapons;

        private UIDocument _doc;
        private bool       _isReady;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;

            // Lobby events
            _lobby.OnLobbyChanged += RefreshPlayerList;

            // Car selection
            root.Q<Button>("car-prev-btn").clicked += () => { _cars.SelectPrev(); UpdateCarLabel(); };
            root.Q<Button>("car-next-btn").clicked += () => { _cars.SelectNext(); UpdateCarLabel(); };

            // Weapon selection
            root.Q<Button>("weapon-prev-btn").clicked += () => { CycleWeapon(-1); UpdateWeaponLabel(); };
            root.Q<Button>("weapon-next-btn").clicked += () => { CycleWeapon(+1); UpdateWeaponLabel(); };

            // Scoop selection
            root.Q<Button>("scoop-prev-btn").clicked += () => { CycleScoop(-1); UpdateScoopLabel(); };
            root.Q<Button>("scoop-next-btn").clicked += () => { CycleScoop(+1); UpdateScoopLabel(); };

            // Game mode
            root.Q<DropdownField>("gamemode-dropdown").RegisterValueChangedCallback(e =>
            {
                if (NetworkManager.Singleton.IsHost)
                    _lobby.SetGameModeServerRpc(e.newValue);
            });

            // Ready / Start
            root.Q<Button>("ready-btn").clicked += ToggleReady;
            root.Q<Button>("start-btn").clicked += OnStartClicked;

            // Host only: show Start button
            bool isHost = NetworkManager.Singleton.IsHost;
            root.Q<Button>("start-btn").style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;

            RefreshPlayerList();
            UpdateCarLabel();
            UpdateWeaponLabel();
            UpdateScoopLabel();
        }

        private void OnDisable()
        {
            _lobby.OnLobbyChanged -= RefreshPlayerList;
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

                var row = new VisualElement();
                row.AddToClassList("player-row");

                var nameLabel = new Label(data.DisplayName.ToString());
                nameLabel.AddToClassList("player-row-name");

                bool ready = data.IsReady;
                var statusLabel = new Label(ready ? "Ready" : "Not Ready");
                statusLabel.AddToClassList("player-row-status");
                statusLabel.AddToClassList(ready ? "is-ready" : "not-ready");

                row.Add(nameLabel);
                row.Add(statusLabel);
                listView.Add(row);
            }

            root.Q<Label>("lobby-status").text = $"{count} / {_lobby.MaxPlayers}  players";

            // Обновляем кнопку Start (только для хоста)
            if (NetworkManager.Singleton.IsHost)
                root.Q<Button>("start-btn").SetEnabled(_lobby.AllPlayersReady());
        }

        // ── Loadout cycling ──────────────────────────────────────────────────

        private void UpdateCarLabel() =>
            _doc.rootVisualElement.Q<Label>("car-name-label").text =
                _cars.SelectedCar?.CarName ?? "—";

        private void CycleWeapon(int dir)
        {
            if (dir > 0) _weapons.SelectWeaponNext();
            else         _weapons.SelectWeaponPrev();
        }
        private void UpdateWeaponLabel() =>
            _doc.rootVisualElement.Q<Label>("weapon-name-label").text =
                _weapons.SelectedWeapon?.WeaponName ?? "—";

        private void CycleScoop(int dir)
        {
            if (dir > 0) _weapons.SelectScoopNext();
            else         _weapons.SelectScoopPrev();
        }
        private void UpdateScoopLabel() =>
            _doc.rootVisualElement.Q<Label>("scoop-name-label").text =
                _weapons.SelectedScoop?.WeaponName ?? "None";

        // ── Ready / Start ────────────────────────────────────────────────────

        private void ToggleReady()
        {
            // LobbyManager спавнится NGO чуть позже загрузки сцены — защита от гонки.
            if (!_lobby.IsSpawned)
            {
                Debug.LogWarning("[LobbyUI] LobbyManager ещё не заспавнен, попробуй ещё раз.");
                return;
            }

            _isReady = !_isReady;

            _lobby.SetReadyServerRpc(_isReady);
            _lobby.SetLoadoutServerRpc(
                _cars.SelectedIndex,
                IndexOf(_weapons.RoofWeapons, _weapons.SelectedWeapon),
                IndexOf(_weapons.Scoops,      _weapons.SelectedScoop));

            var btn = _doc.rootVisualElement.Q<Button>("ready-btn");
            btn.text = _isReady ? "CANCEL" : "READY";
            if (_isReady) btn.AddToClassList("is-ready");
            else          btn.RemoveFromClassList("is-ready");
        }

        private static int IndexOf<T>(IReadOnlyList<T> list, T item) where T : class
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == item) return i;
            return -1;
        }

        private void OnStartClicked()
        {
            Debug.Log("[LobbyUI] Host starting match.");
            // Load game scene — wire to a MatchController or directly:
            // NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        }
    }
}
