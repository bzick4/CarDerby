// Assets/Scripts/Combat/ProjectilePool.cs
using UnityEngine;
using Unity.Netcode;

namespace CarDerby.Combat
{
    /// <summary>
    /// Пул снарядов одного типа. Регистрируется в NGO как INetworkPrefabInstanceHandler.
    ///
    /// Стратегия:
    ///   Сервер    — кольцевой буфер: решает КАКОЙ слот занять, при заполнении
    ///               принудительно гасит старейший снаряд (строгий лимит).
    ///   Клиенты   — свободный список: возвращают любой свободный слот.
    ///               NGO автоматически присваивает правильный NetworkObjectId —
    ///               клиенту без разницы какой именно слот вернуть.
    ///
    /// Лимит _poolSize — ОБЩИЙ на всех игроков.
    /// Хочешь 15 на игрока → выставь poolSize = 15 × maxPlayers.
    ///
    /// Размещение в сцене:
    ///   GameObject «ProjectilePools» → добавь по одному ProjectilePool
    ///   на каждый тип снаряда (Bullet, Rocket, Bomb).
    /// </summary>
    public class ProjectilePool : MonoBehaviour, INetworkPrefabInstanceHandler
    {
        [SerializeField] private GameObject _prefab;

        [Tooltip("Лимит снарядов этого типа на всех игроков суммарно.\n" +
                 "Миниган: 15 × кол-во игроков (10 выстр/с × 1.5с ≈ 15 на игрока).\n" +
                 "Ракета/Бомба: 5-8 хватит.")]
        [SerializeField] private int _poolSize = 15;

        private NetworkObject[] _ring;
        private int             _serverHead;   // только на сервере
        private bool            _registered;

        // ── Init ─────────────────────────────────────────────────────────────

        private void Start()
        {
            if (_prefab == null)
            {
                Debug.LogError("[ProjectilePool] Prefab не назначен!", this);
                return;
            }

            _ring = new NetworkObject[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var go = Instantiate(_prefab);
                go.SetActive(false);
                go.name = $"{_prefab.name}_slot{i}";
                _ring[i] = go.GetComponent<NetworkObject>();

                if (_ring[i] == null)
                    Debug.LogError($"[ProjectilePool] '{_prefab.name}' не имеет NetworkObject!", _prefab);
            }

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.PrefabHandler.AddHandler(_prefab, this);
                _registered = true;
            }
        }

        private void OnDestroy()
        {
            if (_registered && NetworkManager.Singleton != null)
                NetworkManager.Singleton.PrefabHandler.RemoveHandler(_prefab);
        }

        // ── INetworkPrefabInstanceHandler ────────────────────────────────────

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            NetworkObject slot;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                // ── Сервер: кольцевой буфер ──────────────────────────────────
                slot = _ring[_serverHead];
                _serverHead = (_serverHead + 1) % _poolSize;

                // Слот занят — принудительно гасим старейший (строгий лимит)
                if (slot.gameObject.activeSelf)
                {
                    if (slot.IsSpawned)
                        slot.Despawn(destroy: false);   // уведомит клиентов об исчезновении
                    else
                        slot.gameObject.SetActive(false);
                }
            }
            else
            {
                // ── Клиент: первый свободный слот ────────────────────────────
                // NGO присвоит правильный NetworkObjectId сам — нам важно
                // лишь вернуть незанятый объект.
                slot = FindFreeSlot();

                if (slot == null)
                {
                    // Все заняты (клиент отстаёт) — отдаём самый старый
                    slot = _ring[0];
                    slot.gameObject.SetActive(false);
                }
            }

            slot.transform.SetPositionAndRotation(position, rotation);
            slot.gameObject.SetActive(true);
            return slot;
        }

        public void Destroy(NetworkObject networkObject)
        {
            networkObject.gameObject.SetActive(false);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private NetworkObject FindFreeSlot()
        {
            foreach (var slot in _ring)
                if (slot != null && !slot.gameObject.activeSelf)
                    return slot;
            return null;
        }

        // ── Debug ─────────────────────────────────────────────────────────────

        public int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (var s in _ring) if (s != null && s.gameObject.activeSelf) n++;
                return n;
            }
        }
    }
}
