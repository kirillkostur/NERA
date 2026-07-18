using System;
using UnityEngine;

namespace NERA.Energy
{
    public sealed class StationEnvironmentController : MonoBehaviour
    {
        [SerializeField] private EnergyBalanceConfig config;
        [SerializeField, Range(0f, 24f)] private float currentHour = 12f;
        [SerializeField] private StationWeather weather = StationWeather.Clear;
        [SerializeField] private bool advanceTime = true;

        public static StationEnvironmentController Instance { get; private set; }

        public event Action EnvironmentChanged;

        public float CurrentHour => currentHour;
        public StationWeather Weather => weather;
        public bool IsDaytime =>
            currentHour >= Config.SunriseHour &&
            currentHour < Config.SunsetHour;
        public EnergyBalanceConfig Config =>
            config != null ? config : config = EnergyBalanceConfig.LoadDefault();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void Update()
        {
            if (!advanceTime)
                return;

            float hoursPerSecond = 24f / Config.FullDayDurationSeconds;
            currentHour = Mathf.Repeat(currentHour + hoursPerSecond * Time.deltaTime, 24f);
        }

        public void SetTime(float hour)
        {
            currentHour = Mathf.Repeat(hour, 24f);
            EnvironmentChanged?.Invoke();
        }

        public void SetWeather(StationWeather newWeather)
        {
            if (weather == newWeather)
                return;

            weather = newWeather;
            EnvironmentChanged?.Invoke();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
