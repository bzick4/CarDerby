// Assets/Scripts/Networking/SessionManager.cs
using System;
using System.Text;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace CarDerby.Networking
{
    /// <summary>
    /// Manages the NGO NetworkManager lifecycle: Host / Server / Client.
    /// Handles connection approval (password check) and disconnect cleanup.
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        [SerializeField] private NetworkManager _networkManager;

        private string _serverPassword = string.Empty;

        public bool IsHost   => _networkManager.IsHost;
        public bool IsServer => _networkManager.IsServer;
        public bool IsClient => _networkManager.IsClient;

        public event Action<ulong> OnPlayerConnected;
        public event Action<ulong> OnPlayerDisconnected;
        public event Action        OnConnectionFailed;

        private void Awake()
        {
            _networkManager.OnClientConnectedCallback    += id => OnPlayerConnected?.Invoke(id);
            _networkManager.OnClientDisconnectCallback   += id => OnPlayerDisconnected?.Invoke(id);
        }

        // ── Public API ───────────────────────────────────────────────────────

        public void StartHost(string ip, ushort port, string password = "")
        {
            _serverPassword = password;
            ConfigureTransport(ip, port);
            // ConnectionApproval должен быть включён ДО назначения callback — иначе NGO warning
            _networkManager.NetworkConfig.ConnectionApproval = true;
            _networkManager.ConnectionApprovalCallback = HandleApproval;
            _networkManager.StartHost();
        }

        public void StartDedicatedServer(string ip, ushort port, string password = "")
        {
            _serverPassword = password;
            ConfigureTransport(ip, port);
            _networkManager.ConnectionApprovalCallback = HandleApproval;
            _networkManager.StartServer();
        }

        public void StartClient(string ip, ushort port, string password = "")
        {
            ConfigureTransport(ip, port);
            // Должно совпадать с настройкой хоста иначе NGO отклонит по хэшу конфига
            _networkManager.NetworkConfig.ConnectionApproval = true;
            // Embed password in connection payload so the server can validate it
            _networkManager.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(password);
            _networkManager.OnClientDisconnectCallback  += HandleClientDisconnect;
            _networkManager.StartClient();
        }

        public void Disconnect()
        {
            _networkManager.Shutdown();
        }

        // ── Internals ────────────────────────────────────────────────────────

        private void ConfigureTransport(string ip, ushort port)
        {
            var transport = _networkManager.GetComponent<UnityTransport>();
            if (transport != null)
                transport.SetConnectionData(ip, port);
        }

        private void HandleApproval(
            NetworkManager.ConnectionApprovalRequest  request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            string clientPassword = Encoding.UTF8.GetString(request.Payload ?? Array.Empty<byte>());
            bool   approved       = string.IsNullOrEmpty(_serverPassword) || clientPassword == _serverPassword;

            response.Approved          = approved;
            response.CreatePlayerObject = approved;
            response.Pending           = false;

            if (!approved)
                Debug.LogWarning($"[Session] Client {request.ClientNetworkId} rejected: bad password.");
        }

        private void HandleClientDisconnect(ulong clientId)
        {
            // If we are the local client (clientId 0 indicates self-disconnect)
            if (!_networkManager.IsConnectedClient)
                OnConnectionFailed?.Invoke();
        }
    }
}
