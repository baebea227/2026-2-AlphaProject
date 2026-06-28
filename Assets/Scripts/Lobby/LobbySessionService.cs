using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;

/// <summary>
/// Fusion 세션 시작 결과와 UI에 전달할 룸 코드 또는 오류 메시지를 보관합니다.
/// </summary>
public readonly struct LobbySessionStartResult
{
    public bool IsSuccess { get; }
    public string RoomCode { get; }
    public string ErrorMessage { get; }

    private LobbySessionStartResult(bool isSuccess, string roomCode, string errorMessage)
    {
        IsSuccess = isSuccess;
        RoomCode = roomCode;
        ErrorMessage = errorMessage;
    }

    public static LobbySessionStartResult Success(string roomCode)
    {
        return new LobbySessionStartResult(true, roomCode, string.Empty);
    }

    public static LobbySessionStartResult Failure(string errorMessage)
    {
        return new LobbySessionStartResult(false, string.Empty, errorMessage);
    }
}

/// <summary>
/// Fusion의 NetworkRunner를 생성하고 룸 코드 기반 세션 생성과 참가를 처리합니다.
/// 세션 연결은 씬이 바뀐 뒤에도 유지되어야 하므로 이 객체는 파괴되지 않습니다.
/// </summary>
public sealed class LobbySessionService : MonoBehaviour
{
    public static LobbySessionService Instance { get; private set; }

    public bool IsBusy { get; private set; }
    public string CurrentRoomCode { get; private set; } = string.Empty;
    public NetworkRunner Runner { get; private set; }

    private void Awake()
    {
        // 씬 재진입으로 서비스가 중복 생성되면 기존 인스턴스를 유지합니다.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public Task<LobbySessionStartResult> CreateSessionAsync()
    {
        // 생성자는 무작위 룸 코드를 Fusion의 세션 이름으로 사용합니다.
        string roomCode = RoomCodeUtility.Generate();
        return StartSessionAsync(GameMode.Host, roomCode);
    }

    public Task<LobbySessionStartResult> JoinSessionAsync(string roomCode)
    {
        // 참가자는 입력값을 정리한 뒤 동일한 이름의 기존 세션에 접속합니다.
        string normalizedRoomCode = RoomCodeUtility.Normalize(roomCode);
        if (!RoomCodeUtility.IsValid(normalizedRoomCode))
        {
            return Task.FromResult(LobbySessionStartResult.Failure("룸 코드는 숫자 6자리여야 합니다."));
        }

        return StartSessionAsync(GameMode.Client, normalizedRoomCode);
    }

    private async Task<LobbySessionStartResult> StartSessionAsync(GameMode gameMode, string roomCode)
    {
        // 중복 요청이나 이미 연결된 상태에서 새로운 Runner가 생기는 것을 막습니다.
        if (IsBusy)
        {
            return LobbySessionStartResult.Failure("세션 연결을 처리하고 있습니다.");
        }

        if (Runner != null)
        {
            return LobbySessionStartResult.Failure("이미 세션에 연결되어 있습니다.");
        }

        IsBusy = true;

        // NetworkRunner를 별도 자식 객체로 두어 서비스와 네트워크 실행 책임을 구분합니다.
        GameObject runnerObject = new GameObject("LobbyNetworkRunner");
        runnerObject.transform.SetParent(transform);

        NetworkRunner newRunner = runnerObject.AddComponent<NetworkRunner>();
        newRunner.ProvideInput = true;
        NetworkSceneManagerDefault sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();

        try
        {
            // Host는 세션을 생성하고 Client는 같은 SessionName의 세션에만 참가합니다.
            // 로비 씬은 그대로 유지하고, SceneManager는 이후 네트워크 씬 전환에 재사용합니다.
            StartGameResult startResult = await newRunner.StartGame(new StartGameArgs
            {
                GameMode = gameMode,
                SessionName = roomCode,
                SceneManager = sceneManager,
                IsOpen = true,
                IsVisible = false,
                EnableClientSessionCreation = false
            });

            if (!startResult.Ok)
            {
                Destroy(runnerObject);
                return LobbySessionStartResult.Failure(GetFailureMessage(gameMode, startResult));
            }

            Runner = newRunner;
            CurrentRoomCode = roomCode;
            return LobbySessionStartResult.Success(roomCode);
        }
        catch (Exception exception)
        {
            Destroy(runnerObject);
            Debug.LogException(exception, this);
            return LobbySessionStartResult.Failure("세션 연결 중 오류가 발생했습니다.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string GetFailureMessage(GameMode gameMode, StartGameResult result)
    {
        if (gameMode == GameMode.Client)
        {
            return $"룸 코드에 해당하는 세션에 참가하지 못했습니다. ({result.ShutdownReason})";
        }

        return $"세션을 생성하지 못했습니다. ({result.ShutdownReason})";
    }
}
