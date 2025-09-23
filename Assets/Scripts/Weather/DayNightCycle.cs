using System;
using UnityEngine;

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

    public event Action OnNewDay;

    private void Update()
    {
        // Инкремент времени суток
        timeOfDay += Time.deltaTime / dayDuration;
        if (timeOfDay >= 1f)
        {
            timeOfDay = 0f;
            dayCounted = false;
        }

        // Угол солнца
        float sunAngle = timeOfDay * 360f - 90f;

        // Управление светом
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(sunAngle, 220f, 0);

            // Базовый цвет и интенсивность от дня/ночи
            Color baseColor = sunColor.Evaluate(timeOfDay);
            float baseIntensity = sunIntensity.Evaluate(timeOfDay);

            // Если буря активна днём — подмешиваем цвет и интенсивность
            if (SandStormController.Instance != null && SandStormController.StormActive && !isNight)
            {
                Color stormColor = SandStormController.Instance.GetStormColor();
                float stormIntensity = SandStormController.Instance.GetStormIntensity();
                float blend = SandStormController.Instance.StormBlend;

                sun.color = Color.Lerp(baseColor, stormColor, blend);
                sun.intensity = Mathf.Lerp(baseIntensity, stormIntensity, blend);
            }
            else
            {
                sun.color = baseColor;
                sun.intensity = baseIntensity;
            }
        }

        // Определение ночи
        bool nowNight = sunAngle > 180f || sunAngle < 0f;
        if (nowNight != isNight)
        {
            isNight = nowNight;
            Logger.Log(isNight ? "🌙 Наступила ночь" : "☀ Наступил день");
        }

        // Счётчик дней
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
    private void OnEnable()
    {
        GameEvents.RaiseQuestEvent("storm_started", 1);
        GameEvents.RaiseQuestEvent("storm_ended", 1);
    }

    private void OnDisable()
    {
        GameEvents.RaiseQuestEvent("storm_started", 1);
        GameEvents.RaiseQuestEvent("storm_ended", 1);
    }
    private void OnStormStarted()
    {
        Debug.Log("🌪 [DayNightCycle] Буря началась — событие получено");
    }

    private void OnStormEnded()
    {
        Debug.Log("🌤 [DayNightCycle] Буря закончилась — событие получено");
    }


}
