using System;
using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [Header("Настройки дня/ночи")]
    public Light sun;                       // Дирекшн-лайт солнца
    public float dayDuration = 120f;        // Длительность дня (секунды = один оборот солнца)
    public Gradient sunColor;               // Плавный переход цвета солнца
    public AnimationCurve sunIntensity;     // Кривая интенсивности (день -> ночь)

    [Header("Отладка")]
    public int currentDay = 0;
    public float timeOfDay = 0f;            // От 0 до 1 (0 — рассвет, 0.5 — полдень, 1 — снова рассвет)
    public bool isNight = false;
    private bool dayCounted = false;

    [Header("Ссылки")]
    public GeneratorSystem _generator;

    // 🔔 Событие, уведомляющее о наступлении нового дня
    public event Action OnNewDay;

    void Update()
    {
        // Инкремент времени суток
        timeOfDay += Time.deltaTime / dayDuration;

        if (timeOfDay >= 1f)
        {
            timeOfDay = 0f;
            dayCounted = false;
        }

        // Позиция солнца (0–360 градусов)
        float sunAngle = timeOfDay * 360f - 90f; // -90, чтобы старт был на рассвете
        if (sun != null)
            sun.transform.rotation = Quaternion.Euler(sunAngle, 220f, 0);

        // Цвет и интенсивность
        if (sun != null)
        {
            sun.color = sunColor.Evaluate(timeOfDay);
            sun.intensity = sunIntensity.Evaluate(timeOfDay);
        }

        // Определение ночи
        bool nowNight = sunAngle > 180f || sunAngle < 0f;

        if (nowNight && !isNight)
        {
            isNight = true;
            Debug.Log("🌙 Наступила ночь");
        }
        else if (!nowNight && isNight)
        {
            isNight = false;
            Debug.Log("☀ Наступил день");
        }

        // Счётчик дней (инкремент один раз за цикл дня, когда солнце уже над горизонтом)
        if (!dayCounted && !nowNight && sunAngle > 0f)
        {
            if (_generator != null && _generator.gameStarted)
            {
                currentDay++;
                dayCounted = true;
                Debug.Log($"📆 Наступил {currentDay}-й день");
                OnNewDay?.Invoke();
                Debug.Log("📢 OnNewDay событие отправлено");
            }
        }
    }
}
