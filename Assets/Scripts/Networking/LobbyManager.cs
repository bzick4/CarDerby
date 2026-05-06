// Assets/Scripts/Networking/LobbyManager.cs
using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CarDerby.Networking
{
    [Serializable]
    public struct LobbyPlayerData : INetworkSerializable, IEquatable<LobbyPlayerData>
    {
        public ulong             ClientId;
        public FixedString64Bytes DisplayName;
        public bool              IsReady;
        public int               CarIndex;
        public int               WeaponIndex;
        public int               ScoopIndex;  // -1 = none

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref ClientId);
            s.SerializeValue(ref DisplayName);
            s.SerializeValue(ref IsReady);
            s.SerializeValue(ref CarIndex);
            s.SerializeValue(ref WeaponIndex);
            s.SerializeValue(ref ScoopIndex);
        }

        public bool Equals(LobbyPlayerData other) => ClientId == other.ClientId;
    }

    /// <summary>
    /// Manages pre-match lobby state: player list, ready states, settings.
    /// Lives in the scene for the lifetime of a hosted session.
    /// </summary>
    public class LobbyManager : NetworkBehaviour
    {
        [SerializeField] private int    _maxPlayers  = 8;
        [SerializeField] private int    _minPlayers  = 1; // поставь 2 перед релизом
        [SerializeField] private string _lobbyName   = "My Lobby";

        // NetworkList syncs the player roster to all clients automatically.
        private readonly NetworkList<LobbyPlayerData> _players = new();

        private readonly NetworkVariable<FixedString64Bytes> _selectedGameMode = new(
            "Deathmatch",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public int    PlayerCount => _players.Count;
        public int    MaxPlayers  => _maxPlayers;
        public string GameMode    => _selectedGameMode.Value.ToString();

        public LobbyPlayerData GetPlayer(int index) => _players[index];

        // Прямой доступ к NetworkList — LobbyUI подписывается на OnListChanged напрямую
        public NetworkList<LobbyPlayerData> Players => _players;

        public event Action OnLobbyChanged;

        public override void OnNetworkSpawn()
        {
            _players.OnListChanged += _ => OnLobbyChanged?.Invoke();

            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback  += AddPlayer;
                NetworkManager.OnClientDisconnectCallback += RemovePlayer;

                // Хост подключается ДО того как LobbyManager заспавнился —
                // OnClientConnectedCallback для него уже не придёт, добавляем вручную.
                foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
                    AddPlayer(clientId);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback  -= AddPlayer;
                NetworkManager.OnClientDisconnectCallback -= RemovePlayer;
            }
        }

        // ── Client → Server RPCs ─────────────────────────────────────────────

        [ServerRpc(RequireOwnership = false)]
        public void SetReadyServerRpc(bool ready, ServerRpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            UpdatePlayer(sender, p => { p.IsReady = ready; return p; });
            NotifyChangedClientRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetNicknameServerRpc(string nickname, ServerRpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            if (string.IsNullOrWhiteSpace(nickname)) return;
            string trimmed = nickname.Length > 20 ? nickname.Substring(0, 20) : nickname;
            UpdatePlayer(sender, p => { p.DisplayName = trimmed; return p; });
            NotifyChangedClientRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        public void SetLoadoutServerRpc(int carIndex, int weaponIndex, int scoopIndex, ServerRpcParams rpc = default)
        {
            ulong sender = rpc.Receive.SenderClientId;
            UpdatePlayer(sender, p =>
            {
                p.CarIndex    = carIndex;
                p.WeaponIndex = weaponIndex;
                p.ScoopIndex  = scoopIndex;
                return p;
            });
            NotifyChangedClientRpc();
        }

        [ServerRpc]
        public void SetGameModeServerRpc(string mode) =>
            _selectedGameMode.Value = mode;

        // ── Server actions ───────────────────────────────────────────────────

        public bool AllPlayersReady()
        {
            if (_players.Count < _minPlayers) return false;
            foreach (var p in _players)
                if (!p.IsReady) return false;
            return true;
        }

        public LobbyPlayerData GetPlayerData(ulong clientId)
        {
            foreach (var p in _players)
                if (p.ClientId == clientId) return p;
            return default;
        }

        // ── Internals ────────────────────────────────────────────────────────

        private void AddPlayer(ulong clientId)
        {
            // Защита от дублей — может вызваться дважды для хоста
            foreach (var p in _players)
                if (p.ClientId == clientId) return;

            if (_players.Count >= _maxPlayers)
            {
                NetworkManager.DisconnectClient(clientId);
                return;
            }

            _players.Add(new LobbyPlayerData
            {
                ClientId    = clientId,
                DisplayName = $"Player_{clientId}",
                IsReady     = false,
                CarIndex    = 0,
                WeaponIndex = 0,
                ScoopIndex  = -1,
            });
        }

        private void RemovePlayer(ulong clientId)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == clientId)
                {
                    _players.RemoveAt(i);
                    return;
                }
            }
        }

        private void UpdatePlayer(ulong clientId, Func<LobbyPlayerData, LobbyPlayerData> mutate)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].ClientId == clientId)
                {
                    // NGO NetworkList не сохраняет изменения через [i] = value.
                    // Единственный надёжный способ обновить элемент — Remove + Insert.
                    var updated = mutate(_players[i]);
                    _players.RemoveAt(i);
                    _players.Insert(i, updated);
                    OnLobbyChanged?.Invoke();
                    return;
                }
            }

            Debug.LogWarning($"[LobbyManager] UpdatePlayer: клиент {clientId} не найден. " +
                             $"Игроков в списке: {_players.Count}");
        }

        // ClientRpc нужен для обновления UI на УДАЛЁННЫХ клиентах (не хост)
        [ClientRpc]
        private void NotifyChangedClientRpc()
        {
            // На хосте уже вызвали OnLobbyChanged в UpdatePlayer, пропускаем дубль
            if (IsHost) return;
            OnLobbyChanged?.Invoke();
        }
    }
}
