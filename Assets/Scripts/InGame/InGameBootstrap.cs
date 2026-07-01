using System;
using System.Threading.Tasks;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// InGame을 직접 실행했을 때 개발용 Single Runner를 준비합니다.
/// </summary>
public sealed class InGameBootstrap : MonoBehaviour
{
    public NetworkRunner Runner { get; private set; }
    public bool IsRunnerReady { get; private set; }

    public event Action<NetworkRunner> RunnerReady;

    private NetworkRunner createdRunner;

    private async void Start()
    {
        // 씬의 Awake와 OnEnable 처리가 끝난 뒤 Runner 초기화를 시작합니다.
        // 로비에서 유지된 실행 중인 Runner가 있으면 새 Runner를 만들지 않고 준비 완료를 알립니다.
        NetworkRunner existingRunner = FindFirstObjectByType<NetworkRunner>();
        if (existingRunner != null)
        {
            if (existingRunner.IsRunning)
            {
                CompleteRunnerSetup(existingRunner);
            }
            else
            {
                Debug.LogWarning($"{nameof(InGameBootstrap)}: 기존 Runner가 아직 실행 준비되지 않았습니다.", this);
            }

            return;
        }

        await StartSinglePlayerRunnerAsync();
    }

    private async Task StartSinglePlayerRunnerAsync()
    {
        // Fusion이 DontDestroyOnLoad를 적용할 수 있도록 Runner를 루트 GameObject로 생성합니다.
        GameObject runnerObject = new GameObject("InGameSinglePlayerRunner");
        createdRunner = runnerObject.AddComponent<NetworkRunner>();
        createdRunner.ProvideInput = true;

        NetworkSceneManagerDefault sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
        NetworkObjectProviderDefault objectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>();

        Scene activeScene = SceneManager.GetActiveScene();
        SceneRef activeSceneRef = SceneRef.FromIndex(activeScene.buildIndex);
        NetworkSceneInfo sceneInfo = new NetworkSceneInfo();

        if (activeSceneRef.IsValid)
        {
            // 이미 열린 InGame을 Single Runner의 초기 네트워크 씬으로 등록합니다.
            sceneInfo.AddSceneRef(activeSceneRef, LoadSceneMode.Additive);
        }

        try
        {
            StartGameResult result = await createdRunner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                Scene = sceneInfo,
                SceneManager = sceneManager,
                ObjectProvider = objectProvider
            });

            if (!result.Ok)
            {
                Debug.LogError($"{nameof(InGameBootstrap)}: Single Runner를 시작하지 못했습니다. ({result.ShutdownReason})", this);
                DestroyCreatedRunner();
                return;
            }

            // StartGame이 성공한 뒤에만 Ready 상태와 이벤트를 갱신합니다.
            CompleteRunnerSetup(createdRunner);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            DestroyCreatedRunner();
        }
    }

    private void CompleteRunnerSetup(NetworkRunner runner)
    {
        Runner = runner;
        IsRunnerReady = true;

        // Runner 준비가 끝난 뒤 입력 콜백을 연결하고 스폰 시스템에 준비 완료를 전달합니다.
        NetworkInputManager inputManager = FindFirstObjectByType<NetworkInputManager>();
        if (inputManager != null)
        {
            runner.RemoveCallbacks(inputManager);
            runner.AddCallbacks(inputManager);
        }

        RunnerReady?.Invoke(runner);
    }

    private void DestroyCreatedRunner()
    {
        if (createdRunner == null)
        {
            return;
        }

        // 시작에 실패한 Runner를 남기지 않아 다음 실행에서 기존 Runner로 오인하지 않게 합니다.
        Destroy(createdRunner.gameObject);
        createdRunner = null;
        Runner = null;
        IsRunnerReady = false;
    }
}
