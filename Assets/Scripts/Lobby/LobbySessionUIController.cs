using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 버튼과 입력 필드를 세션 서비스에 연결하고 연결 결과를 대기실 UI에 반영합니다.
/// </summary>
public sealed class LobbySessionUIController : MonoBehaviour
{
    private const string RoomCodePrefix = "Room Code: ";

    [Header("세션")]
    [Tooltip("Fusion 세션 생성과 참가를 처리하는 서비스입니다.")]
    [SerializeField] private LobbySessionService sessionService;

    [Header("메인 로비 UI")]
    [Tooltip("참가할 숫자 6자리 룸 코드를 입력하는 필드입니다.")]
    [SerializeField] private TMP_InputField roomCodeInputField;
    [Tooltip("새 세션을 생성하는 버튼입니다.")]
    [SerializeField] private Button createRoomButton;
    [Tooltip("입력한 룸 코드의 세션에 참가하는 버튼입니다.")]
    [SerializeField] private Button joinRoomButton;
    [Tooltip("세션 연결 상태와 오류를 표시할 선택 항목입니다.")]
    [SerializeField] private TMP_Text statusText;

    [Header("대기실 UI")]
    [Tooltip("세션 연결에 성공하면 표시할 대기실 패널입니다.")]
    [SerializeField] private GameObject waitingRoomPanel;
    [Tooltip("현재 세션의 룸 코드를 표시할 텍스트입니다.")]
    [SerializeField] private TMP_Text waitingRoomCodeText;
    [Tooltip("현재 세션의 룸 코드를 시스템 클립보드에 복사하는 버튼입니다.")]
    [SerializeField] private Button copyRoomCodeButton;

    private bool isHandlingRequest;

    private void Awake()
    {
        // Inspector 참조가 없을 때 씬 또는 유지 중인 서비스 인스턴스를 보조적으로 찾습니다.
        if (sessionService == null)
        {
            sessionService = LobbySessionService.Instance != null
                ? LobbySessionService.Instance
                : FindFirstObjectByType<LobbySessionService>();
        }

        if (roomCodeInputField != null)
        {
            // 룸 코드가 6자를 초과해 입력되지 않도록 UI 단계에서도 제한합니다.
            roomCodeInputField.characterLimit = RoomCodeUtility.CodeLength;
        }

        if (waitingRoomPanel != null)
        {
            // 세션 연결에 성공하기 전에는 대기실을 노출하지 않습니다.
            waitingRoomPanel.SetActive(false);
        }

        RefreshInteractableState();
    }

    private void OnEnable()
    {
        // 버튼의 Inspector 이벤트에 의존하지 않고 이 컨트롤러가 이벤트 수명을 관리합니다.
        if (createRoomButton != null)
        {
            createRoomButton.onClick.AddListener(CreateRoom);
        }

        if (joinRoomButton != null)
        {
            joinRoomButton.onClick.AddListener(JoinRoom);
        }

        if (copyRoomCodeButton != null)
        {
            copyRoomCodeButton.onClick.AddListener(CopyRoomCode);
        }

        if (roomCodeInputField != null)
        {
            roomCodeInputField.onValueChanged.AddListener(HandleRoomCodeChanged);
        }
    }

    private void OnDisable()
    {
        if (createRoomButton != null)
        {
            createRoomButton.onClick.RemoveListener(CreateRoom);
        }

        if (joinRoomButton != null)
        {
            joinRoomButton.onClick.RemoveListener(JoinRoom);
        }

        if (copyRoomCodeButton != null)
        {
            copyRoomCodeButton.onClick.RemoveListener(CopyRoomCode);
        }

        if (roomCodeInputField != null)
        {
            roomCodeInputField.onValueChanged.RemoveListener(HandleRoomCodeChanged);
        }
    }

    public async void CreateRoom()
    {
        if (!CanStartRequest())
        {
            return;
        }

        SetRequestState(true, "세션을 생성하고 있습니다.");
        LobbySessionStartResult result = await sessionService.CreateSessionAsync();

        // 비동기 연결 도중 씬이 바뀌어 객체가 파괴된 경우 UI 갱신을 중단합니다.
        if (this == null)
        {
            return;
        }

        CompleteRequest(result);
    }

    public async void JoinRoom()
    {
        if (!CanStartRequest())
        {
            return;
        }

        if (roomCodeInputField == null)
        {
            ShowStatus("룸 코드 입력 필드가 연결되지 않았습니다.");
            return;
        }

        string roomCode = RoomCodeUtility.Normalize(roomCodeInputField.text);
        if (!RoomCodeUtility.IsValid(roomCode))
        {
            ShowStatus("룸 코드를 숫자 6자리로 입력해 주세요.");
            RefreshInteractableState();
            return;
        }

        SetRequestState(true, "세션에 참가하고 있습니다.");
        LobbySessionStartResult result = await sessionService.JoinSessionAsync(roomCode);

        if (this == null)
        {
            return;
        }

        CompleteRequest(result);
    }

    /// <summary>
    /// 연결된 세션의 숫자 6자리 룸 코드를 운영체제 클립보드에 복사합니다.
    /// </summary>
    public void CopyRoomCode()
    {
        if (sessionService == null || !RoomCodeUtility.IsValid(sessionService.CurrentRoomCode))
        {
            ShowStatus("복사할 룸 코드가 없습니다.");
            return;
        }

        // 접두사 없이 숫자 코드만 저장해 입력 필드에 바로 붙여넣을 수 있게 합니다.
        GUIUtility.systemCopyBuffer = sessionService.CurrentRoomCode;
        ShowStatus("룸 코드가 클립보드에 복사되었습니다.");
    }

    private void HandleRoomCodeChanged(string value)
    {
        // 붙여넣기를 포함해 숫자가 아닌 문자는 즉시 제거합니다.
        string normalizedRoomCode = RoomCodeUtility.Normalize(value);
        if (normalizedRoomCode != value)
        {
            roomCodeInputField.SetTextWithoutNotify(normalizedRoomCode);
        }

        RefreshInteractableState();
    }

    private bool CanStartRequest()
    {
        if (isHandlingRequest)
        {
            return false;
        }

        if (sessionService != null)
        {
            return true;
        }

        ShowStatus("세션 서비스가 연결되지 않았습니다.");
        return false;
    }

    private void SetRequestState(bool isRequesting, string message)
    {
        isHandlingRequest = isRequesting;
        ShowStatus(message);
        RefreshInteractableState();
    }

    private void CompleteRequest(LobbySessionStartResult result)
    {
        isHandlingRequest = false;

        if (!result.IsSuccess)
        {
            ShowStatus(result.ErrorMessage);
            RefreshInteractableState();
            return;
        }

        if (waitingRoomCodeText != null)
        {
            // 생성자와 참가자 모두 실제로 연결된 세션 코드를 동일하게 표시합니다.
            waitingRoomCodeText.text = RoomCodePrefix + result.RoomCode;
        }

        if (waitingRoomPanel != null)
        {
            waitingRoomPanel.SetActive(true);
        }

        ShowStatus(string.Empty);
        RefreshInteractableState();
    }

    private void RefreshInteractableState()
    {
        bool canRequest = !isHandlingRequest && sessionService != null && sessionService.Runner == null;

        if (createRoomButton != null)
        {
            createRoomButton.interactable = canRequest;
        }

        if (joinRoomButton != null)
        {
            bool hasValidRoomCode = roomCodeInputField != null
                && RoomCodeUtility.IsValid(RoomCodeUtility.Normalize(roomCodeInputField.text));
            joinRoomButton.interactable = canRequest && hasValidRoomCode;
        }

        if (copyRoomCodeButton != null)
        {
            // 실제 세션 코드가 준비된 뒤에만 복사 버튼을 사용할 수 있습니다.
            copyRoomCodeButton.interactable = sessionService != null
                && RoomCodeUtility.IsValid(sessionService.CurrentRoomCode);
        }
    }

    private void ShowStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        if (!string.IsNullOrEmpty(message))
        {
            Debug.Log($"{nameof(LobbySessionUIController)}: {message}", this);
        }
    }
}
