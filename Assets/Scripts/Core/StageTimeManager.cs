using System;
using UnityEngine;

public enum StageTimePhase
{
    Day,
    Dusk,
    Night
}

public sealed class StageTimeManager : MonoBehaviour
{
    private const float HoursPerDay = 24f;
    private const int MinutesPerDay = 24 * 60;

    [Header("Clock")]
    [Tooltip("활성화하면 스테이지 시작 시 시간이 자동으로 흐릅니다.")]
    [SerializeField] private bool playOnStart = true;
    [Tooltip("낮 단계가 지속되는 실제 초입니다.")]
    [Min(0f)]
    [SerializeField] private float dayDurationSeconds = 480f;
    [Tooltip("밤이 시작되기 직전 황혼 단계가 지속되는 실제 초입니다.")]
    [Min(0f)]
    [SerializeField] private float duskDurationSeconds = 120f;

    [Header("Display Time")]
    [Tooltip("스테이지 시작 시 표시할 인게임 시각입니다.")]
    [Range(0f, 24f)]
    [SerializeField] private float dayStartHour = 6f;
    [Tooltip("밤이 시작될 때 표시할 인게임 시각입니다.")]
    [Range(0f, 24f)]
    [SerializeField] private float nightStartHour = 20f;

    [Header("Danger")]
    [Tooltip("밤 시작 후 위험도가 커브의 최종 값에 도달할 때까지 걸리는 실제 초입니다.")]
    [Min(0f)]
    [SerializeField] private float nightEscalationDurationSeconds = 600f;
    [SerializeField] private AnimationCurve dangerCurve = AnimationCurve.Linear(0f, 1f, 1f, 2f);

    public float ElapsedSeconds { get; private set; }
    public bool IsRunning { get; private set; }
    public StageTimePhase CurrentPhase { get; private set; }
    public float DayDurationSeconds { get { return dayDurationSeconds; } }
    public float SecondsUntilNightStart { get { return dayDurationSeconds + duskDurationSeconds; } }
    public float SecondsUntilMaxDanger { get { return SecondsUntilNightStart + nightEscalationDurationSeconds; } }
    public float NightDangerProgress
    {
        get
        {
            if (NightElapsedSeconds <= 0f)
            {
                return 0f;
            }

            if (nightEscalationDurationSeconds <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(NightElapsedSeconds / nightEscalationDurationSeconds);
        }
    }

    public float DayNightProgress
    {
        get
        {
            float secondsUntilNight = SecondsUntilNightStart;
            if (secondsUntilNight <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(ElapsedSeconds / secondsUntilNight);
        }
    }

    public float NightElapsedSeconds
    {
        get { return Mathf.Max(0f, ElapsedSeconds - SecondsUntilNightStart); }
    }

    public float DangerMultiplier
    {
        get
        {
            if (dangerCurve == null)
            {
                return 1f;
            }

            return dangerCurve.Evaluate(NightDangerProgress);
        }
    }

    public float InGameHour
    {
        get
        {
            float hourDeltaUntilNight = GetPositiveHourDelta(dayStartHour, nightStartHour);
            float hoursPerSecond = GetHoursPerSecond(hourDeltaUntilNight);
            float elapsedGameHours = CurrentPhase == StageTimePhase.Night
                ? hourDeltaUntilNight + NightElapsedSeconds * hoursPerSecond
                : hourDeltaUntilNight * DayNightProgress;

            return WrapHour(dayStartHour + elapsedGameHours);
        }
    }

    public float InGameHour01
    {
        get { return InGameHour / HoursPerDay; }
    }

    public string FormattedTime
    {
        get
        {
            int totalMinutes = Mathf.FloorToInt(InGameHour * 60f) % MinutesPerDay;
            int hour = totalMinutes / 60;
            int minute = totalMinutes % 60;

            return string.Format("{0:00}:{1:00}", hour, minute);
        }
    }

    public event Action<StageTimePhase> PhaseChanged;
    public event Action<float, float> TimeUpdated;

    private void Awake()
    {
        RefreshPhase();
    }

    private void Start()
    {
        if (playOnStart)
        {
            StartClock();
        }
    }

    private void Update()
    {
        if (!IsRunning)
        {
            return;
        }

        SetElapsedSeconds(ElapsedSeconds + Time.deltaTime);
    }

    private void OnValidate()
    {
        dayDurationSeconds = Mathf.Max(0f, dayDurationSeconds);
        duskDurationSeconds = Mathf.Max(0f, duskDurationSeconds);
        nightEscalationDurationSeconds = Mathf.Max(0f, nightEscalationDurationSeconds);
        dayStartHour = WrapHour(dayStartHour);
        nightStartHour = WrapHour(nightStartHour);

        if (dangerCurve == null || dangerCurve.length == 0)
        {
            dangerCurve = AnimationCurve.Linear(0f, 1f, 1f, 2f);
        }

        RefreshPhase();
    }

    public void StartClock()
    {
        SetElapsedSeconds(0f);
        IsRunning = true;
    }

    public void PauseClock()
    {
        IsRunning = false;
    }

    public void ResumeClock()
    {
        IsRunning = true;
    }

    public void ResetClock()
    {
        IsRunning = false;
        SetElapsedSeconds(0f);
    }

    public void AddTime(float seconds)
    {
        SetElapsedSeconds(ElapsedSeconds + seconds);
    }

    public void SetElapsedSeconds(float seconds)
    {
        StageTimePhase previousPhase = CurrentPhase;

        ElapsedSeconds = Mathf.Max(0f, seconds);
        RefreshPhase();

        if (CurrentPhase != previousPhase)
        {
            PhaseChanged?.Invoke(CurrentPhase);
        }

        TimeUpdated?.Invoke(ElapsedSeconds, DayNightProgress);
    }

    private void RefreshPhase()
    {
        CurrentPhase = CalculatePhase();
    }

    private StageTimePhase CalculatePhase()
    {
        if (ElapsedSeconds < DayDurationSeconds)
        {
            return StageTimePhase.Day;
        }

        if (ElapsedSeconds < SecondsUntilNightStart)
        {
            return StageTimePhase.Dusk;
        }

        return StageTimePhase.Night;
    }

    private float GetHoursPerSecond(float hourDeltaUntilNight)
    {
        float secondsUntilNight = SecondsUntilNightStart;
        if (secondsUntilNight <= 0f)
        {
            return 0f;
        }

        return hourDeltaUntilNight / secondsUntilNight;
    }

    private static float GetPositiveHourDelta(float startHour, float endHour)
    {
        return Mathf.Repeat(endHour - startHour, HoursPerDay);
    }

    private static float WrapHour(float hour)
    {
        return Mathf.Repeat(hour, HoursPerDay);
    }
}
