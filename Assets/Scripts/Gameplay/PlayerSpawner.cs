// Assets/Scripts/Gameplay/PlayerSpawner.cs
using UnityEngine;
using Unity.Netcode;
using CarDerby.SO;
using CarDerby.Car;
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
        [SerializeField] private WeaponDataSO[] _weaponRoster = new WeaponDataSO[0];
        [SerializeField] private WeaponDataSO[] _scoopRoster  = new WeaponDataSO[0]; // только ковши (IsFrontScoop = true)
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

            // Применяем данные SO ДО спавна — чтобы OnNetworkSpawn видел правильные значения
            var carSO = _carRoster[Mathf.Clamp(loadout.CarIndex, 0, _carRoster.Length - 1)];
            ApplyCarData(instance, carSO);

            netObj.SpawnWithOwnership(clientId);

            // Инжектируем оружие и ковш выбранные игроком в лобби
            InjectWeaponData(instance, loadout.WeaponIndex);
            if (loadout.ScoopIndex >= 0)
                InjectScoopData(instance, loadout.ScoopIndex);

            Debug.Log($"[PlayerSpawner] Spawned '{prefab.name}' for client {clientId} at {spawnPos}");
        }

        // ── Car data injection ───────────────────────────────────────────────

        private void ApplyCarData(GameObject car, CarDataSO data)
        {
            if (data == null) return;

            // Rigidbody масса
            var rb = car.GetComponent<Rigidbody>();
            if (rb != null) rb.mass = data.MassKg;

            // CarPhysics — скорость, крутящий момент и т.д.
            var physics = car.GetComponent<CarPhysics>();
            if (physics != null) physics.Initialize(data);

            // HealthSystem — HP из SO
            var health = car.GetComponent<Health.HealthSystem>();
            if (health != null) health.Initialize(data.MaxHealth);
        }

        // ── Weapon injection ─────────────────────────────────────────────────

        private void InjectScoopData(GameObject carInstance, int scoopIndex)
        {
            if (_scoopRoster == null || _scoopRoster.Length == 0)
            {
                Debug.LogWarning("[PlayerSpawner] _scoopRoster пустой — назначь WeaponDataSO (ковши) в Inspector.");
                return;
            }

            int idx  = Mathf.Clamp(scoopIndex, 0, _scoopRoster.Length - 1);
            var data = _scoopRoster[idx];
            if (data == null || data.WeaponPrefab == null) { Debug.LogWarning($"[PlayerSpawner] ScoopRoster[{idx}] пустой или без префаба."); return; }

            SpawnScoopOnCar(carInstance, data);

            var netObj = carInstance.GetComponent<NetworkObject>();
            if (netObj != null) SpawnScoopClientRpc(netObj.NetworkObjectId, idx);
        }

        private void InjectWeaponData(GameObject carInstance, int weaponIndex)
        {
            Debug.Log($"[PlayerSpawner] InjectWeaponData: weaponIndex={weaponIndex}, rosterLen={_weaponRoster?.Length ?? -1}, car='{carInstance.name}'");

            if (_weaponRoster == null || _weaponRoster.Length == 0)
            {
                Debug.LogWarning("[PlayerSpawner] _weaponRoster пустой — назначь WeaponDataSO в Inspector.");
                return;
            }

            int idx  = Mathf.Clamp(weaponIndex, 0, _weaponRoster.Length - 1);
            var data = _weaponRoster[idx];
            if (data == null) { Debug.LogWarning($"[PlayerSpawner] WeaponDataSO[{idx}] = null"); return; }
            if (data.WeaponPrefab == null) { Debug.LogWarning($"[PlayerSpawner] '{data.name}'.WeaponPrefab не назначен в SO."); return; }

            var netObj = carInstance.GetComponent<NetworkObject>();

            if (data.IsFrontScoop)
            {
                SpawnScoopOnCar(carInstance, data);
                if (netObj != null) SpawnWeaponClientRpc(netObj.NetworkObjectId, idx);
            }
            else
            {
                Transform mount = FindWeaponMount(carInstance.transform);
                Debug.Log($"[PlayerSpawner] Weapon mount: '{mount.name}'");
                var weaponController = SpawnWeapon(mount, data);
                if (weaponController != null)
                    WireWeaponController(carInstance, weaponController);
                if (netObj != null) SpawnWeaponClientRpc(netObj.NetworkObjectId, idx);
            }
        }

        /// <summary>Server-side scoop attachment: mounts prefab, equips ScoopModifier, applies stat bonuses.</summary>
        private void SpawnScoopOnCar(GameObject carInstance, WeaponDataSO data)
        {
            Transform mount = FindScoopMount(carInstance.transform);
            Debug.Log($"[PlayerSpawner] Scoop mount: '{mount.name}'");

            var scoopObj = Instantiate(data.WeaponPrefab, mount);
            scoopObj.transform.localPosition = Vector3.zero;
            scoopObj.transform.localRotation = Quaternion.identity;

            var scoopMod = scoopObj.GetComponent<Car.ScoopModifier>();
            if (scoopMod == null) scoopMod = scoopObj.AddComponent<Car.ScoopModifier>();
            scoopMod.SetWeaponData(data);
            scoopMod.Equip();

            carInstance.GetComponent<Car.CarController>()?.SetScoop(scoopMod);

            // Speed penalty
            if (data.SpeedPenaltyPercent > 0f)
                carInstance.GetComponent<Car.CarPhysics>()?.SetWeaponSpeedPenalty(data.SpeedPenaltyPercent);

            // Bonus HP — applied after NetworkSpawn so NetworkVariable is writable
            if (data.BonusHealthPercent > 0f)
                carInstance.GetComponent<Health.HealthSystem>()?.IncreaseMaxHealth(data.BonusHealthPercent);

            Debug.Log($"[PlayerSpawner] Scoop '{data.WeaponPrefab.name}' equipped. SpeedPenalty={data.SpeedPenaltyPercent}%, BonusHP={data.BonusHealthPercent}%");
        }

        /// <summary>Finds the scoop mount via CarScoopSlot component; falls back to car root with a warning.</summary>
        private Transform FindScoopMount(Transform root)
        {
            var slot = root.GetComponentInChildren<CarScoopSlot>();
            if (slot != null) return slot.transform;
            Debug.LogWarning($"[PlayerSpawner] CarScoopSlot не найден на '{root.name}'. Добавь пустой дочерний объект спереди машины и повесь на него компонент CarScoopSlot.");
            return root;
        }

        /// <summary>Ищет точку крепления оружия: сначала по компоненту CarWeaponSlot, потом по имени "WeaponSlot".</summary>
        private Transform FindWeaponMount(Transform root)
        {
            var slot = root.GetComponentInChildren<CarWeaponSlot>();
            if (slot != null) return slot.transform;

            foreach (Transform child in root.GetComponentsInChildren<Transform>())
            {
                if (child.name == "WeaponSlot") return child;
            }

            Debug.LogWarning($"[PlayerSpawner] WeaponSlot не найден на '{root.name}' — оружие крепится к корню.");
            return root;
        }

        private WeaponController SpawnWeapon(Transform mount, WeaponDataSO data)
        {
            var weaponObj = Instantiate(data.WeaponPrefab, mount);
            weaponObj.transform.localPosition = Vector3.zero;
            weaponObj.transform.localRotation = Quaternion.identity;
            var controller = weaponObj.GetComponent<WeaponController>();
            if (controller != null) controller.SetWeaponData(data);
            Debug.Log($"[PlayerSpawner] SpawnWeapon: '{data.WeaponPrefab.name}' → '{mount.name}', controller={controller != null}");
            return controller;
        }

        /// <summary>Прокидывает WeaponController в PlayerNetwork и PlayerInputHandler на корне машины.</summary>
        private void WireWeaponController(GameObject carRoot, WeaponController weaponController)
        {
            var inputHandler = carRoot.GetComponent<Player.PlayerInputHandler>();
            if (inputHandler != null)
                inputHandler.SetWeaponController(weaponController);

            var playerNetwork = carRoot.GetComponent<Player.PlayerNetwork>();
            if (playerNetwork != null)
                playerNetwork.SetWeaponController(weaponController);
        }

        [ClientRpc]
        private void SpawnWeaponClientRpc(ulong carNetworkObjectId, int weaponIndex)
        {
            if (IsServer) return;

            if (_weaponRoster == null || weaponIndex >= _weaponRoster.Length) return;
            var data = _weaponRoster[weaponIndex];
            if (data == null || data.WeaponPrefab == null) return;

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
                    .TryGetValue(carNetworkObjectId, out var netObj)) return;

            Debug.Log($"[PlayerSpawner] ClientRpc: spawning '{data.WeaponPrefab.name}' on '{netObj.name}'");

            Transform mount = FindWeaponMount(netObj.transform);
            var weaponController = SpawnWeapon(mount, data);
            if (weaponController != null)
                WireWeaponController(netObj.gameObject, weaponController);
        }

        [ClientRpc]
        private void SpawnScoopClientRpc(ulong carNetworkObjectId, int scoopIndex)
        {
            if (IsServer) return;

            if (_scoopRoster == null || scoopIndex >= _scoopRoster.Length) return;
            var data = _scoopRoster[scoopIndex];
            if (data == null || data.WeaponPrefab == null) return;

            if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects
                    .TryGetValue(carNetworkObjectId, out var netObj)) return;

            Transform mount = FindScoopMount(netObj.transform);
            var scoopObj = Instantiate(data.WeaponPrefab, mount);
            scoopObj.transform.localPosition = Vector3.zero;
            scoopObj.transform.localRotation = Quaternion.identity;
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
