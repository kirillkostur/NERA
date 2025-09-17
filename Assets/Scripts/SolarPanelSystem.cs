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
    [Tooltip("Список партиклов, которые включаются после ремонта панели и выключаются по таймеру.")]
    public List<GameObject> repairAirEffects = new List<GameObject>();

    [Tooltip("Сколько секунд длится очистка после ремонта.")]
    public float cleaningDuration = 3f;

    private RepairableObject repairable;
    private Coroutine cleaningRoutine;

    private void Awake()
    {
        repairable = GetComponent<RepairableObject>();
        if (repairable != null)
            repairable.OnRepaired += HandleRepaired;
    }

    private void OnEnable()
    {
        TryRegister();
    }

    private void Start()
    {
        if (StationBatterySystem.Instance == null)
            StartCoroutine(RegisterWhenReady());
    }

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

    /// <summary>
    /// Возвращает текущую выработку панели.
    /// </summary>
    public float GetCurrentOutput()
    {
        if (repairable == null || !repairable.isRepaired) return 0f;
        if (dayNight == null || dayNight.isNight) return 0f;
        return chargePerSecondDay;
    }

    /// <summary>
    /// Запускает эффекты очистки после успешного ремонта.
    /// </summary>
    private void HandleRepaired(RepairableObject _)
    {
        if (cleaningRoutine != null)
        {
            StopCoroutine(cleaningRoutine);
            cleaningRoutine = null;
        }

        cleaningRoutine = StartCoroutine(CleaningRoutine());
    }

    private IEnumerator CleaningRoutine()
    {
        // Включаем эффекты
        foreach (var effect in repairAirEffects)
        {
            if (effect != null) effect.SetActive(true);
        }

        yield return new WaitForSeconds(cleaningDuration);

        // Выключаем эффекты
        foreach (var effect in repairAirEffects)
        {
            if (effect != null) effect.SetActive(false);
        }

        cleaningRoutine = null;
    }
}
