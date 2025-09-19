using System.Collections.Generic;
using UnityEngine;

public class WeatherManager : MonoBehaviour
{
    public static WeatherManager Instance { get; private set; }

    [Header("Начальный погодный эффект (опционально)")]
    [SerializeField] private MonoBehaviour initialEffect; // Скрипт с IWeatherEffect

    private IWeatherEffect currentEffect;
    private readonly List<IWeatherEffect> allEffects = new List<IWeatherEffect>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

#if UNITY_2023_1_OR_NEWER
        foreach (var effect in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
#else
        foreach (var effect in FindObjectsOfType<MonoBehaviour>())
#endif
        {
            if (effect is IWeatherEffect we)
                allEffects.Add(we);
        }

        if (initialEffect != null && initialEffect is IWeatherEffect startEffect)
            SetWeather(startEffect);
    }

    private void Update()
    {
        if (currentEffect != null)
            currentEffect.UpdateEffect();
    }

    public void SetWeather(IWeatherEffect newEffect)
    {
        if (currentEffect == newEffect) return;

        if (currentEffect != null)
            currentEffect.StopEffect();

        currentEffect = newEffect;

        if (currentEffect != null)
            currentEffect.StartEffect();
    }
}
