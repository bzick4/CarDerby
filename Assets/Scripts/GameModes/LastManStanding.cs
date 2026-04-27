// Assets/Scripts/GameModes/LastManStanding.cs
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace CarDerby.GameModes
{
    /// <summary>
    /// One life per player. Last car driving wins.
    /// Server-only logic; result broadcast via ClientRpc.
    /// </summary>
    public class LastManStanding : NetworkBehaviour, IGameMode
    {
        private readonly NetworkVariable<bool> _isMatchActive = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly HashSet<ulong> _alivePlayers = new();

        public bool IsMatchActive => _isMatchActive.Value;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
                Player.PlayerNetwork.AnyPlayerDied += OnPlayerDied;
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
                Player.PlayerNetwork.AnyPlayerDied -= OnPlayerDied;
        }

        // ── IGameMode ────────────────────────────────────────────────────────

        public void StartMatch()
        {
            if (!IsServer) return;
            _alivePlayers.Clear();

            foreach (var client in NetworkManager.ConnectedClients)
                _alivePlayers.Add(client.Key);

            _isMatchActive.Value = true;
            AnnounceClientRpc("Last Man Standing — FIGHT!");
        }

        public void EndMatch()
        {
            if (!IsServer) return;
            _isMatchActive.Value = false;
        }

        public void OnPlayerDied(ulong playerId, ulong killerId)
        {
            if (!IsServer || !_isMatchActive.Value) return;

            _alivePlayers.Remove(playerId);
            EliminatedClientRpc(playerId);

            if (_alivePlayers.Count == 1)
            {
                ulong winnerId = System.Linq.Enumerable.First(_alivePlayers);
                EndMatch();
                DeclareWinnerClientRpc(winnerId);
            }
            else if (_alivePlayers.Count == 0)
            {
                EndMatch();
                DeclareWinnerClientRpc(ulong.MaxValue); // draw
            }
        }

        public void OnPlayerConnected(ulong playerId)
        {
            // LMS doesn't allow mid-match joins; handled by LobbyManager
        }

        public void OnPlayerDisconnected(ulong playerId)
        {
            if (!IsServer) return;
            OnPlayerDied(playerId, ulong.MaxValue);
        }

        // ── RPCs ─────────────────────────────────────────────────────────────

        [ClientRpc]
        private void AnnounceClientRpc(string message) =>
            Debug.Log($"[LMS] {message}");

        [ClientRpc]
        private void EliminatedClientRpc(ulong playerId) =>
            Debug.Log($"[LMS] Player {playerId} eliminated!");

        [ClientRpc]
        private void DeclareWinnerClientRpc(ulong winnerId)
        {
            string msg = winnerId == ulong.MaxValue
                ? "Draw — everyone perished!"
                : $"Player {winnerId} wins Last Man Standing!";
            Debug.Log($"[LMS] {msg}");
            // Hook your WinScreen UI here
        }
    }
}
