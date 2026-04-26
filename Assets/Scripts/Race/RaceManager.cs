using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class RaceManager : NetworkBehaviour
{
    public static RaceManager Instance { get; private set; }

    [SerializeField] private int _totalLaps = 3;
    [SerializeField] private float _countdownSeconds = 3f;

    public enum RaceState { WaitingForPlayers, Countdown, Racing, Finished }

    private readonly NetworkVariable<RaceState> _state = new(
        RaceState.WaitingForPlayers,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> _countdownValue = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Прогресс гонщиков (только на сервере)
    private readonly Dictionary<ulong, PlayerRaceProgress> _progress = new();

    public int TotalLaps => _totalLaps;
    public RaceState CurrentState => _state.Value;
    public int CountdownValue => _countdownValue.Value;

    public event System.Action<RaceState> OnStateChanged;
    public event System.Action<int> OnCountdownTick;
    public event System.Action<ulong> OnPlayerFinished;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        _state.OnValueChanged += (_, newState) => OnStateChanged?.Invoke(newState);
        _countdownValue.OnValueChanged += (_, v) => OnCountdownTick?.Invoke(v);

        if (IsServer)
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        _progress[clientId] = new PlayerRaceProgress { CurrentLap = 0, NextCheckpointIndex = 0 };
    }

    public void StartRace()
    {
        if (!IsServer || _state.Value != RaceState.WaitingForPlayers) return;
        StartCoroutine(CountdownCoroutine());
    }

    private IEnumerator CountdownCoroutine()
    {
        _state.Value = RaceState.Countdown;

        for (int i = (int)_countdownSeconds; i > 0; i--)
        {
            _countdownValue.Value = i;
            yield return new WaitForSeconds(1f);
        }

        _countdownValue.Value = 0;
        _state.Value = RaceState.Racing;
    }

    // Вызывается из Checkpoint
    public void ReportCheckpoint(ulong clientId, int checkpointIndex, int totalCheckpoints)
    {
        if (!IsServer || _state.Value != RaceState.Racing) return;

        if (!_progress.TryGetValue(clientId, out var prog)) return;

        if (checkpointIndex != prog.NextCheckpointIndex) return;

        prog.NextCheckpointIndex++;

        // Замкнули круг
        if (prog.NextCheckpointIndex >= totalCheckpoints)
        {
            prog.CurrentLap++;
            prog.NextCheckpointIndex = 0;

            NotifyLapClientRpc(clientId, prog.CurrentLap, new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
            });

            if (prog.CurrentLap >= _totalLaps)
            {
                _progress[clientId] = prog;
                _state.Value = RaceState.Finished;
                PlayerFinishedClientRpc(clientId);
                OnPlayerFinished?.Invoke(clientId);
                return;
            }
        }

        _progress[clientId] = prog;
    }

    [ClientRpc]
    private void NotifyLapClientRpc(ulong clientId, int lap, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[Race] Круг {lap}/{_totalLaps}!");
    }

    [ClientRpc]
    private void PlayerFinishedClientRpc(ulong winnerId)
    {
        bool isMe = NetworkManager.Singleton.LocalClientId == winnerId;
        Debug.Log(isMe ? "[Race] Ты победил!" : $"[Race] Победил игрок {winnerId}");
    }

    public int GetPlayerLap(ulong clientId) =>
        _progress.TryGetValue(clientId, out var p) ? p.CurrentLap : 0;

    private struct PlayerRaceProgress
    {
        public int CurrentLap;
        public int NextCheckpointIndex;
    }
}
