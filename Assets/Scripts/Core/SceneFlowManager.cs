using System;
using System.Collections;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SceneId
{
    Lobby,
    MainStage
}

public sealed class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager Instance { get; private set; }

    public bool IsLoading { get; private set; }

    public event Action<SceneId> SceneLoadStarted;
    public event Action<SceneId> SceneLoadCompleted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadLobby()
    {
        LoadScene(SceneId.Lobby);
    }

    public void LoadMainStage()
    {
        LoadScene(SceneId.MainStage);
    }

    public bool LoadNetworkMainStage(NetworkRunner runner)
    {
        // Fusion의 씬 권한을 가진 Host가 MainStage를 로드하면 같은 세션의 참가자에게 전환이 동기화됩니다.
        return LoadNetworkScene(runner, SceneId.MainStage);
    }

    public void LoadScene(SceneId sceneId)
    {
        if (IsLoading)
        {
            return;
        }

        StartCoroutine(LoadSceneRoutine(sceneId));
    }

    private bool LoadNetworkScene(NetworkRunner runner, SceneId sceneId)
    {
        // 씬 권한이 없는 참가자가 네트워크 씬 정보를 변경하지 못하도록 요청 주체를 검증합니다.
        if (runner == null || !runner.IsRunning || !runner.IsSceneAuthority)
        {
            return false;
        }

        if (IsLoading || runner.IsSceneManagerBusy)
        {
            return false;
        }

        IsLoading = true;
        SceneLoadStarted?.Invoke(sceneId);

        try
        {
            // Single 모드로 로비를 교체하고 Runner가 변경된 씬 정보를 모든 참가자에게 전달하게 합니다.
            NetworkSceneAsyncOp operation = runner.LoadScene(
                GetSceneName(sceneId),
                LoadSceneMode.Single,
                LocalPhysicsMode.None,
                true);
            operation.AddOnCompleted(completedOperation => CompleteNetworkSceneLoad(sceneId, completedOperation));
            return true;
        }
        catch (Exception exception)
        {
            IsLoading = false;
            Debug.LogException(exception, this);
            return false;
        }
    }

    private void CompleteNetworkSceneLoad(SceneId sceneId, NetworkSceneAsyncOp operation)
    {
        IsLoading = false;

        if (operation.Error != null)
        {
            Debug.LogException(operation.Error, this);
            return;
        }

        // 네트워크 씬 로드가 끝난 뒤에도 기존 완료 이벤트 흐름을 동일하게 유지합니다.
        SceneLoadCompleted?.Invoke(sceneId);
    }

    private IEnumerator LoadSceneRoutine(SceneId sceneId)
    {
        IsLoading = true;
        SceneLoadStarted?.Invoke(sceneId);

        AsyncOperation operation = SceneManager.LoadSceneAsync(GetSceneName(sceneId));

        while (!operation.isDone)
        {
            yield return null;
        }

        IsLoading = false;
        SceneLoadCompleted?.Invoke(sceneId);
    }

    private static string GetSceneName(SceneId sceneId)
    {
        switch (sceneId)
        {
            case SceneId.Lobby:
                return "Lobby";
            case SceneId.MainStage:
                return "MainStage";
            default:
                throw new ArgumentOutOfRangeException(nameof(sceneId), sceneId, null);
        }
    }
}
