// Assets/Scripts/Networking/LanDiscovery.cs
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace CarDerby.Networking
{
    /// <summary>
    /// UDP broadcast LAN discovery.
    /// Host вызывает StartBroadcasting() — рассылает ServerInfo каждые 2 сек.
    /// Client вызывает StartListening() — собирает ответы в список.
    /// Всё в background-потоках, результаты маршалятся в главный поток.
    /// </summary>
    public class LanDiscovery : MonoBehaviour
    {
        private const int    DISCOVERY_PORT   = 47778;
        private const string BROADCAST_MARKER = "CARDERBY|";

        // ── State ────────────────────────────────────────────────────────────

        private UdpClient _broadcaster;
        private UdpClient _listener;
        private Thread    _broadcastThread;
        private Thread    _listenThread;
        private bool      _running;

        private ServerInfo _hostedInfo;

        // Thread-safe queue to marshal events to main thread
        private readonly Queue<Action> _mainThreadQueue = new();
        private readonly object        _queueLock       = new();

        // Collected servers (key = ip:port)
        private readonly Dictionary<string, ServerInfo> _found = new();
        private readonly object                          _foundLock = new();

        public event Action<ServerInfo> OnServerDiscovered;

        // ── Unity ────────────────────────────────────────────────────────────

        private void Update()
        {
            lock (_queueLock)
            {
                while (_mainThreadQueue.Count > 0)
                    _mainThreadQueue.Dequeue()?.Invoke();
            }
        }

        private void OnDestroy() => Stop();

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Хост вызывает это после StartHost — начинаем рассылать себя.</summary>
        public void StartBroadcasting(ServerInfo info)
        {
            _hostedInfo = info;
            Stop(); // на случай если уже запущено
            _running = true;
            _broadcastThread = new Thread(BroadcastLoop) { IsBackground = true, Name = "LanBroadcast" };
            _broadcastThread.Start();
            Debug.Log($"[LanDiscovery] Broadcasting '{info.Name}' on port {DISCOVERY_PORT}");
        }

        /// <summary>Клиент вызывает это при открытии браузера — слушаем бродкасты.</summary>
        public void StartListening()
        {
            lock (_foundLock) _found.Clear();
            Stop();
            _running = true;
            _listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "LanListen" };
            _listenThread.Start();
            Debug.Log($"[LanDiscovery] Listening for servers on port {DISCOVERY_PORT}");
        }

        public void Stop()
        {
            _running = false;
            try { _broadcaster?.Close(); } catch { /* ignore */ }
            try { _listener?.Close();    } catch { /* ignore */ }
            _broadcaster = null;
            _listener    = null;
        }

        public IReadOnlyCollection<ServerInfo> GetDiscoveredServers()
        {
            lock (_foundLock)
                return new List<ServerInfo>(_found.Values);
        }

        // ── Background threads ───────────────────────────────────────────────

        // Вызывается в главном потоке из Update() через очередь
        private void ParseAndNotify(string json, string senderIp)
        {
            try
            {
                var info = JsonUtility.FromJson<ServerInfo>(json);
                info.IpAddress = senderIp;

                string key = $"{info.IpAddress}:{info.Port}";
                lock (_foundLock) _found[key] = info;

                OnServerDiscovered?.Invoke(info);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LanDiscovery] Failed to parse server info: {e.Message}");
            }
        }

        private void BroadcastLoop()
        {
            try
            {
                _broadcaster = new UdpClient();
                _broadcaster.EnableBroadcast = true;

                // Получаем все адреса подсети (192.168.x.255 и т.п.) + loopback для теста на одной машине
                var targets = GetBroadcastAddresses();

                while (_running)
                {
                    string json  = JsonUtility.ToJson(_hostedInfo);
                    byte[] bytes = Encoding.UTF8.GetBytes(BROADCAST_MARKER + json);

                    foreach (var addr in targets)
                    {
                        try
                        {
                            var ep = new IPEndPoint(addr, DISCOVERY_PORT);
                            _broadcaster.Send(bytes, bytes.Length, ep);
                        }
                        catch { /* один адрес недоступен — пропускаем */ }
                    }

                    Thread.Sleep(2000);
                }
            }
            catch (Exception e)
            {
                if (_running) Debug.LogWarning($"[LanDiscovery] Broadcast error: {e.Message}");
            }
        }

        /// <summary>Возвращает broadcast-адреса всех активных сетевых интерфейсов + loopback.</summary>
        private static List<IPAddress> GetBroadcastAddresses()
        {
            var list = new List<IPAddress> { IPAddress.Loopback }; // всегда добавляем loopback

            try
            {
                foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (iface.OperationalStatus    != OperationalStatus.Up)          continue;

                    foreach (var addr in iface.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;

                        byte[] ip   = addr.Address.GetAddressBytes();
                        byte[] mask = addr.IPv4Mask.GetAddressBytes();
                        var    bc   = new byte[4];
                        for (int i = 0; i < 4; i++)
                            bc[i] = (byte)(ip[i] | ~mask[i]);

                        list.Add(new IPAddress(bc));
                    }
                }
            }
            catch { /* не смогли получить интерфейсы — используем только loopback */ }

            return list;
        }

        private void ListenLoop()
        {
            try
            {
                _listener = new UdpClient();
                _listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Client.Bind(new IPEndPoint(IPAddress.Any, DISCOVERY_PORT));
                _listener.Client.ReceiveTimeout = 1000;
                var from = new IPEndPoint(IPAddress.Any, 0);

                while (_running)
                {
                    try
                    {
                        byte[] data = _listener.Receive(ref from);
                        string msg  = Encoding.UTF8.GetString(data);

                        if (!msg.StartsWith(BROADCAST_MARKER)) continue;

                        // JsonUtility — Unity API, нельзя вызывать из фонового потока.
                        // Передаём сырые данные в главный поток для парсинга.
                        string json       = msg.Substring(BROADCAST_MARKER.Length);
                        string senderIp   = from.Address.ToString();

                        lock (_queueLock)
                            _mainThreadQueue.Enqueue(() => ParseAndNotify(json, senderIp));
                    }
                    catch (SocketException) { /* таймаут — нормально */ }
                }
            }
            catch (Exception e)
            {
                if (_running) Debug.LogWarning($"[LanDiscovery] Listen error: {e.Message}");
            }
        }
    }
}
