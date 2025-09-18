using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RepairableObject))]
public class SolarPanelSystem : MonoBehaviour
{
    [Header("Производство")]
    public float chargePerSecondDay = 4f;

    [Header("Ссылки")]
    public DayNightCycle dayNight;

    [Header("Эффекты очистки")]
    public List<GameObject> repairAirEffects = new List<GameObject>();

    [Tooltip("Время очистки панели (сек).")]
    public float cleaningDuration = 3f;

    [Header("Материалы загрязнения")]
    [Tooltip("Меши панели. Каждому создаётся копия материала с параметром _DissolveStrength.")]
    public List<MeshRenderer> dirtMeshes = new List<MeshRenderer>();

    private static readonly int DissolveId = Shader.PropertyToID("_DissolveStrength");
    private readonly List<Material> dirtMaterials = new List<Material>();
    private RepairableObject repairable;
    private Coroutine cleaningRoutine;
    private Coroutine dirtyRoutine;

    private void Awake()
    {
        repairable = GetComponent<RepairableObject>();
        if (repairable != null)
            repairable.OnRepaired += HandleRepaired;

        // Создаём копии материалов
        dirtMaterials.Clear();
        foreach (var mr in dirtMeshes)
        {
            if (mr == null) continue;
            var instanced = Instantiate(mr.sharedMaterial);
            mr.material = instanced;
            dirtMaterials.Add(instanced);
        }
    }

    private void Start()
    {
        // ✅ Устанавливаем правильное состояние материалов при старте
        SetInitialMaterialState();

        if (StationBatterySystem.Instance == null)
            StartCoroutine(RegisterWhenReady());
    }

    private void OnEnable() => TryRegister();

    private void OnDisable()
    {
        if (StationBatterySystem.Instance != null)
            StationBatterySystem.Instance.UnregisterPanel(this);
    }

    private void OnDestroy()
    {
        if (repairable != null)
            repairable.OnRepaired -= HandleRepaired;
    }

    private void TryRegister()
    {
        if (StationBatterySystem.Instance != null)
            StationBatterySystem.Instance.RegisterPanel(this);
    }

    private IEnumerator RegisterWhenReady()
    {
        while (StationBatterySystem.Instance == null) yield return null;
        StationBatterySystem.Instance.RegisterPanel(this);
    }

    private void SetInitialMaterialState()
    {
        float dissolveValue = (repairable != null && !repairable.isRepaired) ? 1f : 0f; // грязно, если не починена
        foreach (var mat in dirtMaterials)
        {
            if (mat != null && mat.HasProperty(DissolveId))
                mat.SetFloat(DissolveId, dissolveValue);
        }
    }

    public float GetCurrentOutput()
    {
        if (repairable == null || !repairable.isRepaired) return 0f;
        if (dayNight == null || dayNight.isNight) return 0f;
        return chargePerSecondDay;
    }

    private bool IsFullyDirty()
    {
        if (dirtMaterials.Count == 0) return false;
        foreach (var mat in dirtMaterials)
        {
            if (mat == null) continue;
            float v = mat.HasProperty(DissolveId) ? mat.GetFloat(DissolveId) : 0f;
            if (v < 0.999f) return false;
        }
        return true;
    }

    public IEnumerator DirtyOverTime(float duration)
    {
        if (IsFullyDirty()) yield break;

        var starts = new float[dirtMaterials.Count];
        for (int i = 0; i < dirtMaterials.Count; i++)
        {
            var mat = dirtMaterials[i];
            starts[i] = (mat != null && mat.HasProperty(DissolveId)) ? mat.GetFloat(DissolveId) : 0f;
        }

        if (cleaningRoutine != null)
        {
            StopCoroutine(cleaningRoutine);
            cleaningRoutine = null;
        }
        if (dirtyRoutine != null) StopCoroutine(dirtyRoutine);
        dirtyRoutine = StartCoroutine(DirtyLerpRoutine(duration, starts));
        yield return dirtyRoutine;
        dirtyRoutine = null;

        if (repairable != null) repairable.BreakObject();
    }

    private IEnumerator DirtyLerpRoutine(float duration, float[] starts)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < dirtMaterials.Count; i++)
            {
                var mat = dirtMaterials[i];
                if (mat == null || !mat.HasProperty(DissolveId)) continue;

                float from = Mathf.Clamp01(starts[i]);
                float v = Mathf.Lerp(from, 1f, t);
                mat.SetFloat(DissolveId, v);
            }
            yield return null;
        }
        foreach (var mat in dirtMaterials)
            if (mat != null && mat.HasProperty(DissolveId))
                mat.SetFloat(DissolveId, 1f);
    }

    private void HandleRepaired(RepairableObject _)
    {
        if (dirtyRoutine != null)
        {
            StopCoroutine(dirtyRoutine);
            dirtyRoutine = null;
        }
        if (cleaningRoutine != null) StopCoroutine(cleaningRoutine);
        cleaningRoutine = StartCoroutine(CleaningRoutine());
    }

    private IEnumerator CleaningRoutine()
    {
        foreach (var effect in repairAirEffects)
            if (effect != null) effect.SetActive(true);

        var starts = new float[dirtMaterials.Count];
        for (int i = 0; i < dirtMaterials.Count; i++)
        {
            var mat = dirtMaterials[i];
            starts[i] = (mat != null && mat.HasProperty(DissolveId)) ? mat.GetFloat(DissolveId) : 1f;
        }

        float elapsed = 0f;
        while (elapsed < cleaningDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cleaningDuration);
            for (int i = 0; i < dirtMaterials.Count; i++)
            {
                var mat = dirtMaterials[i];
                if (mat == null || !mat.HasProperty(DissolveId)) continue;

                float from = Mathf.Clamp01(starts[i]);
                float v = Mathf.Lerp(from, 0f, t);
                mat.SetFloat(DissolveId, v);
            }
            yield return null;
        }
        foreach (var mat in dirtMaterials)
            if (mat != null && mat.HasProperty(DissolveId))
                mat.SetFloat(DissolveId, 0f);

        foreach (var effect in repairAirEffects)
            if (effect != null) effect.SetActive(false);

        cleaningRoutine = null;
    }
}
