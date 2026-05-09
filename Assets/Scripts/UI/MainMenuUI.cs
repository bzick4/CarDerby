// Assets/Scripts/UI/MainMenuUI.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace CarDerby.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private Networking.SessionManager _session;
        [SerializeField] private Networking.ServerBrowser  _browser;
        [SerializeField] private Networking.AuthManager    _auth;
        [SerializeField] private string _lobbySceneName = "Lobby";

        private UIDocument    _doc;
        private VisualElement _mainPanel, _hostPanel, _joinPanel, _browserPanel;
        private VisualElement _passwordOverlay;
        private Networking.ServerInfo _pendingServer;

        // ── Auth UI refs ─────────────────────────────────────────────────────
        private Label         _authStatus;
        private VisualElement _authSignedIn;
        private Label         _authNickname;
        private VisualElement _authEditRow;

        private void OnEnable()
        {
            _doc = GetComponent<UIDocument>();
            var root = _doc.rootVisualElement;

            _mainPanel       = root.Q("main-panel");
            _hostPanel       = root.Q("host-panel");
            _joinPanel       = root.Q("join-panel");
            _browserPanel    = root.Q("browser-panel");
            _passwordOverlay = root.Q("password-overlay");

            _authStatus   = root.Q<Label>("auth-status");
            _authSignedIn = root.Q("auth-signed-in");
            _authNickname = root.Q<Label>("auth-nickname");
            _authEditRow  = root.Q("auth-edit-row");

            // Main menu
            root.Q<Button>("host-btn")  .clicked += () => ShowPanel(_hostPanel);
            root.Q<Button>("join-btn")  .clicked += () => ShowPanel(_joinPanel);
            root.Q<Button>("browse-btn").clicked += OpenBrowser;
            root.Q<Button>("quit-btn")  .clicked += Application.Quit;

            // Host / Join panels
            root.Q<Button>("confirm-host-btn").clicked += OnConfirmHost;
            root.Q<Button>("host-back-btn")   .clicked += () => ShowPanel(_mainPanel);
            root.Q<Button>("confirm-join-btn").clicked += OnConfirmJoin;
            root.Q<Button>("join-back-btn")   .clicked += () => ShowPanel(_mainPanel);

            // Server browser
            root.Q<Button>("refresh-btn")        .clicked += () => _ = _browser.RefreshAsync();
            root.Q<Button>("browser-back-btn")   .clicked += () => ShowPanel(_mainPanel);
            root.Q<Button>("browser-connect-btn").clicked += OnPasswordConnect;
            root.Q<Button>("browser-cancel-btn") .clicked += () => ShowPasswordOverlay(false);

            // Auth panel
            root.Q<Button>("auth-edit-btn").clicked += OnEditNickname;
            root.Q<Button>("auth-save-btn").clicked += OnSaveNickname;

            _session.OnConnectionFailed += OnConnectionFailed;
            _browser.OnServersRefreshed += RebuildServerList;

            if (_auth != null)
            {
                _auth.OnSignedIn        += OnSignedIn;
                _auth.OnNicknameChanged += OnNicknameChanged;
            }

            ShowPanel(_mainPanel);
            SetMainButtonsEnabled(false);

            // Запускаем авторизацию — кнопки разблокируются когда готово
            if (_auth != null && !_auth.IsReady)
                _ = _auth.InitializeAsync();
            else if (_auth != null && _auth.IsReady)
                ApplySignedInState(_auth.PlayerNickname);
            else
                SetMainButtonsEnabled(true); // AuthManager не назначен — работаем без него
        }

        private void OnDisable()
        {
            _session.OnConnectionFailed -= OnConnectionFailed;
            _browser.OnServersRefreshed -= RebuildServerList;

            if (_auth != null)
            {
                _auth.OnSignedIn        -= OnSignedIn;
                _auth.OnNicknameChanged -= OnNicknameChanged;
            }
        }

        // ── Auth callbacks ────────────────────────────────────────────────────

        private void OnSignedIn()
        {
            SetMainButtonsEnabled(true);
        }

        private void OnNicknameChanged(string nickname)
        {
            ApplySignedInState(nickname);
        }

        private void ApplySignedInState(string nickname)
        {
            _authStatus.style.display   = DisplayStyle.None;
            _authSignedIn.style.display = DisplayStyle.Flex;
            _authNickname.text          = nickname;
            SetMainButtonsEnabled(true);
        }

        private void OnEditNickname()
        {
            _authEditRow.style.display  = DisplayStyle.Flex;
            _authSignedIn.style.display = DisplayStyle.None;
            var field = _doc.rootVisualElement.Q<TextField>("auth-nickname-field");
            field.value = _auth?.PlayerNickname ?? "";
            field.Focus();
        }

        private void OnSaveNickname()
        {
            var field    = _doc.rootVisualElement.Q<TextField>("auth-nickname-field");
            string nick  = field.value;
            _authEditRow.style.display  = DisplayStyle.None;
            _authSignedIn.style.display = DisplayStyle.Flex;
            if (_auth != null) _ = _auth.SaveNicknameAsync(nick);
            else _authNickname.text = nick;
        }

        private void SetMainButtonsEnabled(bool enabled)
        {
            var root = _doc?.rootVisualElement;
            if (root == null) return;
            root.Q<Button>("host-btn")  ?.SetEnabled(enabled);
            root.Q<Button>("join-btn")  ?.SetEnabled(enabled);
            root.Q<Button>("browse-btn")?.SetEnabled(enabled);

            if (!enabled)
                _authStatus.style.display = DisplayStyle.Flex;
        }

        // ── Host / Join ───────────────────────────────────────────────────────

        private void OnConfirmHost()
        {
            var root    = _doc.rootVisualElement;
            string name = root.Q<TextField>("host-name").value;
            ushort port = FindFreePort();
            string pass = root.Q<TextField>("host-password").value;

            _session.StartHost("0.0.0.0", port, pass);

            if (!NetworkManager.Singleton.IsHost)
            {
                Debug.LogError($"[MainMenuUI] StartHost не удался — порт {port}.");
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

            StartCoroutine(LoadLobbyWhenReady(_lobbySceneName));
        }

        private void OnConfirmJoin()
        {
            var root    = _doc.rootVisualElement;
            string ip   = root.Q<TextField>("join-ip").value;
            ushort port = ParsePort(root.Q<TextField>("join-port").value);
            string pass = root.Q<TextField>("join-password").value;
            if (string.IsNullOrWhiteSpace(ip)) ip = "127.0.0.1";
            _session.StartClient(ip, port, pass);
        }

        private IEnumerator LoadLobbyWhenReady(string sceneName)
        {
            float timeout = 3f;
            while (timeout > 0f)
            {
                var status = NetworkManager.Singleton.SceneManager
                    .LoadScene(sceneName, LoadSceneMode.Single);
                if (status == SceneEventProgressStatus.Started) yield break;
                if (status != SceneEventProgressStatus.SceneEventInProgress)
                {
                    Debug.LogError($"[MainMenuUI] LoadScene вернул: {status}");
                    yield break;
                }
                yield return null;
                timeout -= Time.deltaTime;
            }
            Debug.LogError("[MainMenuUI] Таймаут ожидания SceneEvent.");
        }

        private async void OpenBrowser()
        {
            ShowPanel(_browserPanel);
            await _browser.RefreshAsync();
        }

        private void OnConnectionFailed()
        {
            ShowPanel(_mainPanel);
            Debug.LogWarning("[MainMenuUI] Connection failed.");
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
                    if (captured.HasPassword) { _pendingServer = captured; ShowPasswordOverlay(true); }
                    else _browser.JoinServer(captured);
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

        private void ShowPasswordOverlay(bool show)
        {
            _passwordOverlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) _doc.rootVisualElement.Q<TextField>("browser-password").value = "";
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
                if (IsUdpPortFree(port)) return port;
            for (ushort port = 1024; port < start; port++)
                if (IsUdpPortFree(port)) return port;
            return start;
        }

        private static bool IsUdpPortFree(ushort port)
        {
            try { var u = new System.Net.Sockets.UdpClient(port); u.Close(); return true; }
            catch { return false; }
        }

        private static ushort ParsePort(string s) =>
            ushort.TryParse(s, out ushort p) ? p : (ushort)7777;
    }
}
