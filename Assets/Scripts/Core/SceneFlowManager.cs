using System;
using System.Collections;
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

    public void LoadScene(SceneId sceneId)
    {
        if (IsLoading)
        {
            return;
        }

        StartCoroutine(LoadSceneRoutine(sceneId));
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
