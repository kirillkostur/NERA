using System;
using UnityEngine;

/// <summary>
/// Цикл дня/ночи. Дни начинают считаться только после первого старта аккумулятора:
/// StationBatterySystem.Instance.GameStarted == true.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    [Header("Настройки дня/ночи")]
    [Tooltip("Directional Light солнца")]
    public Light sun;
    [Tooltip("Длительность полного дня в секундах (от рассвета до следующего рассвета)")]
    public float dayDuration = 120f;
    [Tooltip("Плавный переход цвета солнца в течение дня")]
    public Gradient sunColor;
    [Tooltip("Кривая интенсивности света (день -> ночь)")]
    public AnimationCurve sunIntensity;

    [Header("Состояние")]
    [Tooltip("Текущий номер дня")]
    public int currentDay = 0;
    [Tooltip("Время суток от 0 до 1 (0 — рассвет, 0.5 — полдень, 1 — снова рассвет)")]
    public float timeOfDay = 0f;
    [Tooltip("Сейчас ночь?")]
    public bool isNight = false;

    private bool dayCounted = false;

    /// <summary>Событие наступления нового дня.</summary>
    public event Action OnNewDay;

    private void Update()
    {
        // Инкремент времени
        timeOfDay += Time.deltaTime / dayDuration;
        if (timeOfDay >= 1f)
        {
            timeOfDay = 0f;
            dayCounted = false;
        }

        // Угол солнца
        float sunAngle = timeOfDay * 360f - 90f;

        // Поворот/цвет/интенсивность
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(sunAngle, 220f, 0);
            sun.color = sunColor.Evaluate(timeOfDay);
            sun.intensity = sunIntensity.Evaluate(timeOfDay);
        }

        // День/ночь
        bool nowNight = sunAngle > 180f || sunAngle < 0f;
        if (nowNight != isNight)
        {
            isNight = nowNight;
            Logger.Log(isNight ? "🌙 Наступила ночь" : "☀ Наступил день");
        }

        // Считаем новый день один раз за цикл, только если аккумулятор стартовал игру
        var battery = StationBatterySystem.Instance;
        bool gameStarted = (battery != null && battery.GameStarted);

        if (!dayCounted && !nowNight && sunAngle > 0f && gameStarted)
        {
            currentDay++;
            dayCounted = true;
            Logger.Log($"📆 Наступил {currentDay}-й день");
            OnNewDay?.Invoke();
        }
    }
}
