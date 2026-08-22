using System;
using NERA.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NERA.Energy
{
    /// <summary>
    /// Backward-compatible station clock facade. Weather scheduling and
    /// rendering are owned by StationWeatherController and both systems use
    /// the centralized StationEnvironmentConfig.
    /// </summary>
    [DefaultExecutionOrder(-190)]
    [DisallowMultipleComponent]
    public sealed class StationEnvironmentController : MonoBehaviour
    {
        public const string PlayerStationSceneName = "Player_Station";

        [SerializeField, Range(0f, 24f)] private float currentHour = 12f;
        [SerializeField] private bool advanceTime = true;
        [SerializeField] private StationWeatherController weatherController;

        private StationWeatherController subscribedWeather;

        public static StationEnvironmentController Instance { get; private set; }

        public event Action EnvironmentChanged;

        public float CurrentHour => currentHour;
        public StationWeather Weather => ResolveWeatherController(false) != null
            ? weatherController.Weather
            : StationWeather.Clear;
        public bool IsDaytime
        {
            get
            {
                float sunrise = Config.SunriseHour;
                float sunset = Config.SunsetHour;
                return sunrise <= sunset
                    ? currentHour >= sunrise && currentHour < sunset
                    : currentHour >= sunrise || currentHour < sunset;
            }
        }
        public StationEnvironmentConfig Config =>
            ResolveWeatherController(false) != null
                ? weatherController.Config
                : StationEnvironmentConfig.LoadDefault();
        public StationWeatherController WeatherController =>
            ResolveWeatherController(false);
        public static bool IsPlayerStationSceneActive =>
            IsPlayerStationScene(SceneManager.GetActiveScene().name);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            ResolveWeatherController(true);
            BindWeatherController();
        }

        private void OnEnable()
        {
            BindWeatherController();
        }

        private void Update()
        {
            if (!advanceTime)
                return;

            float hoursPerSecond = 24f / Config.FullDayDurationSeconds;
            currentHour = Mathf.Repeat(
                currentHour + hoursPerSecond * Time.deltaTime,
                24f);
        }

        public static bool IsPlayerStationScene(string sceneName)
        {
            return string.Equals(
                sceneName,
                PlayerStationSceneName,
                StringComparison.Ordinal);
        }

        public void SetTime(float hour)
        {
            currentHour = Mathf.Repeat(hour, 24f);
            EnvironmentChanged?.Invoke();
        }

        public void SetWeather(StationWeather newWeather)
        {
            StationWeatherController controller =
                ResolveWeatherController(true);
            BindWeatherController();
            controller?.SetWeather(newWeather);
        }

        private StationWeatherController ResolveWeatherController(
            bool createIfMissing)
        {
            if (weatherController != null)
                return weatherController;

            weatherController = GetComponent<StationWeatherController>();
            if (weatherController == null && createIfMissing)
                weatherController = gameObject.AddComponent<StationWeatherController>();
            return weatherController;
        }

        private void BindWeatherController()
        {
            StationWeatherController resolved =
                ResolveWeatherController(false);
            if (subscribedWeather == resolved)
                return;

            if (subscribedWeather != null)
                subscribedWeather.WeatherChanged -= HandleWeatherChanged;

            subscribedWeather = resolved;
            if (subscribedWeather != null)
                subscribedWeather.WeatherChanged += HandleWeatherChanged;
        }

        private void HandleWeatherChanged(StationWeather _)
        {
            EnvironmentChanged?.Invoke();
        }

        private void OnValidate()
        {
            currentHour = Mathf.Repeat(currentHour, 24f);
            ResolveWeatherController(false);
        }

        private void OnDisable()
        {
            if (subscribedWeather != null)
                subscribedWeather.WeatherChanged -= HandleWeatherChanged;
            subscribedWeather = null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
