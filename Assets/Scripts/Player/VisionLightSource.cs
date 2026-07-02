using System.Collections.Generic;
using UnityEngine;

public sealed class VisionLightSource : MonoBehaviour
{
    private static readonly List<VisionLightSource> ActiveSources = new List<VisionLightSource>();

    [Header("Vision Light")]
    [Tooltip("이 광원이 시야 마스크를 완화하는 반경입니다.")]
    [Min(0f)]
    [SerializeField] private float visionRadius = 4f;
    [Tooltip("이 광원이 어둠 마스크를 완화하는 강도입니다.")]
    [Range(0f, 1f)]
    [SerializeField] private float visionIntensity = 1f;
    [Tooltip("꺼져 있으면 시야 마스크에 영향을 주지 않습니다.")]
    [SerializeField] private bool isLit = true;

    public static int ActiveSourceCount
    {
        get { return ActiveSources.Count; }
    }

    public Vector3 Position
    {
        get { return transform.position; }
    }

    public float VisionRadius
    {
        get { return visionRadius; }
    }

    public float VisionIntensity
    {
        get { return visionIntensity; }
    }

    public bool IsLit
    {
        get { return isLit; }
    }

    private void OnEnable()
    {
        if (!ActiveSources.Contains(this))
        {
            ActiveSources.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveSources.Remove(this);
    }

    private void OnValidate()
    {
        visionRadius = Mathf.Max(0f, visionRadius);
        visionIntensity = Mathf.Clamp01(visionIntensity);
    }

    public static VisionLightSource GetActiveSource(int index)
    {
        if (index < 0 || index >= ActiveSources.Count)
        {
            return null;
        }

        return ActiveSources[index];
    }

    public void SetLit(bool lit)
    {
        isLit = lit;
    }

    public void SetVisionRadius(float radius)
    {
        visionRadius = Mathf.Max(0f, radius);
    }

    public void SetVisionIntensity(float intensity)
    {
        visionIntensity = Mathf.Clamp01(intensity);
    }
}
