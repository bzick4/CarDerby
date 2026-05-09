// Assets/Scripts/Networking/AuthManager.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;

namespace CarDerby.Networking
{
    public class AuthManager : MonoBehaviour
    {
        public static AuthManager Instance { get; private set; }

        private const string NicknameKey = "nickname";

        public string  PlayerNickname { get; private set; } = "";
        public bool    IsSignedIn     => AuthenticationService.Instance.IsSignedIn;
        public bool    IsReady        { get; private set; }

        public event Action        OnSignedIn;
        public event Action<string> OnNicknameChanged;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ── Public API ───────────────────────────────────────────────────────

        public async Task InitializeAsync()
        {
            // Уже инициализированы (DontDestroyOnLoad пережил смену сцены) — просто дёргаем события
            if (IsReady)
            {
                OnSignedIn?.Invoke();
                OnNicknameChanged?.Invoke(PlayerNickname);
                return;
            }

            try
            {
                // Исключаем Vivox из инициализации — он требует настройки сервера, которой у нас нет
                var options = new Unity.Services.Core.InitializationOptions();
                options.SetOption("com.unity.services.vivox.server", "none");
                await UnityServices.InitializeAsync(options);

                if (!AuthenticationService.Instance.IsSignedIn)
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();

                IsReady = true;
                OnSignedIn?.Invoke();

                await LoadNicknameAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthManager] Sign-in failed: {e.Message}");
                PlayerNickname = PlayerPrefs.GetString("PlayerNickname", "Player");
                IsReady = true;
                OnSignedIn?.Invoke();
                OnNicknameChanged?.Invoke(PlayerNickname);
            }
        }

        public async Task SaveNicknameAsync(string nickname)
        {
            nickname = nickname.Trim();
            if (nickname.Length == 0) return;
            if (nickname.Length > 20) nickname = nickname.Substring(0, 20);

            PlayerNickname = nickname;
            PlayerPrefs.SetString("PlayerNickname", nickname);
            PlayerPrefs.Save();
            OnNicknameChanged?.Invoke(nickname);

            if (!IsSignedIn) return;
            try
            {
                await CloudSaveService.Instance.Data.Player.SaveAsync(
                    new Dictionary<string, object> { { NicknameKey, nickname } });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthManager] Cloud Save write failed: {e.Message}");
            }
        }

        // ── Private ──────────────────────────────────────────────────────────

        private async Task LoadNicknameAsync()
        {
            try
            {
                var data = await CloudSaveService.Instance.Data.Player.LoadAsync(
                    new HashSet<string> { NicknameKey });

                if (data.TryGetValue(NicknameKey, out var item))
                    PlayerNickname = item.Value.GetAs<string>();
                else
                    PlayerNickname = FallbackNickname();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AuthManager] Cloud Save read failed: {e.Message}");
                PlayerNickname = FallbackNickname();
            }

            PlayerPrefs.SetString("PlayerNickname", PlayerNickname);
            PlayerPrefs.Save();
            OnNicknameChanged?.Invoke(PlayerNickname);
        }

        private static string FallbackNickname()
        {
            string saved = PlayerPrefs.GetString("PlayerNickname", "");
            if (!string.IsNullOrWhiteSpace(saved)) return saved;
            string id = AuthenticationService.Instance.PlayerId ?? "????";
            return "Player_" + id.Substring(0, Mathf.Min(6, id.Length));
        }
    }
}
