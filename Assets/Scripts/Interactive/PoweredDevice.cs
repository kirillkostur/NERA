using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoweredDevice : MonoBehaviour, IPowerConsumer
{
    [Header("Питание")]
    public StationBatterySystem battery;
    public float consumptionPerSecond = 1f;

    [Header("Активация")]
    public bool requiresRepairToRun = false;
    public bool autoActivateOnPower = false;
    public bool onlyActivateAtNight = false;

    [Header("Поведение на улице")]
    public bool isOutdoor = false;
    public MeshRenderer dirtMesh;
    public float stormDirtyTime = 2f;

    [Header("Очистка")]
    public List<GameObject> repairAirEffects = new List<GameObject>();
    public float cleaningDuration = 2f;

    [Header("Что включать/выключать при работе")]
    public GameObject[] enableOnActive;

    private bool hasPower = false;
    private bool isActive = false;
    private bool manuallyEnabled = false;
    private bool initialized = false;

    private bool wasDirty = false;          // объект стал грязным в бурю
    private bool isCurrentlyDirty = false;  // состояние грязи сейчас
    private RepairableObject repairable;
    private DayNightCycle dayNight;
    private Material dirtMaterialInstance;
    private Coroutine dirtyRoutine;
    private Coroutine cleaningRoutine;

    private static readonly int DissolveId = Shader.PropertyToID("_DissolveStrength");

    private void Awake()
    {
        if (battery == null) battery = StationBatterySystem.Instance;

#if UNITY_2023_1_OR_NEWER
        dayNight = Object.FindFirstObjectByType<DayNightCycle>();
#else
        dayNight = Object.FindObjectOfType<DayNightCycle>();
#endif

        repairable = GetComponent<RepairableObject>();
        if (repairable != null)
            repairable.OnRepaired += OnDeviceRepaired;

        if (dirtMesh != null)
        {
            dirtMaterialInstance = Instantiate(dirtMesh.sharedMaterial);
            dirtMesh.material = dirtMaterialInstance;
            if (dirtMaterialInstance.HasProperty(DissolveId))
                dirtMaterialInstance.SetFloat(DissolveId, 0f);
        }
    }

    private void OnDestroy()
    {
        if (repairable != null)
            repairable.OnRepaired -= OnDeviceRepaired;
    }

    private void OnEnable()
    {
        if (battery == null) battery = StationBatterySystem.Instance;
        if (battery != null) battery.RegisterConsumer(this);

        SetActive(false);
        initialized = true;
    }

    private void OnDisable()
    {
        if (battery != null) battery.UnregisterConsumer(this);
        SetActive(false);
        StopCleaningFX();
    }

    private void Update()
    {
        // Ночной режим
        if (onlyActivateAtNight && dayNight != null && initialized)
        {
            if (!dayNight.isNight && isActive)
                SetActive(false);

            if (dayNight.isNight && hasPower)
                UpdateActiveState();
        }

        // Загрязнение во время бури — даже если уже сломан
        if (isOutdoor && SandStormController.StormActive && dirtyRoutine == null && !isCurrentlyDirty)
        {
            SetActive(false);
            BreakDevice();
            dirtyRoutine = StartCoroutine(DirtyOverTime(stormDirtyTime));
        }
    }

    private void OnDeviceRepaired(RepairableObject _)
    {
        // Во время бури ремонт не проходит, не ставим флаг "починен"
        if (SandStormController.StormActive)
        {
            Logger.Log("🚫 Ремонт невозможен во время бури!");
            if (repairable != null) repairable.BreakObject();
            return;
        }

        bool shouldPlayFX = wasDirty; // эффект только если объект был грязным
        wasDirty = false;

        // Нет питания — не запускаем
        if (battery == null || !battery.GameStarted || !battery.HasPower)
        {
            Logger.Log("⚠ Невозможно запустить — нет питания");
            BreakDevice();
            return;
        }

        // Включаем устройство (если удовлетворяет условиям)
        manuallyEnabled = true;
        UpdateActiveState();

        // Плавная очистка грязи (никаких мгновенных SetFloat(0) тут!)
        if (shouldPlayFX && dirtMaterialInstance != null && dirtMaterialInstance.HasProperty(DissolveId))
        {
            if (cleaningRoutine != null) StopCoroutine(cleaningRoutine);
            cleaningRoutine = StartCoroutine(CleaningRoutine());
        }
        else
        {
            // Если не было грязи — убедимся, что параметр чистый
            if (dirtMaterialInstance != null && dirtMaterialInstance.HasProperty(DissolveId))
                dirtMaterialInstance.SetFloat(DissolveId, 0f);
        }

        // После успешного ремонта устройство считается не-грязным
        isCurrentlyDirty = false;
    }

    public void OnPowerChanged(bool available)
    {
        hasPower = available;

        if (!initialized) return;

        if (!hasPower)
        {
            manuallyEnabled = false;
            SetActive(false);
            BreakDevice();
        }

        UpdateActiveState();
    }

    public float GetConsumptionPerSecond() => isActive ? consumptionPerSecond : 0f;
    public bool IsConsuming() => isActive;

    private void UpdateActiveState()
    {
        bool repairedOk = !requiresRepairToRun || (repairable != null && repairable.isRepaired && manuallyEnabled);
        bool shouldBeActive = hasPower && repairedOk;

        if (hasPower && autoActivateOnPower && !requiresRepairToRun)
            shouldBeActive = true;

        if (onlyActivateAtNight && dayNight != null)
            shouldBeActive = shouldBeActive && dayNight.isNight;

        SetActive(shouldBeActive);
    }

    private void SetActive(bool value)
    {
        if (isActive == value && initialized) return;

        isActive = value;

        foreach (var obj in enableOnActive)
            if (obj != null) obj.SetActive(isActive);
    }

    private void BreakDevice()
    {
        if (repairable != null && repairable.isRepaired)
        {
            repairable.BreakObject();
        }
        SetActive(false);
    }

    private IEnumerator DirtyOverTime(float duration)
    {
        isCurrentlyDirty = true;

        float elapsed = 0f;
        float start = 0f;
        if (dirtMaterialInstance != null && dirtMaterialInstance.HasProperty(DissolveId))
            start = dirtMaterialInstance.GetFloat(DissolveId);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            if (dirtMaterialInstance != null && dirtMaterialInstance.HasProperty(DissolveId))
                dirtMaterialInstance.SetFloat(DissolveId, Mathf.Lerp(start, 1f, t));

            yield return null;
        }

        wasDirty = true;
        BreakDevice();
        dirtyRoutine = null;
    }

    private IEnumerator CleaningRoutine()
    {
        // Не запускаем очистку, если внезапно началась буря
        if (SandStormController.StormActive)
            yield break;

        SetRepairEffectsActive(true);

        float elapsed = 0f;
        float start = 1f;
        if (dirtMaterialInstance != null && dirtMaterialInstance.HasProperty(DissolveId))
            start = dirtMaterialInstance.GetFloat(DissolveId);

        // Плавно от текущего значения к 0 за cleaningDuration
        while (elapsed < cleaningDuration)
        {
            // Если во время очистки началась буря — прекращаем, ничего не дотираем
            if (SandStormController.StormActive)
            {
                SetRepairEffectsActive(false);
                cleaningRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / cleaningDuration);

            if (dirtMaterialInstance != null && dirtMaterialInstance.HasProperty(DissolveId))
                dirtMaterialInstance.SetFloat(DissolveId, Mathf.Lerp(start, 0f, t));

            yield return null;
        }

        if (dirtMaterialInstance != null && dirtMaterialInstance.HasProperty(DissolveId))
            dirtMaterialInstance.SetFloat(DissolveId, 0f);

        SetRepairEffectsActive(false);
        cleaningRoutine = null;
    }

    private void SetRepairEffectsActive(bool active)
    {
        foreach (var effect in repairAirEffects)
            if (effect != null) effect.SetActive(active);
    }

    private void StopCleaningFX()
    {
        SetRepairEffectsActive(false);
        cleaningRoutine = null;
    }
}
