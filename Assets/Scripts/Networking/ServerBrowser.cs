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
    /// Обёртка над LanDiscovery.
    /// RefreshAsync() запускает UDP-прослушивание и ждёт 2 сек для сбора ответов.
    /// RegisterHostedServer() запускает UDP-рассылку.
    /// </summary>
    public class ServerBrowser : MonoBehaviour
    {
        [SerializeField] private SessionManager _sessionManager;
        [SerializeField] private LanDiscovery   _lanDiscovery;

        private readonly List<ServerInfo> _serverList = new();

        public IReadOnlyList<ServerInfo> Servers => _serverList;

        public event Action OnServersRefreshed;

        private void Awake()
        {
            if (_lanDiscovery != null)
                _lanDiscovery.OnServerDiscovered += OnServerDiscovered;
        }

        private void OnDestroy()
        {
            if (_lanDiscovery != null)
                _lanDiscovery.OnServerDiscovered -= OnServerDiscovered;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Запускает прослушивание LAN и ждёт 2 сек для сбора серверов.</summary>
        public async Task RefreshAsync()
        {
            _serverList.Clear();
            OnServersRefreshed?.Invoke(); // очищаем UI сразу

            if (_lanDiscovery == null)
            {
                Debug.LogWarning("[ServerBrowser] LanDiscovery не назначен в Inspector.");
                return;
            }

            _lanDiscovery.StartListening();

            // Ждём пока серверы ответят
            await Task.Delay(2000);

            // Забираем всё что нашли
            _serverList.Clear();
            foreach (var info in _lanDiscovery.GetDiscoveredServers())
                _serverList.Add(info);

            OnServersRefreshed?.Invoke();
        }

        /// <summary>Регистрирует хост — начинает рассылку по LAN.</summary>
        public void RegisterHostedServer(ServerInfo info)
        {
            if (_lanDiscovery != null)
                _lanDiscovery.StartBroadcasting(info);

            Debug.Log($"[ServerBrowser] Hosting: {info.Name} @ {info.IpAddress}:{info.Port}");
        }

        public void JoinServer(ServerInfo info, string password = "")
        {
            _lanDiscovery?.Stop();
            _sessionManager.StartClient(info.IpAddress, info.Port, password);
        }

        // ── Private ──────────────────────────────────────────────────────────

        private void OnServerDiscovered(ServerInfo info)
        {
            // Обновляем в реальном времени — добавляем/обновляем в списке
            bool found = false;
            for (int i = 0; i < _serverList.Count; i++)
            {
                if (_serverList[i].IpAddress == info.IpAddress && _serverList[i].Port == info.Port)
                {
                    _serverList[i] = info;
                    found = true;
                    break;
                }
            }
            if (!found) _serverList.Add(info);

            OnServersRefreshed?.Invoke();
        }
    }
}
