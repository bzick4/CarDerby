// Assets/Scripts/Gameplay/PlayerSpawner.cs
using UnityEngine;
using Unity.Netcode;
using CarDerby.SO;
using CarDerby.Combat;

namespace CarDerby.Gameplay
{
    /// <summary>
    /// Server-only NetworkBehaviour placed in GameScene.
    /// Reads MatchData (filled by LobbyUI before scene load) and spawns
    /// each connected player's chosen car prefab with NGO ownership.
    ///
    /// Setup:
    ///   1. Attach to a GameObject in GameScene.
    ///   2. В _carRoster перетащи те же CarDataSO-ассеты что и в CarSelectionSystem в Lobby
    ///      (ассеты живут в Assets/ — их можно присвоить из любой сцены).
    ///   3. Assign one or more SpawnPoints (empty Transforms) in _spawnPoints.
    /// </summary>
    public class PlayerSpawner : NetworkBehaviour
    {
        [SerializeField] private CarDataSO[]    _carRoster    = new CarDataSO[0];
        [SerializeField] private WeaponDataSO[] _weaponRoster = new WeaponDataSO[0]; // те же SO что в лобби
        [SerializeField] private Transform[]    _spawnPoints  = new Transform[0];

        // ── NGO lifecycle ────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            // NGO has already loaded the scene and connected all clients by the time
            // OnNetworkSpawn fires, so we can spawn immediately.
            SpawnAllPlayers();
        }

        // ── Spawn logic ──────────────────────────────────────────────────────

        private void SpawnAllPlayers()
        {
            int spawnIndex = 0;

            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                SpawnPlayerCar(clientId, spawnIndex);
                spawnIndex++;
            }
        }

        private void SpawnPlayerCar(ulong clientId, int spawnIndex)
        {
            // Determine which car prefab to use
            GameObject prefab = null;

            if (MatchData.TryGetLoadout(clientId, out var loadout))
            {
                prefab = GetPrefabByIndex(loadout.CarIndex);
            }

            // Fallback: use index 0 if no loadout or prefab found
            if (prefab == null)
            {
                prefab = GetPrefabByIndex(0);
                Debug.LogWarning($"[PlayerSpawner] No loadout for client {clientId}, using car 0.");
            }

            if (prefab == null)
            {
                Debug.LogError("[PlayerSpawner] No car prefab found — перетащи CarDataSO-ассеты в поле Car Roster на PlayerSpawner.");
                return;
            }

            // Pick a spawn position (cycle through available points)
            Vector3    spawnPos = GetSpawnPosition(spawnIndex);
            Quaternion spawnRot = GetSpawnRotation(spawnIndex);

            // Instantiate and spawn with ownership
            var instance = Instantiate(prefab, spawnPos, spawnRot);
            var netObj   = instance.GetComponent<NetworkObject>();

            if (netObj == null)
            {
                Debug.LogError($"[PlayerSpawner] Prefab '{prefab.name}' has no NetworkObject component.");
                Destroy(instance);
                return;
            }

            netObj.SpawnWithOwnership(clientId);

            // Инжектируем WeaponDataSO выбранный игроком в лобби
            InjectWeaponData(instance, loadout.WeaponIndex);

            Debug.Log($"[PlayerSpawner] Spawned '{prefab.name}' for client {clientId} at {spawnPos}");
        }

        // ── Weapon injection ─────────────────────────────────────────────────

        private void InjectWeaponData(GameObject carInstance, int weaponIndex)
        {
            if (_weaponRoster == null || _weaponRoster.Length == 0) return;

            int idx  = Mathf.Clamp(weaponIndex, 0, _weaponRoster.Length - 1);
            var data = _weaponRoster[idx];
            if (data == null || data.WeaponPrefab == null) return;

            // Ищем точку крепления на этой машине, если нет — вешаем на корень
            var slot  = carInstance.GetComponentInChildren<Car.CarWeaponSlot>();
            Transform mount = slot != null ? slot.transform : carInstance.transform;

            var weaponObj = Instantiate(data.WeaponPrefab, mount);
            weaponObj.transform.localPosition = Vector3.zero;
            // localRotation не трогаем — берём из префаба оружия

            // Если в префабе оружия есть WeaponController — подставляем SO со статами
            var controller = weaponObj.GetComponent<WeaponController>();
            if (controller != null)
                controller.SetWeaponData(data);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private GameObject GetPrefabByIndex(int index)
        {
            if (_carRoster == null || _carRoster.Length == 0) return null;
            int clamped = Mathf.Clamp(index, 0, _carRoster.Length - 1);
            return _carRoster[clamped]?.NetworkPrefab;
        }

        private Vector3 GetSpawnPosition(int index)
        {
            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                var pt = _spawnPoints[index % _spawnPoints.Length];
                if (pt != null) return pt.position;
            }

            // Fallback: spread players in a line if no spawn points assigned
            return new Vector3(index * 4f, 0f, 0f);
        }

        private Quaternion GetSpawnRotation(int index)
        {
            if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                var pt = _spawnPoints[index % _spawnPoints.Length];
                if (pt != null) return pt.rotation;
            }
            return Quaternion.identity;
        }
    }
}
