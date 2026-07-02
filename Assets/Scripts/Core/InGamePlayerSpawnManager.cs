using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.Serialization;

public enum PlayerSpawnMode
{
    LocalInstantiate,
    FusionNetwork
}

public sealed class InGamePlayerSpawnManager : MonoBehaviour, INetworkRunnerCallbacks
{
    private static readonly Vector3[] SinglePointSpawnOffsets =
    {
        new Vector3(-1f, 0f, 1f),
        new Vector3(1f, 0f, 1f),
        new Vector3(-1f, 0f, -1f),
        new Vector3(1f, 0f, -1f)
    };

    [Header("Spawn Mode")]
    [Tooltip("플레이어를 생성할 방식을 선택합니다.")]
    [FormerlySerializedAs("spawnBackend")]
    [SerializeField] private PlayerSpawnMode spawnMode = PlayerSpawnMode.FusionNetwork;
    [Tooltip("인게임 매니저가 시작될 때 자동으로 플레이어를 생성합니다.")]
    [SerializeField] private bool spawnOnStart = true;

    [Header("Local Spawn")]
    [Tooltip("레거시 로컬 테스트용으로 생성할 플레이어 프리팹입니다.")]
    [SerializeField] private GameObject localPlayerPrefab;
    [Tooltip("레거시 로컬 테스트용으로 생성할 플레이어 수입니다.")]
    [Range(1, 4)]
    [SerializeField] private int localPlayerCount = 1;

    [Header("Fusion Spawn")]
    [Tooltip("네트워크 플레이어를 생성할 NetworkRunner입니다. 비어 있으면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private NetworkRunner networkRunner;
    [Tooltip("Fusion으로 생성할 네트워크 플레이어 프리팹입니다.")]
    [SerializeField] private NetworkObject networkPlayerPrefab;

    [Header("Spawn Points")]
    [Tooltip("플레이어들이 생성될 기준 위치입니다. 비어 있으면 이 매니저 위치를 사용합니다.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("기준 위치를 중심으로 플레이어를 사각형으로 벌려 배치할 거리입니다.")]
    [Min(0f)]
    [SerializeField] private float singleSpawnPointOffsetDistance = 1.5f;
    [Tooltip("레거시 로컬 테스트용으로 생성된 플레이어를 정리해서 담을 부모 Transform입니다.")]
    [SerializeField] private Transform spawnedPlayersParent;

    private readonly List<GameObject> spawnedLocalPlayers = new List<GameObject>();
    private readonly Dictionary<PlayerRef, NetworkObject> spawnedNetworkPlayers = new Dictionary<PlayerRef, NetworkObject>();

    private InGameBootstrap runnerBootstrap;
    private NetworkRunner registeredRunner;
    private bool didWarnLegacyLocalSpawn;

    public IReadOnlyList<GameObject> SpawnedLocalPlayers
    {
        get { return spawnedLocalPlayers; }
    }

    private void OnEnable()
    {
        if (spawnMode != PlayerSpawnMode.FusionNetwork)
        {
            WarnLegacyLocalSpawnModeOnce();
            return;
        }

        // 테스트 씬의 부트스트랩이 있으면 Runner 준비 완료 이벤트를 우선 기다립니다.
        runnerBootstrap = FindFirstObjectByType<InGameBootstrap>();
        if (runnerBootstrap != null)
        {
            runnerBootstrap.RunnerReady -= HandleRunnerReady;
            runnerBootstrap.RunnerReady += HandleRunnerReady;

            if (runnerBootstrap.IsRunnerReady)
            {
                HandleRunnerReady(runnerBootstrap.Runner);
            }

            return;
        }

        // 로비를 거쳐 InGame에 들어온 경우 이미 실행 중인 Runner를 바로 사용합니다.
        TryRegisterNetworkCallbacks();
    }

    private void Start()
    {
        if (spawnMode == PlayerSpawnMode.FusionNetwork && runnerBootstrap == null)
        {
            TryRegisterNetworkCallbacks();
        }

        if (spawnMode == PlayerSpawnMode.LocalInstantiate && spawnOnStart)
        {
            WarnLegacyLocalSpawnModeOnce();
            SpawnLocalPlayers();
        }
        else if (spawnMode == PlayerSpawnMode.FusionNetwork && spawnOnStart)
        {
            TrySpawnActiveNetworkPlayers(registeredRunner, true);
        }
    }

    private void OnDisable()
    {
        if (runnerBootstrap != null)
        {
            runnerBootstrap.RunnerReady -= HandleRunnerReady;
            runnerBootstrap = null;
        }

        UnregisterNetworkCallbacks();
    }

    private void OnValidate()
    {
        localPlayerCount = Mathf.Clamp(localPlayerCount, 1, 4);
        singleSpawnPointOffsetDistance = Mathf.Max(0f, singleSpawnPointOffsetDistance);
    }

    private void OnDrawGizmosSelected()
    {
        Transform baseSpawnPoint = GetSpawnPoint(0);
        Vector3 basePosition = baseSpawnPoint.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(basePosition, 0.2f);

        Gizmos.color = Color.cyan;
        for (int i = 0; i < SinglePointSpawnOffsets.Length; i++)
        {
            Vector3 spawnPosition = GetSpawnPosition(i);
            Gizmos.DrawLine(basePosition, spawnPosition);
            Gizmos.DrawWireSphere(spawnPosition, 0.35f);
        }
    }

    public GameObject SpawnLocalPlayer(int playerIndex)
    {
        if (playerIndex < 0)
        {
            Debug.LogError($"{nameof(InGamePlayerSpawnManager)}: 플레이어 인덱스는 0보다 작을 수 없습니다.", this);
            return null;
        }

        if (localPlayerPrefab == null)
        {
            Debug.LogError($"{nameof(InGamePlayerSpawnManager)}: 로컬 플레이어 프리팹이 연결되지 않았습니다.", this);
            return null;
        }

        EnsureLocalPlayerSlot(playerIndex);

        GameObject existingPlayer = spawnedLocalPlayers[playerIndex];
        if (existingPlayer != null)
        {
            return existingPlayer;
        }

        GameObject spawnedPlayer = Instantiate(
            localPlayerPrefab,
            GetSpawnPosition(playerIndex),
            GetSpawnRotation(playerIndex),
            spawnedPlayersParent);

        spawnedLocalPlayers[playerIndex] = spawnedPlayer;
        return spawnedPlayer;
    }

    public void SpawnLocalPlayers()
    {
        WarnLegacyLocalSpawnModeOnce();

        for (int i = 0; i < localPlayerCount; i++)
        {
            SpawnLocalPlayer(i);
        }
    }

    public NetworkObject SpawnNetworkPlayer(NetworkRunner runner, PlayerRef player)
    {
        if (runner == null)
        {
            Debug.LogError($"{nameof(InGamePlayerSpawnManager)}: NetworkRunner가 연결되지 않았습니다.", this);
            return null;
        }

        if (networkPlayerPrefab == null)
        {
            Debug.LogError($"{nameof(InGamePlayerSpawnManager)}: 네트워크 플레이어 프리팹이 연결되지 않았습니다.", this);
            return null;
        }

        if (spawnedNetworkPlayers.TryGetValue(player, out NetworkObject existingPlayer) && existingPlayer != null)
        {
            if (runner.GetPlayerObject(player) != existingPlayer)
            {
                runner.SetPlayerObject(player, existingPlayer);
            }

            return existingPlayer;
        }

        int playerIndex = GetNetworkPlayerIndex(player);
        NetworkObject spawnedPlayer = runner.Spawn(networkPlayerPrefab, GetSpawnPosition(playerIndex), GetSpawnRotation(playerIndex), player);

        spawnedNetworkPlayers[player] = spawnedPlayer;
        runner.SetPlayerObject(player, spawnedPlayer);

        return spawnedPlayer;
    }

    public Transform GetSpawnPoint(int playerIndex)
    {
        return spawnPoint != null ? spawnPoint : transform;
    }

    public void DespawnLocalPlayers()
    {
        for (int i = 0; i < spawnedLocalPlayers.Count; i++)
        {
            GameObject spawnedPlayer = spawnedLocalPlayers[i];
            if (spawnedPlayer != null)
            {
                Destroy(spawnedPlayer);
            }
        }

        spawnedLocalPlayers.Clear();
    }

    private void TryRegisterNetworkCallbacks()
    {
        if (spawnMode != PlayerSpawnMode.FusionNetwork)
        {
            return;
        }

        if (networkRunner == null)
        {
            networkRunner = FindFirstObjectByType<NetworkRunner>();
        }

        if (networkRunner == null || !networkRunner.IsRunning)
        {
            return;
        }

        if (registeredRunner == networkRunner)
        {
            return;
        }

        UnregisterNetworkCallbacks();

        networkRunner.RemoveCallbacks(this);
        networkRunner.AddCallbacks(this);
        registeredRunner = networkRunner;

        if (spawnOnStart)
        {
            TrySpawnActiveNetworkPlayers(registeredRunner, true);
        }
    }

    private void HandleRunnerReady(NetworkRunner readyRunner)
    {
        // StartGame 성공이 확인된 Runner만 등록해 초기화 도중의 Runner 사용을 막습니다.
        networkRunner = readyRunner;
        TryRegisterNetworkCallbacks();
    }

    private void UnregisterNetworkCallbacks()
    {
        if (registeredRunner == null)
        {
            return;
        }

        registeredRunner.RemoveCallbacks(this);
        registeredRunner = null;
    }

    private void EnsureLocalPlayerSlot(int playerIndex)
    {
        while (spawnedLocalPlayers.Count <= playerIndex)
        {
            spawnedLocalPlayers.Add(null);
        }
    }

    private void WarnLegacyLocalSpawnModeOnce()
    {
        if (didWarnLegacyLocalSpawn)
        {
            return;
        }

        didWarnLegacyLocalSpawn = true;
        Debug.LogWarning($"{nameof(InGamePlayerSpawnManager)}: LocalInstantiate는 레거시 로컬 테스트용 경로입니다. 공식 플레이 흐름은 FusionNetwork를 사용합니다.", this);
    }

    private int GetNetworkPlayerIndex(PlayerRef player)
    {
        return Mathf.Max(0, player.PlayerId);
    }

    private void TrySpawnActiveNetworkPlayers(NetworkRunner runner, bool requireSceneReady)
    {
        if (spawnMode != PlayerSpawnMode.FusionNetwork || !CanSpawnNetworkPlayers(runner))
        {
            return;
        }

        if (requireSceneReady && runner.IsSceneManagerBusy)
        {
            return;
        }

        foreach (PlayerRef player in runner.ActivePlayers)
        {
            SpawnNetworkPlayer(runner, player);
        }
    }

    private Vector3 GetSpawnPosition(int playerIndex)
    {
        Transform spawnPoint = GetSpawnPoint(playerIndex);
        Vector3 offset = SinglePointSpawnOffsets[GetWrappedIndex(playerIndex, SinglePointSpawnOffsets.Length)];
        return spawnPoint.position + offset * singleSpawnPointOffsetDistance;
    }

    private Quaternion GetSpawnRotation(int playerIndex)
    {
        return GetSpawnPoint(playerIndex).rotation;
    }

    private static int GetWrappedIndex(int index, int length)
    {
        if (length <= 0)
        {
            return 0;
        }

        int wrappedIndex = index % length;
        return wrappedIndex < 0 ? wrappedIndex + length : wrappedIndex;
    }

    private static bool CanSpawnNetworkPlayers(NetworkRunner runner)
    {
        return runner != null && runner.IsRunning && (runner.IsServer || runner.IsSharedModeMasterClient);
    }

    void INetworkRunnerCallbacks.OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (spawnMode != PlayerSpawnMode.FusionNetwork || !CanSpawnNetworkPlayers(runner))
        {
            return;
        }

        SpawnNetworkPlayer(runner, player);
    }

    void INetworkRunnerCallbacks.OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        spawnedNetworkPlayers.Remove(player);
    }

    void INetworkRunnerCallbacks.OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    void INetworkRunnerCallbacks.OnInput(NetworkRunner runner, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    void INetworkRunnerCallbacks.OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
    void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    void INetworkRunnerCallbacks.OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    void INetworkRunnerCallbacks.OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    void INetworkRunnerCallbacks.OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    void INetworkRunnerCallbacks.OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    void INetworkRunnerCallbacks.OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    void INetworkRunnerCallbacks.OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    void INetworkRunnerCallbacks.OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    void INetworkRunnerCallbacks.OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    void INetworkRunnerCallbacks.OnSceneLoadDone(NetworkRunner runner)
    {
        if (!spawnOnStart)
        {
            return;
        }

        TrySpawnActiveNetworkPlayers(runner, false);
    }
    void INetworkRunnerCallbacks.OnSceneLoadStart(NetworkRunner runner) { }
}
