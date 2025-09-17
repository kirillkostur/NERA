using UnityEngine;

/// <summary>
/// Питаемый объект. Управляет своим состоянием и активируемыми объектами.
/// </summary>
public class PoweredDevice : MonoBehaviour, IPowerConsumer
{
    [Header("Питание")]
    public StationBatterySystem battery;
    public float consumptionPerSecond = 1f;

    [Header("Активация")]
    public bool requiresRepairToRun = false;  // Требует ручного запуска после ремонта
    public bool autoActivateOnPower = false;  // Включается автоматически при появлении питания

    [Header("Что включать/выключать при работе")]
    public GameObject[] enableOnActive;

    private bool hasPower = false;
    private bool isActive = false;
    private bool manuallyEnabled = false;
    private bool initialized = false;

    private RepairableObject repairable;

    private void Awake()
    {
        if (battery == null) battery = StationBatterySystem.Instance;
        repairable = GetComponent<RepairableObject>();

        if (repairable != null)
        {
            repairable.OnRepaired += OnDeviceRepaired;
        }
    }

    private void OnDestroy()
    {
        if (repairable != null)
        {
            repairable.OnRepaired -= OnDeviceRepaired;
        }
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

    /// <summary>
    /// Обработчик успешного ремонта.
    /// </summary>
    private void OnDeviceRepaired(RepairableObject _)
    {
        // Проверяем, запущен ли аккумулятор
        if (battery == null || !battery.GameStarted || !battery.HasPower)
        {
            // Нельзя починить — откатываем ремонт
            Debug.Log("⚠ Нет активного аккумулятора или питания — ремонт невозможен.");
            if (repairable != null) repairable.BreakObject();
            manuallyEnabled = false;
            SetActive(false);
            return;
        }

        // Всё в порядке — включаем устройство
        manuallyEnabled = true;
        UpdateActiveState();
    }

    /// <summary>
    /// Вызывается аккумулятором при изменении состояния питания.
    /// </summary>
    public void OnPowerChanged(bool available)
    {
        hasPower = available;

        if (!initialized) return;

        if (!hasPower)
        {
            manuallyEnabled = false;
            SetActive(false);

            if (requiresRepairToRun && repairable != null && repairable.isRepaired)
            {
                // Ломаем устройство при потере питания
                repairable.BreakObject();
            }
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

        SetActive(shouldBeActive);
    }

    /// <summary>
    /// Включает или выключает объекты из массива enableOnActive.
    /// </summary>
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
