// Assets/Scripts/UI/MainMenuUI.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace CarDerby.UI
{
    /// <summary>
    /// Drives MainMenu.uxml.
    ///
    /// Scene-loading rule:
    ///   Host  → StartHost() затем NetworkManager.SceneManager.LoadScene()
    ///           Это обязательно — только так NGO знает о NetworkObject-ах в Lobby сцене
    ///           и корректно спавнит их (LobbyManager и др.).
    ///   Client → StartClient(), больше ничего.
    ///           Когда сервер загрузит Lobby, NGO автоматически синхронизирует сцену клиенту.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Networking.SessionManager _session;
        [SerializeField] private Networking.ServerBrowser  _browser;
        [SerializeField] private string _lobbySceneName = "Lobby";

        private UIDocument    _doc;
        private VisualElement _mainPanel, _hostPanel, _joinPanel, _browserPanel;
        private VisualElement _passwordOverlay;
        private Networking.ServerInfo _pendingServer;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;

            _mainPanel       = root.Q("main-panel");
            _hostPanel       = root.Q("host-panel");
            _joinPanel       = root.Q("join-panel");
            _browserPanel    = root.Q("browser-panel");
            _passwordOverlay = root.Q("password-overlay");

            root.Q<Button>("host-btn")  .clicked += () => ShowPanel(_hostPanel);
            root.Q<Button>("join-btn")  .clicked += () => ShowPanel(_joinPanel);
            root.Q<Button>("browse-btn").clicked += OpenBrowser;
            root.Q<Button>("quit-btn")  .clicked += Application.Quit;

            root.Q<Button>("confirm-host-btn").clicked += OnConfirmHost;
            root.Q<Button>("host-back-btn")   .clicked += () => ShowPanel(_mainPanel);

            root.Q<Button>("confirm-join-btn").clicked += OnConfirmJoin;
            root.Q<Button>("join-back-btn")   .clicked += () => ShowPanel(_mainPanel);

            root.Q<Button>("refresh-btn")        .clicked += () => _ = _browser.RefreshAsync();
            root.Q<Button>("browser-back-btn")   .clicked += () => ShowPanel(_mainPanel);
            root.Q<Button>("browser-connect-btn").clicked += OnPasswordConnect;
            root.Q<Button>("browser-cancel-btn") .clicked += () => ShowPasswordOverlay(false);

            _session.OnConnectionFailed += OnConnectionFailed;
            _browser.OnServersRefreshed += RebuildServerList;

            ShowPanel(_mainPanel);
        }

        private void OnDisable()
        {
            _session.OnConnectionFailed -= OnConnectionFailed;
            _browser.OnServersRefreshed -= RebuildServerList;
        }

        // ── Handlers ─────────────────────────────────────────────────────────

        private void OnConfirmHost()
        {
            var root    = _doc.rootVisualElement;
            string name = root.Q<TextField>("host-name").value;
            ushort port = FindFreePort();
            string pass = root.Q<TextField>("host-password").value;

            _session.StartHost("0.0.0.0", port, pass);

            // StartHost может упасть (занятый порт и т.п.) — проверяем до загрузки сцены
            if (!NetworkManager.Singleton.IsHost)
            {
                Debug.LogError($"[MainMenuUI] StartHost не удался — порт {port} занят или другая ошибка.");
                return;
            }

            _browser.RegisterHostedServer(new Networking.ServerInfo
            {
                Name        = string.IsNullOrWhiteSpace(name) ? "My Derby" : name,
                IpAddress   = "127.0.0.1",
                Port        = port,
                MaxPlayers  = 8,
                HasPassword = !string.IsNullOrEmpty(pass),
                GameMode    = "Deathmatch",
            });

            // NGO при старте хоста запускает внутренний SceneEvent (синхронизацию).
            // Ждём его завершения — только потом грузим лобби.
            StartCoroutine(LoadLobbyWhenReady(_lobbySceneName));
        }

        private void OnConfirmJoin()
        {
            var root    = _doc.rootVisualElement;
            string ip   = root.Q<TextField>("join-ip").value;
            ushort port = ParsePort(root.Q<TextField>("join-port").value);
            string pass = root.Q<TextField>("join-password").value;

            if (string.IsNullOrWhiteSpace(ip)) ip = "127.0.0.1";

            // Клиент только коннектится — сцену ему загрузит сервер через NGO.
            _session.StartClient(ip, port, pass);
        }

        private IEnumerator LoadLobbyWhenReady(string sceneName)
        {
            // Ждём пока NGO завершит внутренний scene event (до 3 сек)
            float timeout = 3f;
            while (timeout > 0f)
            {
                var status = NetworkManager.Singleton.SceneManager
                    .LoadScene(sceneName, LoadSceneMode.Single);

                if (status == SceneEventProgressStatus.Started)
                    yield break; // успешно запустили загрузку

                if (status != SceneEventProgressStatus.SceneEventInProgress)
                {
                    Debug.LogError($"[MainMenuUI] LoadScene вернул: {status}");
                    yield break;
                }

                yield return null; // ждём кадр и пробуем снова
                timeout -= Time.deltaTime;
            }

            Debug.LogError("[MainMenuUI] Таймаут ожидания SceneEvent — лобби не загружено.");
        }

        private async void OpenBrowser()
        {
            ShowPanel(_browserPanel);
            await _browser.RefreshAsync();
        }

        private void OnConnectionFailed()
        {
            ShowPanel(_mainPanel);
            Debug.LogWarning("[MainMenuUI] Connection failed — wrong password or server offline.");
        }

        // ── Server browser ────────────────────────────────────────────────────

        private void RebuildServerList()
        {
            var list = _doc.rootVisualElement.Q<ScrollView>("server-list");
            list.Clear();

            foreach (var srv in _browser.Servers)
            {
                var row = new VisualElement();
                row.AddToClassList("server-row");

                var nameLabel = new Label(srv.Name);
                nameLabel.AddToClassList("server-row-name");

                var infoLabel = new Label(
                    $"{srv.CurrentPlayers}/{srv.MaxPlayers}  {srv.GameMode}{(srv.HasPassword ? "  🔒" : "")}");
                infoLabel.AddToClassList("server-row-info");

                var captured = srv;
                var joinBtn = new Button(() =>
                {
                    if (captured.HasPassword)
                    {
                        _pendingServer = captured;
                        ShowPasswordOverlay(true);
                    }
                    else
                    {
                        _browser.JoinServer(captured);
                    }
                }) { text = "JOIN" };
                joinBtn.AddToClassList("menu-btn");
                joinBtn.AddToClassList("server-row-join");

                row.Add(nameLabel);
                row.Add(infoLabel);
                row.Add(joinBtn);
                list.Add(row);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Загружает сцену через NGO SceneManager.
        /// Возвращает false если сцена не добавлена в Build Profiles.
        /// </summary>
        private bool TryNgoLoadScene(string sceneName)
        {
            // Проверяем что сцена зарегистрирована в Build Profiles
            bool inBuild =
                SceneUtility.GetBuildIndexByScenePath(sceneName) >= 0 ||
                SceneUtility.GetBuildIndexByScenePath($"Assets/Scenes/{sceneName}.unity") >= 0;

            if (!inBuild)
            {
                Debug.LogError(
                    $"[MainMenuUI] Сцена '{sceneName}' не добавлена в Build Profiles.\n" +
                    "File → Build Profiles → Shared scenes list → добавь все три сцены.");
                return false;
            }

            var status = NetworkManager.Singleton.SceneManager.LoadScene(
                sceneName, LoadSceneMode.Single);

            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogError($"[MainMenuUI] NGO SceneManager.LoadScene вернул: {status}");
                return false;
            }

            return true;
        }

        private void ShowPasswordOverlay(bool show)
        {
            _passwordOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show)
                _doc.rootVisualElement.Q<TextField>("browser-password").value = "";
        }

        private void OnPasswordConnect()
        {
            string pass = _doc.rootVisualElement.Q<TextField>("browser-password").value;
            ShowPasswordOverlay(false);
            _browser.JoinServer(_pendingServer, pass);
        }

        private void ShowPanel(VisualElement target)
        {
            _mainPanel   .style.display = target == _mainPanel    ? DisplayStyle.Flex : DisplayStyle.None;
            _hostPanel   .style.display = target == _hostPanel    ? DisplayStyle.Flex : DisplayStyle.None;
            _joinPanel   .style.display = target == _joinPanel    ? DisplayStyle.Flex : DisplayStyle.None;
            _browserPanel.style.display = target == _browserPanel ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static ushort FindFreePort(ushort start = 7777)
        {
            for (ushort port = start; port <= 9999; port++)
            {
                if (IsUdpPortFree(port)) return port;
            }
            for (ushort port = 1024; port < start; port++)
            {
                if (IsUdpPortFree(port)) return port;
            }
            return start;
        }

        private static bool IsUdpPortFree(ushort port)
        {
            try
            {
                var u = new System.Net.Sockets.UdpClient(port);
                u.Close();
                return true;
            }
            catch { return false; }
        }

        private static ushort ParsePort(string s) =>
            ushort.TryParse(s, out ushort p) ? p : (ushort)7777;
    }
}
