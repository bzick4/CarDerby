// Assets/Scripts/Player/PlayerNetwork.cs
using System;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;

namespace CarDerby.Player
{
    /// <summary>
    /// Root NetworkBehaviour for each player.
    ///
    /// Spawn flow:
    ///   1. Server calls NetworkObject.Spawn() for the player prefab.
    ///   2. OnNetworkSpawn fires on server + all clients.
    ///   3. Owner enables input + camera; others enable world-space HP bar.
    ///   4. Server subscribes to HealthSystem.OnDeath to notify the active game mode.
    ///
    /// Position sync is handled by a NetworkTransform component on the same prefab.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class PlayerNetwork : NetworkBehaviour
    {
        [SerializeField] private Car.CarController          _carController;
        [SerializeField] private Combat.WeaponController    _weaponController;
        [SerializeField] private Health.HealthSystem        _healthSystem;
        [SerializeField] private PlayerInputHandler         _inputHandler;
        [SerializeField] private UI.WorldSpaceHealthBar     _worldSpaceHealthBar;

        // ── Networked state (readable by all, written by server / owner) ─────

        public NetworkVariable<int>              KillCount  { get; } = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool>             IsAlive    { get; } = new(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<FixedString64Bytes> PlayerName { get; } = new("Player", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Static relay so game-mode classes can subscribe without holding player references
        public static event Action<ulong, ulong> AnyPlayerDied; // (victimId, killerId)

        public ulong PlayerId => OwnerClientId;

        // ── Lifecycle ────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            name = $"Player_{OwnerClientId}";

            if (IsOwner)
            {
                _inputHandler.enabled = true;
                // Говорим единственной сцен-камере следить за этой машиной
                SceneCamera.Instance?.Follow(transform);
                _worldSpaceHealthBar.gameObject.SetActive(false);
            }
            else
            {
                _inputHandler.enabled = false;
                _worldSpaceHealthBar.Initialize(_healthSystem);
            }

            if (IsServer)
                _healthSystem.OnDeath += HandleDeath;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                _healthSystem.OnDeath -= HandleDeath;
        }

        // ── Server-only methods ──────────────────────────────────────────────

        private void HandleDeath(ulong killerId)
        {
            IsAlive.Value = false;
            AnyPlayerDied?.Invoke(OwnerClientId, killerId);
            ShowDeathScreenClientRpc();
        }

        public void AddKill()
        {
            if (!IsServer) return;
            KillCount.Value++;
        }

        /// <summary>Called by Deathmatch to respawn the player at a new position.</summary>
        public void Respawn(Vector3 spawnPosition)
        {
            if (!IsServer) return;
            IsAlive.Value = true;
            _healthSystem.Heal(_healthSystem.MaxHealth); // restore full HP
            RespawnClientRpc(spawnPosition);
        }

        [ClientRpc]
        private void RespawnClientRpc(Vector3 position)
        {
            transform.position = position;
        }

        [ClientRpc]
        private void ShowDeathScreenClientRpc()
        {
            if (!IsOwner) return;
            // UI.MainMenuUI or a dedicated DeathScreen can subscribe to this event instead
            // to keep UI concerns out of PlayerNetwork.
        }
    }
}
