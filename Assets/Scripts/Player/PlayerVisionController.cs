using Fusion;
using UnityEngine;

public sealed class PlayerVisionController : MonoBehaviour
{
    private const int MaxEnvironmentVisionLightCount = 16;

    private static readonly int PlayerPositionId = Shader.PropertyToID("_PlayerPosition");
    private static readonly int ViewForwardId = Shader.PropertyToID("_ViewForwardXZ");
    private static readonly int ViewDistanceId = Shader.PropertyToID("_ViewDistance");
    private static readonly int ViewAngleId = Shader.PropertyToID("_ViewAngle");
    private static readonly int NearVisionRadiusId = Shader.PropertyToID("_NearVisionRadius");
    private static readonly int FlashlightEnabledId = Shader.PropertyToID("_FlashlightEnabled");
    private static readonly int FlashlightDistanceId = Shader.PropertyToID("_FlashlightDistance");
    private static readonly int FlashlightAngleId = Shader.PropertyToID("_FlashlightAngle");
    private static readonly int EnvironmentVisionLightCountId = Shader.PropertyToID("_EnvironmentVisionLightCount");
    private static readonly int EnvironmentVisionLightDataId = Shader.PropertyToID("_EnvironmentVisionLightData");
    private static readonly int DarknessAlphaId = Shader.PropertyToID("_DarknessAlpha");
    private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
    private static readonly int GroundYId = Shader.PropertyToID("_GroundY");
    private static readonly int CameraWorldPositionId = Shader.PropertyToID("_CameraWorldPosition");
    private static readonly int InverseViewProjectionId = Shader.PropertyToID("_InverseViewProjection");

    [Header("References")]
    [Tooltip("시간 흐름을 제공하는 인게임 시간 매니저입니다. 비어 있으면 씬에서 자동으로 찾습니다.")]
    [SerializeField] private InGameTimeManager timeManager;
    [Tooltip("시야 마스크를 표시할 로컬 플레이어 카메라입니다. 비어 있으면 자식 카메라를 자동으로 찾습니다.")]
    [SerializeField] private Camera localCamera;
    [Tooltip("카메라 앞에 생성되는 시야 오버레이에 사용할 머티리얼입니다.")]
    [SerializeField] private Material overlayMaterialTemplate;

    [Header("Vision Distance")]
    [Min(0f)]
    [SerializeField] private float dayDistance = 18f;
    [Min(0f)]
    [SerializeField] private float duskDistance = 12f;
    [Min(0f)]
    [SerializeField] private float nightDistance = 6f;

    [Header("Vision Angle")]
    [Range(1f, 360f)]
    [SerializeField] private float dayAngle = 140f;
    [Range(1f, 360f)]
    [SerializeField] private float duskAngle = 100f;
    [Range(1f, 360f)]
    [SerializeField] private float nightAngle = 60f;

    [Header("Equipment Vision")]
    [Min(0f)]
    [SerializeField] private float nearVisionRadius = 2.4f;
    [Min(0f)]
    [SerializeField] private float flashlightDistance = 9f;
    [Range(1f, 180f)]
    [SerializeField] private float flashlightAngle = 55f;

    [Header("Equipment State")]
    [Tooltip("장비 시스템이 붙기 전까지 Inspector에서 손전등 테스트에 사용합니다.")]
    [SerializeField] private bool flashlightEnabled;

    [Header("Environment Vision")]
    [Tooltip("시야 마스크에 반영할 주변 환경 광원의 최대 개수입니다.")]
    [Range(0, MaxEnvironmentVisionLightCount)]
    [SerializeField] private int maxEnvironmentVisionLights = MaxEnvironmentVisionLightCount;
    [Tooltip("플레이어 주변 환경 광원을 수집할 거리입니다.")]
    [Min(0f)]
    [SerializeField] private float environmentVisionSearchRadius = 30f;

    [Header("Overlay")]
    [Tooltip("낮에서 밤까지의 진행도에 따라 적용할 어둠 오버레이 알파입니다.")]
    [SerializeField] private AnimationCurve darknessAlphaByProgress = CreateDefaultDarknessCurve();
    [Tooltip("시야 가장자리가 부드럽게 사라지는 거리입니다.")]
    [Min(0.01f)]
    [SerializeField] private float softness = 1.5f;

    [Header("Runtime Lights")]
    [SerializeField] private bool createRuntimeLights = true;
    [Min(0f)]
    [SerializeField] private float flashlightIntensity = 2.2f;

    private NetworkObject networkObject;
    private Material overlayMaterialInstance;
    private GameObject overlayObject;
    private Light flashlightLight;
    private readonly Vector4[] environmentVisionLightData = new Vector4[MaxEnvironmentVisionLightCount];
    private int environmentVisionLightCount;
    private bool isVisionActive;

    private void Awake()
    {
        EnsureReferences();
        EnsureDefaults();
    }

    private void Start()
    {
        EnsureReferences();
        SetVisionActive(ShouldEnableLocalVision());
    }

    private void LateUpdate()
    {
        bool shouldBeActive = ShouldEnableLocalVision();
        if (shouldBeActive != isVisionActive)
        {
            SetVisionActive(shouldBeActive);
        }

        if (!isVisionActive || localCamera == null)
        {
            return;
        }

        EnsureOverlay();
        EnsureRuntimeLights();
        UpdateOverlayTransform();

        float progress = GetDayNightProgress();
        float viewDistance = GetCurrentBaseViewDistance(progress);
        float viewAngle = GetCurrentBaseViewAngle(progress);
        float darknessAlpha = Mathf.Clamp01(darknessAlphaByProgress.Evaluate(progress));

        CollectEnvironmentVisionLights();
        UpdateOverlayMaterial(viewDistance, viewAngle, darknessAlpha);
        UpdateRuntimeLights();
    }

    private void OnDisable()
    {
        SetVisionActive(false);
    }

    private void OnDestroy()
    {
        if (overlayObject != null)
        {
            Destroy(overlayObject);
        }

        if (overlayMaterialInstance != null)
        {
            Destroy(overlayMaterialInstance);
        }
    }

    private void OnValidate()
    {
        dayDistance = Mathf.Max(0f, dayDistance);
        duskDistance = Mathf.Max(0f, duskDistance);
        nightDistance = Mathf.Max(0f, nightDistance);
        dayAngle = Mathf.Clamp(dayAngle, 1f, 360f);
        duskAngle = Mathf.Clamp(duskAngle, 1f, 360f);
        nightAngle = Mathf.Clamp(nightAngle, 1f, 360f);
        nearVisionRadius = Mathf.Max(0f, nearVisionRadius);
        flashlightDistance = Mathf.Max(0f, flashlightDistance);
        flashlightAngle = Mathf.Clamp(flashlightAngle, 1f, 180f);
        maxEnvironmentVisionLights = Mathf.Clamp(maxEnvironmentVisionLights, 0, MaxEnvironmentVisionLightCount);
        environmentVisionSearchRadius = Mathf.Max(0f, environmentVisionSearchRadius);
        softness = Mathf.Max(0.01f, softness);
        flashlightIntensity = Mathf.Max(0f, flashlightIntensity);

        EnsureDefaults();
    }

    public bool IsPointVisible(Vector3 worldPoint)
    {
        float progress = GetDayNightProgress();
        float viewDistance = GetCurrentBaseViewDistance(progress);
        float viewAngle = GetCurrentBaseViewAngle(progress);
        Vector3 toPoint = worldPoint - transform.position;
        toPoint.y = 0f;

        float sqrDistance = toPoint.sqrMagnitude;
        if (sqrDistance <= nearVisionRadius * nearVisionRadius)
        {
            return true;
        }

        if (sqrDistance <= Mathf.Epsilon)
        {
            return false;
        }

        Vector3 forward = GetPlanarForward();
        Vector3 direction = toPoint.normalized;

        if (IsInsideVisionCone(direction, sqrDistance, forward, viewDistance, viewAngle))
        {
            return true;
        }

        if (IsVisibleByEnvironmentLight(worldPoint))
        {
            return true;
        }

        return flashlightEnabled &&
            IsInsideVisionCone(direction, sqrDistance, forward, flashlightDistance, flashlightAngle);
    }

    public void SetFlashlightEnabled(bool enabled)
    {
        flashlightEnabled = enabled;
    }

    private void EnsureReferences()
    {
        if (timeManager == null)
        {
            timeManager = FindFirstObjectByType<InGameTimeManager>();
        }

        if (localCamera == null)
        {
            localCamera = GetComponentInChildren<Camera>(true);
        }

        if (networkObject == null)
        {
            networkObject = GetComponent<NetworkObject>();
        }
    }

    private void EnsureDefaults()
    {
        if (darknessAlphaByProgress == null || darknessAlphaByProgress.length == 0)
        {
            darknessAlphaByProgress = CreateDefaultDarknessCurve();
        }
    }

    private bool ShouldEnableLocalVision()
    {
        if (localCamera == null)
        {
            return false;
        }

        if (networkObject == null || networkObject.Runner == null)
        {
            return true;
        }

        return networkObject.HasInputAuthority;
    }

    private void SetVisionActive(bool active)
    {
        isVisionActive = active;

        if (overlayObject != null)
        {
            overlayObject.SetActive(active);
        }

        if (flashlightLight != null)
        {
            flashlightLight.gameObject.SetActive(active && createRuntimeLights && flashlightEnabled);
        }
    }

    private void EnsureOverlay()
    {
        if (overlayObject != null)
        {
            return;
        }

        if (localCamera == null)
        {
            return;
        }

        if (overlayMaterialTemplate != null)
        {
            overlayMaterialInstance = new Material(overlayMaterialTemplate);
        }
        else
        {
            Shader shader = Shader.Find("AlphaProject/PlayerVisionOverlay");
            if (shader == null)
            {
                Debug.LogWarning($"{nameof(PlayerVisionController)}: PlayerVisionOverlay shader를 찾지 못했습니다.", this);
                return;
            }

            overlayMaterialInstance = new Material(shader);
        }

        overlayMaterialInstance.name = "PlayerVisionOverlay (Instance)";

        overlayObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        overlayObject.name = "PlayerVisionOverlay";
        overlayObject.transform.SetParent(localCamera.transform, false);

        Collider overlayCollider = overlayObject.GetComponent<Collider>();
        if (overlayCollider != null)
        {
            Destroy(overlayCollider);
        }

        MeshRenderer renderer = overlayObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = overlayMaterialInstance;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void EnsureRuntimeLights()
    {
        if (!createRuntimeLights || flashlightLight != null)
        {
            return;
        }

        flashlightLight = CreateVisionLight("Vision Flashlight", LightType.Spot);
    }

    private Light CreateVisionLight(string objectName, LightType type)
    {
        GameObject lightObject = new GameObject(objectName);
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = Vector3.up * 1.2f;
        lightObject.transform.localRotation = Quaternion.identity;

        Light visionLight = lightObject.AddComponent<Light>();
        visionLight.type = type;
        visionLight.color = new Color(1f, 0.88f, 0.62f);
        visionLight.shadows = LightShadows.None;
        visionLight.renderMode = LightRenderMode.Auto;
        return visionLight;
    }

    private void UpdateOverlayTransform()
    {
        if (overlayObject == null || localCamera == null)
        {
            return;
        }

        float distance = localCamera.nearClipPlane + 0.02f;
        float height = 2f * distance * Mathf.Tan(localCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * localCamera.aspect;

        overlayObject.transform.localPosition = Vector3.forward * distance;
        overlayObject.transform.localRotation = Quaternion.identity;
        overlayObject.transform.localScale = new Vector3(width, height, 1f);
    }

    private void UpdateOverlayMaterial(float viewDistance, float viewAngle, float darknessAlpha)
    {
        if (overlayMaterialInstance == null || localCamera == null)
        {
            return;
        }

        Matrix4x4 viewMatrix = localCamera.worldToCameraMatrix;
        Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix(localCamera.projectionMatrix, false);
        Matrix4x4 inverseViewProjection = (projectionMatrix * viewMatrix).inverse;

        Vector3 forward = GetPlanarForward();
        overlayMaterialInstance.SetVector(PlayerPositionId, transform.position);
        overlayMaterialInstance.SetVector(ViewForwardId, new Vector4(forward.x, forward.z, 0f, 0f));
        overlayMaterialInstance.SetFloat(ViewDistanceId, Mathf.Max(0.01f, viewDistance));
        overlayMaterialInstance.SetFloat(ViewAngleId, viewAngle);
        overlayMaterialInstance.SetFloat(NearVisionRadiusId, nearVisionRadius);
        overlayMaterialInstance.SetFloat(FlashlightEnabledId, flashlightEnabled ? 1f : 0f);
        overlayMaterialInstance.SetFloat(FlashlightDistanceId, Mathf.Max(0.01f, flashlightDistance));
        overlayMaterialInstance.SetFloat(FlashlightAngleId, flashlightAngle);
        overlayMaterialInstance.SetInt(EnvironmentVisionLightCountId, environmentVisionLightCount);
        overlayMaterialInstance.SetVectorArray(EnvironmentVisionLightDataId, environmentVisionLightData);
        overlayMaterialInstance.SetFloat(DarknessAlphaId, darknessAlpha);
        overlayMaterialInstance.SetFloat(SoftnessId, softness);
        overlayMaterialInstance.SetFloat(GroundYId, transform.position.y);
        overlayMaterialInstance.SetVector(CameraWorldPositionId, localCamera.transform.position);
        overlayMaterialInstance.SetMatrix(InverseViewProjectionId, inverseViewProjection);
    }

    private void UpdateRuntimeLights()
    {
        if (!createRuntimeLights)
        {
            if (flashlightLight != null)
            {
                flashlightLight.gameObject.SetActive(false);
            }

            return;
        }

        float nightWeight = Mathf.InverseLerp(0.65f, 1f, GetDayNightProgress());

        if (flashlightLight != null)
        {
            flashlightLight.gameObject.SetActive(isVisionActive && flashlightEnabled);
            flashlightLight.range = flashlightDistance;
            flashlightLight.spotAngle = flashlightAngle;
            flashlightLight.intensity = flashlightEnabled ? flashlightIntensity * nightWeight : 0f;
        }
    }

    private void CollectEnvironmentVisionLights()
    {
        environmentVisionLightCount = 0;
        ClearEnvironmentVisionLightData();

        int sourceCount = VisionLightSource.ActiveSourceCount;
        if (maxEnvironmentVisionLights <= 0 || sourceCount <= 0)
        {
            return;
        }

        float searchSqrDistance = environmentVisionSearchRadius * environmentVisionSearchRadius;
        for (int i = 0; i < sourceCount; i++)
        {
            VisionLightSource source = VisionLightSource.GetActiveSource(i);
            if (!IsValidEnvironmentVisionLight(source))
            {
                continue;
            }

            Vector3 toSource = source.Position - transform.position;
            toSource.y = 0f;
            if (toSource.sqrMagnitude > searchSqrDistance)
            {
                continue;
            }

            AddEnvironmentVisionLight(source);
        }
    }

    private void AddEnvironmentVisionLight(VisionLightSource source)
    {
        Vector4 lightData = new Vector4(
            source.Position.x,
            source.Position.z,
            source.VisionRadius,
            source.VisionIntensity);

        if (environmentVisionLightCount < maxEnvironmentVisionLights)
        {
            environmentVisionLightData[environmentVisionLightCount] = lightData;
            environmentVisionLightCount++;
            return;
        }

        int farthestIndex = FindFarthestEnvironmentVisionLightIndex();
        if (farthestIndex < 0)
        {
            return;
        }

        Vector2 playerPosition = new Vector2(transform.position.x, transform.position.z);
        Vector2 sourcePosition = new Vector2(lightData.x, lightData.y);
        Vector2 farthestPosition = new Vector2(
            environmentVisionLightData[farthestIndex].x,
            environmentVisionLightData[farthestIndex].y);

        if ((sourcePosition - playerPosition).sqrMagnitude < (farthestPosition - playerPosition).sqrMagnitude)
        {
            environmentVisionLightData[farthestIndex] = lightData;
        }
    }

    private int FindFarthestEnvironmentVisionLightIndex()
    {
        if (environmentVisionLightCount <= 0)
        {
            return -1;
        }

        int farthestIndex = 0;
        float farthestSqrDistance = -1f;
        Vector2 playerPosition = new Vector2(transform.position.x, transform.position.z);

        for (int i = 0; i < environmentVisionLightCount; i++)
        {
            Vector2 lightPosition = new Vector2(environmentVisionLightData[i].x, environmentVisionLightData[i].y);
            float sqrDistance = (lightPosition - playerPosition).sqrMagnitude;
            if (sqrDistance > farthestSqrDistance)
            {
                farthestSqrDistance = sqrDistance;
                farthestIndex = i;
            }
        }

        return farthestIndex;
    }

    private void ClearEnvironmentVisionLightData()
    {
        for (int i = 0; i < environmentVisionLightData.Length; i++)
        {
            environmentVisionLightData[i] = Vector4.zero;
        }
    }

    private bool IsVisibleByEnvironmentLight(Vector3 worldPoint)
    {
        int sourceCount = VisionLightSource.ActiveSourceCount;
        for (int i = 0; i < sourceCount; i++)
        {
            VisionLightSource source = VisionLightSource.GetActiveSource(i);
            if (!IsValidEnvironmentVisionLight(source))
            {
                continue;
            }

            Vector3 toPoint = worldPoint - source.Position;
            toPoint.y = 0f;
            if (toPoint.sqrMagnitude <= source.VisionRadius * source.VisionRadius)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsValidEnvironmentVisionLight(VisionLightSource source)
    {
        return source != null &&
            source.IsLit &&
            source.VisionRadius > 0f &&
            source.VisionIntensity > 0f;
    }

    private float GetDayNightProgress()
    {
        if (timeManager == null)
        {
            return 0f;
        }

        return Mathf.Clamp01(timeManager.DayNightProgress);
    }

    private float GetCurrentBaseViewDistance(float progress)
    {
        return EvaluateDayDuskNight(dayDistance, duskDistance, nightDistance, progress);
    }

    private float GetCurrentBaseViewAngle(float progress)
    {
        return EvaluateDayDuskNight(dayAngle, duskAngle, nightAngle, progress);
    }

    private float EvaluateDayDuskNight(float dayValue, float duskValue, float nightValue, float progress)
    {
        float duskStartProgress = GetDuskStartProgress();
        if (progress <= duskStartProgress)
        {
            float dayToDusk = duskStartProgress <= 0f ? 1f : progress / duskStartProgress;
            return Mathf.Lerp(dayValue, duskValue, dayToDusk);
        }

        float duskToNight = Mathf.InverseLerp(duskStartProgress, 1f, progress);
        return Mathf.Lerp(duskValue, nightValue, duskToNight);
    }

    private float GetDuskStartProgress()
    {
        if (timeManager == null || timeManager.SecondsUntilNightStart <= 0f)
        {
            return 0.75f;
        }

        return Mathf.Clamp01(timeManager.DayDurationSeconds / timeManager.SecondsUntilNightStart);
    }

    private Vector3 GetPlanarForward()
    {
        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude <= Mathf.Epsilon)
        {
            return Vector3.forward;
        }

        return forward.normalized;
    }

    private static bool IsInsideVisionCone(Vector3 direction, float sqrDistance, Vector3 forward, float distance, float angle)
    {
        if (sqrDistance > distance * distance)
        {
            return false;
        }

        float dot = Vector3.Dot(forward, direction);
        float minimumDot = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
        return dot >= minimumDot;
    }

    private static AnimationCurve CreateDefaultDarknessCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.12f),
            new Keyframe(0.75f, 0.28f),
            new Keyframe(0.9f, 0.55f),
            new Keyframe(1f, 0.82f));
    }
}
