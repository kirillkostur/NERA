using System.Collections;
using UnityEngine;

/// <summary>
/// Солнечная панель: даёт output только днём и когда панель починена.
/// Надёжно регистрируется в аккумуляторе, даже если порядок инициализации сцен разный.
/// </summary>
[RequireComponent(typeof(RepairableObject))]
public class SolarPanelSystem : MonoBehaviour
{
    [Header("Производство")]
    public float chargePerSecondDay = 4f;

    [Header("Ссылки")]
    public DayNightCycle dayNight;

    private RepairableObject repairable;

    private void Awake()
    {
        repairable = GetComponent<RepairableObject>();
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

    public float GetCurrentOutput()
    {
        if (repairable == null || !repairable.isRepaired) return 0f;
        if (dayNight == null || dayNight.isNight) return 0f;
        return chargePerSecondDay;
    }
}
