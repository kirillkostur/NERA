using UnityEngine;

public class PoweredDevice : MonoBehaviour, IPowerConsumer
{
    [Header("Питание")]
    public StationBatterySystem battery;
    public float consumptionPerSecond = 1f;

    [Header("Активация")]
    public bool requiresRepairToRun = false;     // Требует ремонта перед работой
    public bool autoActivateOnPower = false;     // Авто активация при питании
    public bool onlyActivateAtNight = false;     // ✅ Работает только ночью

    [Header("Что включать/выключать при работе")]
    public GameObject[] enableOnActive;

    private bool hasPower = false;
    private bool isActive = false;
    private bool manuallyEnabled = false;
    private bool initialized = false;

    private RepairableObject repairable;
    private DayNightCycle dayNight;

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
    }

    private void Update()
    {
        // ✅ Проверка смены дня/ночи, чтобы сразу выключить свет на рассвете
        if (onlyActivateAtNight && dayNight != null && initialized)
        {
            // Если день настал, а свет ещё горит — выключаем
            if (!dayNight.isNight && isActive)
                SetActive(false);

            // Если ночь и условия выполнены — обновляем состояние
            if (dayNight.isNight && hasPower)
                UpdateActiveState();
        }
    }

    private void OnDeviceRepaired(RepairableObject _)
    {
        if (battery == null || !battery.GameStarted || !battery.HasPower)
        {
            Logger.Log("⚠ Невозможно починить — аккумулятор не активен или без питания");
            if (repairable != null) repairable.BreakObject();
            manuallyEnabled = false;
            SetActive(false);
            return;
        }

        manuallyEnabled = true;
        UpdateActiveState();
    }

    public void OnPowerChanged(bool available)
    {
        hasPower = available;

        if (!initialized) return;

        if (!hasPower)
        {
            manuallyEnabled = false;
            SetActive(false);

            if (requiresRepairToRun && repairable != null && repairable.isRepaired)
                repairable.BreakObject();
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

        // ✅ Проверяем ночь
        if (onlyActivateAtNight && dayNight != null)
            shouldBeActive = shouldBeActive && dayNight.isNight;

        SetActive(shouldBeActive);
    }

    private void SetActive(bool value)
    {
        if (isActive == value && initialized) return;

        isActive = value;

        foreach (var obj in enableOnActive)
        {
            if (obj != null) obj.SetActive(isActive);
        }
    }
}
