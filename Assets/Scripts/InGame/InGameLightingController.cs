using UnityEngine;

public sealed class InGameLightingController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("시간 흐름을 제공하는 인게임 시간 매니저입니다. 비어 있으면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private InGameTimeManager timeManager;
    [Tooltip("낮, 노을, 밤 색상과 강도를 적용할 태양광입니다. 비어 있으면 이 오브젝트의 Light를 우선 사용합니다.")]
    [SerializeField] private Light sunLight;

    [Header("Light Transition")]
    [Tooltip("낮에서 밤까지의 진행도에 따라 적용할 태양광 색상입니다.")]
    [SerializeField] private Gradient lightColorByProgress = CreateDefaultLightColorGradient();
    [Tooltip("낮에서 밤까지의 진행도에 따라 적용할 태양광 강도입니다.")]
    [SerializeField] private AnimationCurve lightIntensityByProgress = CreateDefaultLightIntensityCurve();

    [Header("Ambient")]
    [Tooltip("활성화하면 씬의 Ambient Intensity도 시간에 따라 함께 조정합니다.")]
    [SerializeField] private bool controlAmbientIntensity = true;
    [Tooltip("낮에서 밤까지의 진행도에 따라 적용할 Ambient Intensity입니다.")]
    [SerializeField] private AnimationCurve ambientIntensityByProgress = CreateDefaultAmbientIntensityCurve();

    private void Awake()
    {
        EnsureReferences();
        EnsureDefaults();
    }

    private void OnEnable()
    {
        EnsureReferences();
        EnsureDefaults();

        if (timeManager == null)
        {
            Debug.LogWarning($"{nameof(InGameLightingController)}: {nameof(InGameTimeManager)}를 찾지 못했습니다.", this);
            return;
        }

        timeManager.TimeUpdated -= ApplyLighting;
        timeManager.TimeUpdated += ApplyLighting;
        ApplyLighting(timeManager.ElapsedSeconds, timeManager.DayNightProgress);
    }

    private void OnDisable()
    {
        if (timeManager != null)
        {
            timeManager.TimeUpdated -= ApplyLighting;
        }
    }

    private void OnValidate()
    {
        EnsureDefaults();
    }

    private void ApplyLighting(float elapsedSeconds, float dayNightProgress)
    {
        float progress = Mathf.Clamp01(dayNightProgress);

        if (sunLight != null)
        {
            sunLight.color = lightColorByProgress.Evaluate(progress);
            sunLight.intensity = Mathf.Max(0f, lightIntensityByProgress.Evaluate(progress));
        }

        if (controlAmbientIntensity)
        {
            RenderSettings.ambientIntensity = Mathf.Max(0f, ambientIntensityByProgress.Evaluate(progress));
        }
    }

    private void EnsureReferences()
    {
        if (timeManager == null)
        {
            timeManager = FindFirstObjectByType<InGameTimeManager>();
        }

        if (sunLight == null)
        {
            sunLight = GetComponent<Light>();
        }

        if (sunLight == null && RenderSettings.sun != null)
        {
            sunLight = RenderSettings.sun;
        }
    }

    private void EnsureDefaults()
    {
        if (lightColorByProgress == null)
        {
            lightColorByProgress = CreateDefaultLightColorGradient();
        }

        if (lightIntensityByProgress == null || lightIntensityByProgress.length == 0)
        {
            lightIntensityByProgress = CreateDefaultLightIntensityCurve();
        }

        if (ambientIntensityByProgress == null || ambientIntensityByProgress.length == 0)
        {
            ambientIntensityByProgress = CreateDefaultAmbientIntensityCurve();
        }
    }

    private static Gradient CreateDefaultLightColorGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.96f, 0.84f), 0f),
                new GradientColorKey(new Color(1f, 0.86f, 0.58f), 0.72f),
                new GradientColorKey(new Color(1f, 0.32f, 0.14f), 0.9f),
                new GradientColorKey(new Color(0.06f, 0.09f, 0.18f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        return gradient;
    }

    private static AnimationCurve CreateDefaultLightIntensityCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.72f, 1f),
            new Keyframe(0.9f, 0.25f),
            new Keyframe(1f, 0.04f));
    }

    private static AnimationCurve CreateDefaultAmbientIntensityCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.72f, 1f),
            new Keyframe(0.9f, 0.28f),
            new Keyframe(1f, 0.06f));
    }
}
