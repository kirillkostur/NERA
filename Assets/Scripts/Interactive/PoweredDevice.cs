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

    [Header("Очистка")]
    public List<GameObject> repairAirEffects = new List<GameObject>();
    public float cleaningDuration = 2f;

    [Header("Что включать/выключать при работе")]
    public GameObject[] enableOnActive;

    [Header("Особые режимы")]
    public bool isLifeSupport = false;

    private bool gridHasPower = false;   // состояние «сети» от StationBatterySystem (HasPower)
    private bool isActive = false;
    private bool manuallyEnabled = false;
    private bool initialized = false;

    private bool wasDirty = false;
    private bool isCurrentlyDirty = false;
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
        if (battery != null)
        {
            battery.RegisterConsumer(this);
            battery.OnChargeChanged += OnBatteryChargeChanged; // слушаем 0% для LifeSupport
            OnPowerChanged(battery.HasPower);
        }

        SetActive(false);
        initialized = true;
    }

    private void OnDisable()
    {
        if (battery != null)
        {
            battery.UnregisterConsumer(this);
            battery.OnChargeChanged -= OnBatteryChargeChanged;
        }
        SetActive(false);
        StopCleaningFX();
    }

    private void Update()
    {
        if (onlyActivateAtNight && dayNight != null && initialized)
        {
            if (!dayNight.isNight && isActive)
                SetActive(false);

            if (dayNight.isNight)
                UpdateActiveState();
        }

        // Загрязнение/поломка бурей только для уличных объектов
        if (isOutdoor)
        {
            if (SandStormController.StormActive && dirtyRoutine == null && !isCurrentlyDirty)
            {
                float stormDuration = SandStormController.Instance != null
                    ? SandStormController.Instance.stormDuration
                    : 5f;
                dirtyRoutine = StartCoroutine(DirtyOverTime(stormDuration));
            }
        }
    }

    // === IPowerConsumer ===
    public void OnPowerChanged(bool availableFromGrid)
    {
        gridHasPower = availableFromGrid;

        // Обычные устройства ломаются при падении сети (<30%)
        if (!isLifeSupport && !gridHasPower)
        {
            BreakDevice();
        }

        if (!initialized) return;
        UpdateActiveState();
    }

    public float GetConsumptionPerSecond() => isActive ? consumptionPerSecond : 0f;
    public bool IsConsuming() => isActive;

    private void OnBatteryChargeChanged(float percent)
    {
        // LifeSupport выключается и «ломается» при 0%
        if (isLifeSupport && percent <= 0f)
        {
            BreakDevice();
            SetActive(false);
        }
        UpdateActiveState();
    }

    private void OnDeviceRepaired(RepairableObject _)
    {
        // ✅ ВАЖНОЕ ИЗМЕНЕНИЕ:
        // Буря блокирует ремонт ТОЛЬКО для уличных объектов. Внутренние (isOutdoor == false) игнорируют бурю.
        if (isOutdoor && SandStormController.StormActive)
        {
            Logger.Log("🚫 Ремонт невозможен во время бури (объект на улице)!");
            BreakDevice();
            if (repairable != null) repairable.ShowHighlight();
            return;
        }

        // LifeSupport нельзя чинить при полностью разряженной батарее
        if (isLifeSupport && battery != null && battery.ChargePercent <= 0f)
        {
            Logger.Log("🚫 Батарея разряжена до 0% — ремонт жизненно важного устройства невозможен.");
            BreakDevice();
            if (repairable != null) repairable.ShowHighlight();
            return;
        }

        // Обычные устройства чинятся только при реально поданном питании
        if (!isLifeSupport && (battery == null || !battery.GameStarted || !battery.HasPower))
        {
            Logger.Log("⚠ Сначала запустите батарею, чтобы починить устройство.");
            BreakDevice();
            if (repairable != null) repairable.ShowHighlight();
            return;
        }

        bool shouldPlayFX = wasDirty;
        wasDirty = false;

        manuallyEnabled = true; // игрок «включил» устройство ремонтом
        UpdateActiveState();

        if (shouldPlayFX && dirtMaterialInstance != null && dirtMaterialInstance.HasProperty(DissolveId))
        {
            if (cleaningRoutine != null) StopCoroutine(cleaningRoutine);
            cleaningRoutine = StartCoroutine(CleaningRoutine());
        }
        else
        {
            if (dirtMaterialInstance != null && dirtMaterialInstance.HasProperty(DissolveId))
                dirtMaterialInstance.SetFloat(DissolveId, 0f);
        }

        isCurrentlyDirty = false;
    }

    private void UpdateActiveState()
    {
        // Источник энергии:
        // - обычные устройства: только «сеть» (HasPower)
        // - LifeSupport: любой заряд > 0%
        bool energyAvailable = isLifeSupport
            ? (battery != null && battery.ChargePercent > 0f)
            : gridHasPower;

        // Требование ремонта:
        bool repairedOk = !requiresRepairToRun || (repairable != null && repairable.isRepaired && manuallyEnabled);

        bool shouldBeActive = energyAvailable && repairedOk;

        if (energyAvailable && autoActivateOnPower && !requiresRepairToRun)
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
        manuallyEnabled = false;
        SetActive(false);
    }

    private IEnumerator DirtyOverTime(float duration)
    {
        isCurrentlyDirty = true;

        float elapsed = 0f;
        float start = dirtMaterialInstance != null && dirtMaterialInstance.HasProperty(DissolveId)
            ? dirtMaterialInstance.GetFloat(DissolveId) : 0f;

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
        if (SandStormController.StormActive) yield break;

        SetRepairEffectsActive(true);

        float elapsed = 0f;
        float start = dirtMaterialInstance != null && dirtMaterialInstance.HasProperty(DissolveId)
            ? dirtMaterialInstance.GetFloat(DissolveId) : 1f;

        while (elapsed < cleaningDuration)
        {
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
