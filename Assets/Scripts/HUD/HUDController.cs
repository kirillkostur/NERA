using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HUDController : MonoBehaviour
{
    [Header("Player References")]
    public StationBatterySystem batterySystem;

    [Header("UI Elements")]
    public Slider healthBar;      // Индикатор здоровья
    public Slider armorBar;       // Индикатор брони
    public Slider batteryBar;     // Индикатор батареи

    [Header("Text (TMP) Elements")]
    public TMP_Text batteryText;  // Текст % батареи
    public TMP_Text dayText;      // Текст дня

    [Header("Sun/Moon Icons")]
    public Image[] sunIcons;      // [0] — Луна (ночь), [1] — Солнце (день)

    private void Start()
    {
        if (batteryBar != null)
        {
            batteryBar.minValue = 0;
            batteryBar.maxValue = 1f; // нормализованное значение (0–1)
        }
    }

    private void Update()
    {
        // === Батарея ===
        if (batterySystem != null && batteryBar != null)
        {
            batteryBar.value = batterySystem.ChargePercent / 100f;

            if (batteryText != null)
                batteryText.text = $"{batterySystem.ChargePercent:F0}%";
        }

        // === День/ночь ===
        if (batterySystem != null && batterySystem.dayNight != null)
        {
            if (dayText != null)
                dayText.text = $"DAY {batterySystem.dayNight.currentDay}";

            bool isNight = batterySystem.dayNight.isNight;

            if (sunIcons != null && sunIcons.Length >= 2)
            {
                if (sunIcons[0] != null) sunIcons[0].enabled = isNight;     // Луна
                if (sunIcons[1] != null) sunIcons[1].enabled = !isNight;    // Солнце
            }
        }
    }
}
