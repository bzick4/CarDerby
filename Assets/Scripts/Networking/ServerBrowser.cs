// Assets/Scripts/Networking/ServerBrowser.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace CarDerby.Networking
{
    [Serializable]
    public struct ServerInfo
    {
        public string Name;
        public string IpAddress;
        public ushort Port;
        public int    CurrentPlayers;
        public int    MaxPlayers;
        public bool   HasPassword;
        public string GameMode;
        public int    PingMs;
    }

    /// <summary>
    /// Abstracts server discovery. Currently implements direct-connect + a
    /// manual server list. Swap DiscoverServersAsync() to use UGS Lobby if needed.
    /// </summary>
    public class ServerBrowser : MonoBehaviour
    {
        [SerializeField] private SessionManager _sessionManager;

        // Manual server entries (populated by host registration or a simple relay)
        private readonly List<ServerInfo> _serverList = new();

        public IReadOnlyList<ServerInfo> Servers => _serverList;

        public event Action OnServersRefreshed;

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Populate _serverList. Extend to call UGS QueryLobbiesAsync here.</summary>
        public async Task RefreshAsync()
        {
            _serverList.Clear();

            // Stub: in production replace with UGS Lobby QueryLobbiesAsync or a relay API call.
            await Task.Yield();

            OnServersRefreshed?.Invoke();
        }

        /// <summary>Add a locally-known server (e.g. typed-in IP).</summary>
        public void AddManualServer(string name, string ip, ushort port, bool hasPassword, string gameMode)
        {
            _serverList.Add(new ServerInfo
            {
                Name           = name,
                IpAddress      = ip,
                Port           = port,
                CurrentPlayers = 0,
                MaxPlayers     = 8,
                HasPassword    = hasPassword,
                GameMode       = gameMode,
                PingMs         = -1,
            });

            OnServersRefreshed?.Invoke();
        }

        public void JoinServer(ServerInfo info, string password = "")
        {
            _sessionManager.StartClient(info.IpAddress, info.Port, password);
        }

        // ── Host registration ────────────────────────────────────────────────

        /// <summary>
        /// Call this after StartHost to register this session so LAN clients
        /// can discover it (or forward to a relay API for internet-wide listing).
        /// </summary>
        public void RegisterHostedServer(ServerInfo info)
        {
            // In production: POST to a relay REST endpoint or UGS CreateLobby.
            Debug.Log($"[ServerBrowser] Registered: {info.Name} @ {info.IpAddress}:{info.Port}");
        }
    }
}
