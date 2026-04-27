// Assets/Scripts/GameModes/Deathmatch.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace CarDerby.GameModes
{
    /// <summary>
    /// Timed free-for-all with respawns.
    /// Kill tracking lives on each PlayerNetwork.KillCount (NetworkVariable).
    /// Server drives the match timer and announces end game.
    /// </summary>
    public class Deathmatch : NetworkBehaviour, IGameMode
    {
        [SerializeField] private float _matchDuration     = 300f; // 5 minutes
        [SerializeField] private int   _killsToWin        = 15;
        [SerializeField] private float _respawnDelay      = 3f;
        [SerializeField] private Transform[] _spawnPoints;

        // ── Networked state ──────────────────────────────────────────────────

        private readonly NetworkVariable<float> _timeRemaining = new(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<bool> _isMatchActive = new(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        // killerId → PlayerNetwork (server-side only lookup)
        private readonly Dictionary<ulong, Player.PlayerNetwork> _players = new();

        public bool  IsMatchActive   => _isMatchActive.Value;
        public float TimeRemaining   => _timeRemaining.Value;

        // ── Lifecycle ────────────────────────────────────────────────────────

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;
            Player.PlayerNetwork.AnyPlayerDied += OnPlayerDied;
            NetworkManager.OnClientConnectedCallback    += OnPlayerConnected;
            NetworkManager.OnClientDisconnectCallback   += OnPlayerDisconnected;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer) return;
            Player.PlayerNetwork.AnyPlayerDied -= OnPlayerDied;
            NetworkManager.OnClientConnectedCallback    -= OnPlayerConnected;
            NetworkManager.OnClientDisconnectCallback   -= OnPlayerDisconnected;
        }

        // ── IGameMode ────────────────────────────────────────────────────────

        public void StartMatch()
        {
            if (!IsServer) return;
            _players.Clear();

            // Index currently connected players
            foreach (var kv in NetworkManager.ConnectedClients)
            {
                var pn = kv.Value.PlayerObject?.GetComponent<Player.PlayerNetwork>();
                if (pn != null) _players[kv.Key] = pn;
            }

            _timeRemaining.Value = _matchDuration;
            _isMatchActive.Value = true;

            StartCoroutine(TickTimer());
            AnnounceClientRpc("Deathmatch — FIGHT!");
        }

        public void EndMatch()
        {
            if (!IsServer) return;
            _isMatchActive.Value = false;
            StopAllCoroutines();

            ulong winnerId = GetLeadingPlayer();
            DeclareWinnerClientRpc(winnerId, GetKillCount(winnerId));
        }

        public void OnPlayerDied(ulong victimId, ulong killerId)
        {
            if (!IsServer || !_isMatchActive.Value) return;

            // Credit kill
            if (killerId != ulong.MaxValue && _players.TryGetValue(killerId, out var killer))
            {
                killer.AddKill();
                if (GetKillCount(killerId) >= _killsToWin)
                {
                    EndMatch();
                    return;
                }
            }

            // Schedule respawn
            if (_players.TryGetValue(victimId, out var victim))
                StartCoroutine(RespawnAfterDelay(victim));
        }

        public void OnPlayerConnected(ulong clientId)
        {
            if (!IsServer) return;
            var pn = NetworkManager.ConnectedClients[clientId].PlayerObject?.GetComponent<Player.PlayerNetwork>();
            if (pn != null) _players[clientId] = pn;
        }

        public void OnPlayerDisconnected(ulong clientId)
        {
            if (!IsServer) return;
            _players.Remove(clientId);
        }

        // ── Internals ────────────────────────────────────────────────────────

        private IEnumerator TickTimer()
        {
            while (_timeRemaining.Value > 0f && _isMatchActive.Value)
            {
                yield return new WaitForSeconds(1f);
                _timeRemaining.Value -= 1f;
            }

            if (_isMatchActive.Value)
                EndMatch();
        }

        private IEnumerator RespawnAfterDelay(Player.PlayerNetwork player)
        {
            yield return new WaitForSeconds(_respawnDelay);
            if (!_isMatchActive.Value) yield break;

            Vector3 spawnPos = PickSpawnPoint();
            player.Respawn(spawnPos);
        }

        private Vector3 PickSpawnPoint()
        {
            if (_spawnPoints == null || _spawnPoints.Length == 0) return Vector3.zero;
            return _spawnPoints[Random.Range(0, _spawnPoints.Length)].position;
        }

        private ulong GetLeadingPlayer()
        {
            ulong leader = ulong.MaxValue;
            int   top    = -1;

            foreach (var kv in _players)
            {
                int k = kv.Value.KillCount.Value;
                if (k > top) { top = k; leader = kv.Key; }
            }

            return leader;
        }

        private int GetKillCount(ulong clientId) =>
            _players.TryGetValue(clientId, out var pn) ? pn.KillCount.Value : 0;

        // ── RPCs ─────────────────────────────────────────────────────────────

        [ClientRpc]
        private void AnnounceClientRpc(string message) =>
            Debug.Log($"[DM] {message}");

        [ClientRpc]
        private void DeclareWinnerClientRpc(ulong winnerId, int kills)
        {
            string msg = winnerId == ulong.MaxValue
                ? "Match over — it's a draw!"
                : $"Player {winnerId} wins with {kills} kills!";
            Debug.Log($"[DM] {msg}");
            // Hook your WinScreen UI here
        }
    }
}
