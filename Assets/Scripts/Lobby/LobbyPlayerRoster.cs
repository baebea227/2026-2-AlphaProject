using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;

/// <summary>
/// Fusion 세션에 참가한 플레이어를 참가 순서대로 관리하고 변경 이벤트를 제공합니다.
/// </summary>
public sealed class LobbyPlayerRoster : MonoBehaviour, INetworkRunnerCallbacks
{
    private static readonly ReliableKey ReadyRequestKey = ReliableKey.FromInts(0x4C4F4242, 1, 0, 0);
    private static readonly ReliableKey ReadyStateKey = ReliableKey.FromInts(0x4C4F4242, 2, 0, 0);

    private readonly List<PlayerRef> players = new List<PlayerRef>();
    private readonly Dictionary<PlayerRef, bool> readyStates = new Dictionary<PlayerRef, bool>();
    private NetworkRunner runner;

    public IReadOnlyList<PlayerRef> Players => players;

    public event Action PlayerStatesChanged;

    /// <summary>
    /// 지정한 플레이어의 현재 Ready 상태를 반환합니다.
    /// </summary>
    public bool IsReady(PlayerRef player)
    {
        return readyStates.TryGetValue(player, out bool isReady) && isReady;
    }

    /// <summary>
    /// 로컬 플레이어의 Ready 상태를 반전해 서버에 적용을 요청합니다.
    /// </summary>
    public void ToggleLocalReady()
    {
        if (runner == null || !runner.IsRunning || !runner.LocalPlayer.IsRealPlayer)
        {
            return;
        }

        PlayerRef localPlayer = runner.LocalPlayer;
        bool nextReadyState = !IsReady(localPlayer);

        if (runner.IsServer)
        {
            // Host는 서버 권한으로 즉시 상태를 확정하고 모든 클라이언트에 전파합니다.
            SetReadyState(localPlayer, nextReadyState);
            BroadcastReadyState(localPlayer, nextReadyState);
            return;
        }

        // Client는 서버가 상태를 확정하도록 한 바이트 크기의 변경 요청을 보냅니다.
        runner.SendReliableDataToServer(ReadyRequestKey, new[] { nextReadyState ? (byte)1 : (byte)0 });
    }

    /// <summary>
    /// 세션 시작 전에 Runner 콜백을 등록하여 최초 참가 이벤트부터 놓치지 않게 합니다.
    /// </summary>
    public void Initialize(NetworkRunner networkRunner)
    {
        if (runner == networkRunner)
        {
            return;
        }

        if (runner != null)
        {
            runner.RemoveCallbacks(this);
        }

        runner = networkRunner;
        players.Clear();
        readyStates.Clear();

        if (runner != null)
        {
            runner.AddCallbacks(this);
        }
    }

    private void OnDestroy()
    {
        // Runner보다 로스터가 먼저 제거되는 경우 남은 콜백 참조를 정리합니다.
        if (runner != null)
        {
            runner.RemoveCallbacks(this);
        }
    }

    public void OnPlayerJoined(NetworkRunner networkRunner, PlayerRef player)
    {
        if (players.Contains(player))
        {
            return;
        }

        players.Add(player);

        // 참가 콜백보다 Ready 패킷이 먼저 도착했다면 수신한 상태를 보존합니다.
        if (!readyStates.ContainsKey(player))
        {
            readyStates[player] = false;
        }

        // PlayerRef 번호를 기준으로 정렬해 모든 클라이언트가 같은 순서로 표시합니다.
        players.Sort((left, right) => left.RawEncoded.CompareTo(right.RawEncoded));
        PlayerStatesChanged?.Invoke();

        if (networkRunner.IsServer)
        {
            // 새 참가자에게 기존 상태 전체를 전달하고, 새 참가자의 초기 상태도 모두에게 알립니다.
            SendAllReadyStatesToPlayer(player);
            BroadcastReadyState(player, false);
        }
    }

    public void OnPlayerLeft(NetworkRunner networkRunner, PlayerRef player)
    {
        if (players.Remove(player))
        {
            readyStates.Remove(player);
            PlayerStatesChanged?.Invoke();
        }
    }

    public void OnShutdown(NetworkRunner networkRunner, ShutdownReason shutdownReason)
    {
        // 세션이 종료되면 남아 있는 플레이어 표시를 모두 비웁니다.
        players.Clear();
        readyStates.Clear();
        PlayerStatesChanged?.Invoke();
    }

    public void OnReliableDataReceived(
        NetworkRunner networkRunner,
        PlayerRef player,
        ReliableKey key,
        ArraySegment<byte> data)
    {
        if (key == ReadyRequestKey && networkRunner.IsServer)
        {
            // 서버만 클라이언트의 Ready 변경 요청을 승인하고 확정 상태를 전파합니다.
            if (data.Array == null || data.Count < 1 || !players.Contains(player))
            {
                return;
            }

            bool isReady = data.Array[data.Offset] != 0;
            SetReadyState(player, isReady);
            BroadcastReadyState(player, isReady);
            return;
        }

        if (key != ReadyStateKey || networkRunner.IsServer || data.Array == null || data.Count < 5)
        {
            return;
        }

        // 클라이언트는 서버가 보낸 PlayerRef와 Ready 값을 로컬 표시 상태에 반영합니다.
        int playerRawEncoded = BitConverter.ToInt32(data.Array, data.Offset);
        PlayerRef readyPlayer = PlayerRef.FromEncoded(playerRawEncoded);
        bool readyState = data.Array[data.Offset + sizeof(int)] != 0;
        SetReadyState(readyPlayer, readyState);
    }

    private void SetReadyState(PlayerRef player, bool isReady)
    {
        if (!players.Contains(player))
        {
            // 참가 콜백 전 수신된 상태는 보관했다가 플레이어가 등록될 때 사용합니다.
            readyStates[player] = isReady;
            return;
        }

        if (readyStates.TryGetValue(player, out bool currentState) && currentState == isReady)
        {
            return;
        }

        readyStates[player] = isReady;
        PlayerStatesChanged?.Invoke();
    }

    private void SendAllReadyStatesToPlayer(PlayerRef targetPlayer)
    {
        foreach (PlayerRef player in players)
        {
            SendReadyStateToPlayer(targetPlayer, player, IsReady(player));
        }
    }

    private void BroadcastReadyState(PlayerRef player, bool isReady)
    {
        foreach (PlayerRef targetPlayer in players)
        {
            // Host 자신의 UI는 SetReadyState에서 이미 갱신되므로 네트워크 전송에서 제외합니다.
            if (targetPlayer == runner.LocalPlayer)
            {
                continue;
            }

            SendReadyStateToPlayer(targetPlayer, player, isReady);
        }
    }

    private void SendReadyStateToPlayer(PlayerRef targetPlayer, PlayerRef player, bool isReady)
    {
        byte[] data = new byte[sizeof(int) + sizeof(byte)];
        byte[] encodedPlayer = BitConverter.GetBytes(player.RawEncoded);
        Buffer.BlockCopy(encodedPlayer, 0, data, 0, sizeof(int));
        data[sizeof(int)] = isReady ? (byte)1 : (byte)0;
        runner.SendReliableDataToPlayer(targetPlayer, ReadyStateKey, data);
    }

    public void OnObjectExitAOI(NetworkRunner networkRunner, NetworkObject networkObject, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner networkRunner, NetworkObject networkObject, PlayerRef player) { }
    public void OnInput(NetworkRunner networkRunner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner networkRunner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner networkRunner) { }
    public void OnDisconnectedFromServer(NetworkRunner networkRunner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner networkRunner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner networkRunner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner networkRunner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner networkRunner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner networkRunner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner networkRunner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataProgress(NetworkRunner networkRunner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner networkRunner) { }
    public void OnSceneLoadStart(NetworkRunner networkRunner) { }
}
