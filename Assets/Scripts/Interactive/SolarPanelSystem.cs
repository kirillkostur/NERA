using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(RepairableObject))]
[RequireComponent(typeof(Identifiable))]
public class SolarPanelSystem : MonoBehaviour
{
    [Header("Производство")]
    public float chargePerSecondDay = 4f;

    [Header("Ссылки")]
    public DayNightCycle dayNight;

    [Header("Эффекты очистки")]
    public List<GameObject> repairAirEffects = new List<GameObject>();
    public float cleaningDuration = 3f;

    [Header("Материалы загрязнения")]
    public List<MeshRenderer> dirtMeshes = new List<MeshRenderer>();

    private static readonly int DissolveId = Shader.PropertyToID("_DissolveStrength");
    private readonly List<Material> dirtMaterials = new List<Material>();
    private RepairableObject repairable;
    private Coroutine cleaningRoutine;
    private Coroutine dirtyRoutine;
    private AlertManager alerts;

    private void Awake()
    {
        repairable = GetComponent<RepairableObject>();
        if (repairable != null)
            repairable.OnRepaired += HandleRepaired;

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
        SetInitialMaterialState();
        alerts = FindFirstObjectByType<AlertManager>();
    }

    private void OnEnable()
    {
        StartCoroutine(RegisterWhenReady());
    }

    private void OnDisable()
    {
        if (StationBatterySystem.Instance != null)
            StationBatterySystem.Instance.UnregisterPanel(this);

        // На всякий случай выключаем VFX при выключении объекта
        SafeStopCleaningFX();
    }

    private void OnDestroy()
    {
        if (repairable != null)
            repairable.OnRepaired -= HandleRepaired;

        // На всякий случай выключаем VFX при уничтожении
        SafeStopCleaningFX();
    }

    private void Update()
    {
        // Если во время очистки началась буря — немедленно прерываем очистку и гасим VFX,
        // чтобы не зависали «воздушные струи».
        if (SandStormController.StormActive && cleaningRoutine != null)
        {
            StopCoroutine(cleaningRoutine);
            cleaningRoutine = null;
            SafeStopCleaningFX();
        }
    }

    private IEnumerator RegisterWhenReady()
    {
        while (StationBatterySystem.Instance == null)
            yield return null;

        StationBatterySystem.Instance.RegisterPanel(this);
    }

    private void SetInitialMaterialState()
    {
        float dissolveValue = (repairable != null && !repairable.isRepaired) ? 1f : 0f;
        foreach (var mat in dirtMaterials)
            if (mat != null && mat.HasProperty(DissolveId))
                mat.SetFloat(DissolveId, dissolveValue);
    }

    /// <summary>
    /// Возвращает текущую мощность панели.
    /// Во время бури зарядка сразу прекращается.
    /// </summary>
    public float GetCurrentOutput()
    {
        if (SandStormController.StormActive) return 0f;
        if (repairable == null || !repairable.isRepaired) return 0f;
        if (dayNight == null || dayNight.isNight) return 0f;
        return chargePerSecondDay;
    }

    private bool IsFullyDirty()
    {
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

        // Если шла очистка — останавливаем и ГАСИМ VFX немедленно,
        // чтобы не зависали эффекты при старте бури.
        if (cleaningRoutine != null)
        {
            StopCoroutine(cleaningRoutine);
            cleaningRoutine = null;
            SafeStopCleaningFX();
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
        if (SandStormController.StormActive)
        {
            Logger.Log("🚫 Очистка невозможна во время бури!");
            return;
        }

        if (StationBatterySystem.Instance != null)
            StationBatterySystem.Instance.RegisterPanel(this);

        if (dirtyRoutine != null)
        {
            StopCoroutine(dirtyRoutine);
            dirtyRoutine = null;
        }

        if (cleaningRoutine != null) StopCoroutine(cleaningRoutine);
        cleaningRoutine = StartCoroutine(CleaningRoutine());

        // 🔔 Квестовое событие: панель почищена
        var ident = GetComponent<Identifiable>();
        GameEvents.RaiseQuestEvent(new QuestEventData(
            QuestEventType.CleanSolarPanel,
            ident.Id,
            1
        ));
    }

    private IEnumerator CleaningRoutine()
    {
        SetRepairEffectsActive(true);

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

        SetRepairEffectsActive(false);
        cleaningRoutine = null;
    }

    // =======================
    // ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ
    // =======================

    private void SetRepairEffectsActive(bool active)
    {
        foreach (var effect in repairAirEffects)
            if (effect != null) effect.SetActive(active);
    }

    private void SafeStopCleaningFX()
    {
        // Гасим VFX и сбрасываем ссылку на корутину очистки (если она была)
        SetRepairEffectsActive(false);
    }
}
