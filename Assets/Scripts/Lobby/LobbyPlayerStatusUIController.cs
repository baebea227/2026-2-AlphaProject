using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비의 플레이어 상태 텍스트를 세션 로스터와 동기화합니다.
/// </summary>
public sealed class LobbyPlayerStatusUIController : MonoBehaviour
{
    private const string NotReadyStatus = "Not Ready";

    [Tooltip("플레이어 로스터를 제공하는 전역 세션 서비스입니다.")]
    [SerializeField] private LobbySessionService sessionService;
    [Tooltip("위에서부터 플레이어 참가 순서대로 사용할 상태 텍스트 목록입니다.")]
    [SerializeField] private TMP_Text[] playerStatusTexts;
    [Tooltip("로컬 플레이어의 Ready 상태를 전환하는 버튼입니다.")]
    [SerializeField] private Button readyButton;
    [Tooltip("Ready 또는 Cancel 문구를 표시하는 버튼 텍스트입니다.")]
    [SerializeField] private TMP_Text readyButtonText;
    [Tooltip("Host가 모든 플레이어의 Ready 완료 후 사용할 게임 시작 버튼입니다.")]
    [SerializeField] private Button gameStartButton;

    private LobbyPlayerRoster boundRoster;

    private void Awake()
    {
        // 세션에 연결되기 전에는 모든 플레이어 상태 슬롯을 숨깁니다.
        ClearPlayerStatusTexts();
        RefreshReadyButton();
        RefreshGameStartButton();
    }

    private void OnEnable()
    {
        ResolveSessionService();

        if (sessionService == null)
        {
            return;
        }

        sessionService.PlayerRosterReady += BindRoster;

        if (readyButton != null)
        {
            readyButton.onClick.AddListener(ToggleLocalReady);
        }

        if (gameStartButton != null)
        {
            gameStartButton.onClick.AddListener(StartGame);
        }

        // UI가 늦게 활성화된 경우 이미 생성된 로스터를 즉시 연결합니다.
        if (sessionService.PlayerRoster != null)
        {
            BindRoster(sessionService.PlayerRoster);
        }
    }

    private void OnDisable()
    {
        if (sessionService != null)
        {
            sessionService.PlayerRosterReady -= BindRoster;
        }

        if (readyButton != null)
        {
            readyButton.onClick.RemoveListener(ToggleLocalReady);
        }

        if (gameStartButton != null)
        {
            gameStartButton.onClick.RemoveListener(StartGame);
        }

        UnbindRoster();
    }

    private void ResolveSessionService()
    {
        if (sessionService != null)
        {
            return;
        }

        sessionService = LobbySessionService.Instance != null
            ? LobbySessionService.Instance
            : FindFirstObjectByType<LobbySessionService>();
    }

    private void BindRoster(LobbyPlayerRoster roster)
    {
        if (boundRoster == roster)
        {
            RefreshPlayerStatusTexts();
            return;
        }

        UnbindRoster();
        boundRoster = roster;

        if (boundRoster != null)
        {
            boundRoster.PlayerStatesChanged += RefreshPlayerStatusTexts;
        }

        RefreshPlayerStatusTexts();
    }

    private void UnbindRoster()
    {
        if (boundRoster != null)
        {
            boundRoster.PlayerStatesChanged -= RefreshPlayerStatusTexts;
        }

        boundRoster = null;
    }

    private void RefreshPlayerStatusTexts()
    {
        if (playerStatusTexts == null)
        {
            return;
        }

        int playerCount = boundRoster != null ? boundRoster.Players.Count : 0;

        for (int index = 0; index < playerStatusTexts.Length; index++)
        {
            TMP_Text statusText = playerStatusTexts[index];
            if (statusText == null)
            {
                continue;
            }

            bool hasPlayer = index < playerCount;
            statusText.gameObject.SetActive(hasPlayer);

            if (!hasPlayer)
            {
                continue;
            }

            PlayerRef player = boundRoster.Players[index];
            bool isLocalPlayer = sessionService != null
                && sessionService.Runner != null
                && player == sessionService.Runner.LocalPlayer;
            string localPlayerLabel = isLocalPlayer ? " (Me)" : string.Empty;
            string readyStatus = boundRoster.IsReady(player) ? "Ready" : NotReadyStatus;

            // 플레이어 번호는 화면의 위쪽 슬롯부터 1번으로 표시합니다.
            statusText.text = $"Player{index + 1}{localPlayerLabel}: {readyStatus}";
        }

        RefreshReadyButton();
        RefreshGameStartButton();
    }

    private void ToggleLocalReady()
    {
        // 실제 상태 변경은 로스터가 서버에 요청하고 동기화 결과를 다시 UI에 전달합니다.
        boundRoster?.ToggleLocalReady();
    }

    private void RefreshReadyButton()
    {
        bool hasLocalPlayer = boundRoster != null
            && sessionService != null
            && sessionService.Runner != null
            && sessionService.Runner.LocalPlayer.IsRealPlayer;

        if (readyButton != null)
        {
            readyButton.interactable = hasLocalPlayer;
        }

        if (readyButtonText == null)
        {
            return;
        }

        bool isReady = hasLocalPlayer && boundRoster.IsReady(sessionService.Runner.LocalPlayer);
        readyButtonText.text = isReady ? "Cancel" : "Ready";
    }

    private void RefreshGameStartButton()
    {
        if (gameStartButton == null)
        {
            return;
        }

        // Host이면서 현재 세션의 모든 참가자가 Ready일 때만 게임 시작을 허용합니다.
        bool canStartGame = boundRoster != null
            && sessionService != null
            && sessionService.Runner != null
            && sessionService.Runner.IsServer
            && boundRoster.AreAllPlayersReady;
        gameStartButton.interactable = canStartGame;
    }

    private void StartGame()
    {
        // 버튼 활성화 조건을 다시 검증해 코드 호출로 조건을 우회하지 못하게 합니다.
        bool canStartGame = boundRoster != null
            && sessionService != null
            && sessionService.Runner != null
            && sessionService.Runner.IsServer
            && boundRoster.AreAllPlayersReady;

        if (!canStartGame)
        {
            return;
        }

        SceneFlowManager sceneFlowManager = SceneFlowManager.Instance;
        if (sceneFlowManager == null)
        {
            Debug.LogError($"{nameof(LobbyPlayerStatusUIController)}: SceneFlowManager가 없습니다.", this);
            return;
        }

        // Host의 Runner로 네트워크 씬 정보를 갱신해 세션 참가자 전원을 InGame으로 이동시킵니다.
        gameStartButton.interactable = false;
        if (!sceneFlowManager.LoadNetworkInGame(sessionService.Runner))
        {
            // 씬 전환을 시작하지 못한 경우 Host가 조건을 확인하고 다시 시도할 수 있게 버튼 상태를 복구합니다.
            RefreshGameStartButton();
        }
    }

    private void ClearPlayerStatusTexts()
    {
        if (playerStatusTexts == null)
        {
            return;
        }

        foreach (TMP_Text statusText in playerStatusTexts)
        {
            if (statusText != null)
            {
                statusText.gameObject.SetActive(false);
            }
        }
    }
}
