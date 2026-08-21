using System;
using NERA.Energy;
using NERA.Quests;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NERA.World
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class StationWeatherController : MonoBehaviour
    {
        private const float MinimumStormDuration = 0.1f;

        [SerializeField] private StationEnvironmentConfig config;
        [SerializeField] private StationWeather initialWeather =
            StationWeather.Clear;
        [SerializeField] private bool runAutomaticWeather = true;
        [SerializeField] private bool controlSandstormRendering = true;

        private StationWeather weather;
        private float activeSandstormDuration;
        private float sandstormElapsed;
        private float secondsUntilNextRoll;
        private string activeWeatherCause = string.Empty;
        private bool fogTransitionActive;
        private bool disableRendererFeatureAfterFogTransition;
        private float fogTransitionElapsed;
        private float fogTransitionDuration;
        private float fogTransitionStartDensity;
        private float fogTransitionTargetDensity;

        public static StationWeatherController Instance { get; private set; }
        public static event Action<float> AnySandstormStarted;
        public static event Action<bool> AnySandstormEnded;

        public event Action<StationWeather> WeatherChanged;
        public event Action<float> SandstormStarted;
        public event Action<bool> SandstormEnded;

        public StationEnvironmentConfig Config =>
            config != null
                ? config
                : config = StationEnvironmentConfig.LoadDefault();
        public StationWeather Weather => weather;
        public bool IsSandstormActive => weather == StationWeather.Sandstorm;
        public float ActiveSandstormDuration => activeSandstormDuration;
        public float SandstormElapsed => sandstormElapsed;
        public float SandstormRemainingSeconds => IsSandstormActive
            ? Mathf.Max(0f, activeSandstormDuration - sandstormElapsed)
            : 0f;
        public float SandstormProgress01 => IsSandstormActive
            ? Mathf.Clamp01(
                sandstormElapsed /
                Mathf.Max(MinimumStormDuration, activeSandstormDuration))
            : 0f;
        public float SecondsUntilNextAutomaticRoll =>
            Mathf.Max(0f, secondsUntilNextRoll);
        public bool IsFogTransitionActive => fogTransitionActive;
        public float CurrentFogDensity =>
            TryGetFogMaterial(out Material material, out string propertyName)
                ? material.GetFloat(propertyName)
                : Config.ClearFogDensity;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            weather = initialWeather;
            if (IsSandstormActive)
                activeSandstormDuration = Config.GetRandomSandstormDuration();
            ScheduleNextAutomaticRoll();
            ApplyRenderingState();
        }

        private void OnEnable()
        {
            if (Instance == this)
                ApplyRenderingState();
        }

        private void Start()
        {
            SynchronizeQuestWeather();
            if (IsSandstormActive)
                NotifySandstormStarted();
        }

        private void Update()
        {
            AdvanceSimulation(Time.deltaTime);
        }

        public void Configure(StationEnvironmentConfig environmentConfig)
        {
            config = environmentConfig;
            ScheduleNextAutomaticRoll();
            ApplyRenderingState();
        }

        public void SetAutomaticWeatherEnabled(bool enabled)
        {
            runAutomaticWeather = enabled;
            if (enabled)
                ScheduleNextAutomaticRoll();
        }

        public void AdvanceSimulation(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            AdvanceFogTransition(deltaTime);

            if (IsSandstormActive)
            {
                sandstormElapsed = Mathf.Min(
                    activeSandstormDuration,
                    sandstormElapsed + deltaTime);
                if (sandstormElapsed >= activeSandstormDuration)
                    EndSandstorm(true, "duration");
                return;
            }

            if (!runAutomaticWeather ||
                !Config.AutomaticSandstormsEnabled ||
                weather != StationWeather.Clear)
            {
                return;
            }

            secondsUntilNextRoll -= deltaTime;
            if (secondsUntilNextRoll > 0f)
                return;

            if (UnityEngine.Random.value <= Config.SandstormChancePerRoll)
            {
                BeginSandstorm(
                    Config.GetRandomSandstormDuration(),
                    "random");
            }
            else
            {
                ScheduleNextAutomaticRoll();
            }
        }

        public bool StartSandstorm()
        {
            return BeginSandstorm(
                Config.GetRandomSandstormDuration(),
                "manual");
        }

        public bool StartSandstorm(float durationSeconds)
        {
            return BeginSandstorm(durationSeconds, "manual");
        }

        public bool StartSandstormFromQuest(
            string questId,
            float minimumDurationSeconds = 0f,
            float maximumDurationSeconds = 0f)
        {
            float duration = ResolveQuestDuration(
                minimumDurationSeconds,
                maximumDurationSeconds);
            string normalizedQuestId = string.IsNullOrWhiteSpace(questId)
                ? "unknown"
                : questId.Trim().ToLowerInvariant();
            return BeginSandstorm(duration, $"quest:{normalizedQuestId}");
        }

        public bool StopSandstorm()
        {
            return EndSandstorm(false, "manual");
        }

        public bool StopSandstormFromQuest(string questId)
        {
            string normalizedQuestId = string.IsNullOrWhiteSpace(questId)
                ? "unknown"
                : questId.Trim().ToLowerInvariant();
            return EndSandstorm(false, $"quest:{normalizedQuestId}");
        }

        [ContextMenu("Start Sandstorm")]
        private void StartSandstormFromContextMenu()
        {
            StartSandstorm();
        }

        [ContextMenu("Stop Sandstorm")]
        private void StopSandstormFromContextMenu()
        {
            StopSandstorm();
        }

        public bool SetWeather(StationWeather newWeather)
        {
            if (newWeather == StationWeather.Sandstorm)
                return StartSandstorm();

            if (IsSandstormActive)
            {
                bool ended = EndSandstorm(false, "manual");
                if (newWeather == StationWeather.Clear)
                    return ended;
            }

            if (weather == newWeather)
                return false;

            SetWeatherInternal(newWeather, "manual");
            return true;
        }

        private bool BeginSandstorm(float durationSeconds, string cause)
        {
            if (IsSandstormActive)
                return false;

            activeSandstormDuration = Mathf.Max(
                MinimumStormDuration,
                durationSeconds);
            sandstormElapsed = 0f;
            SetWeatherInternal(StationWeather.Sandstorm, cause);
            ApplyRenderingState();
            NotifySandstormStarted();
            return true;
        }

        private bool EndSandstorm(bool completed, string cause)
        {
            if (!IsSandstormActive)
                return false;

            sandstormElapsed = completed
                ? activeSandstormDuration
                : sandstormElapsed;
            SetWeatherInternal(StationWeather.Clear, cause);
            ApplyRenderingState();
            SandstormEnded?.Invoke(completed);
            AnySandstormEnded?.Invoke(completed);
            activeSandstormDuration = 0f;
            sandstormElapsed = 0f;
            activeWeatherCause = string.Empty;
            ScheduleNextAutomaticRoll();
            return true;
        }

        private void SetWeatherInternal(
            StationWeather newWeather,
            string cause)
        {
            weather = newWeather;
            activeWeatherCause = cause ?? string.Empty;
            WeatherChanged?.Invoke(weather);
            ReportQuestWeather();
        }

        private void NotifySandstormStarted()
        {
            SandstormStarted?.Invoke(activeSandstormDuration);
            AnySandstormStarted?.Invoke(activeSandstormDuration);
        }

        private float ResolveQuestDuration(float minimum, float maximum)
        {
            if (minimum <= 0f && maximum <= 0f)
                return Config.GetRandomSandstormDuration();

            float safeMinimum = Mathf.Max(MinimumStormDuration, minimum);
            float safeMaximum = Mathf.Max(safeMinimum, maximum);
            return UnityEngine.Random.Range(safeMinimum, safeMaximum);
        }

        private void ScheduleNextAutomaticRoll()
        {
            secondsUntilNextRoll = Config.GetRandomRollInterval();
        }

        private void ApplyRenderingState()
        {
            bool renderSandstorm =
                controlSandstormRendering && IsSandstormActive;
            ScriptableRendererFeature feature =
                Config.SandstormRendererFeature;
            if (renderSandstorm)
            {
                if (feature != null && Config.ToggleRendererFeature)
                    feature.SetActive(true);

                BeginFogTransition(
                    Config.SandstormFogDensity,
                    Config.SandstormFogFadeDurationSeconds,
                    false);
                return;
            }

            if (Config.ToggleRendererFeature &&
                (feature == null || !feature.isActive))
            {
                SetFogDensityImmediately(Config.ClearFogDensity);
                return;
            }

            BeginFogTransition(
                Config.ClearFogDensity,
                Config.SandstormFogFadeDurationSeconds,
                Config.ToggleRendererFeature);
        }

        private void SetFogDensityImmediately(float density)
        {
            fogTransitionActive = false;
            disableRendererFeatureAfterFogTransition = false;
            fogTransitionElapsed = 0f;

            if (TryGetFogMaterial(
                    out Material material,
                    out string propertyName))
            {
                material.SetFloat(propertyName, Mathf.Max(0f, density));
            }
        }

        private void BeginFogTransition(
            float targetDensity,
            float durationSeconds,
            bool disableFeatureAfterTransition)
        {
            fogTransitionActive = false;
            disableRendererFeatureAfterFogTransition =
                disableFeatureAfterTransition;

            if (!TryGetFogMaterial(
                    out Material material,
                    out string propertyName))
            {
                CompleteFogTransition();
                return;
            }

            fogTransitionStartDensity = material.GetFloat(propertyName);
            fogTransitionTargetDensity = Mathf.Max(0f, targetDensity);
            fogTransitionElapsed = 0f;
            fogTransitionDuration = Mathf.Max(0.01f, durationSeconds);

            if (Mathf.Approximately(
                    fogTransitionStartDensity,
                    fogTransitionTargetDensity))
            {
                material.SetFloat(
                    propertyName,
                    fogTransitionTargetDensity);
                CompleteFogTransition();
                return;
            }

            fogTransitionActive = true;
        }

        private void AdvanceFogTransition(float deltaTime)
        {
            if (!fogTransitionActive || deltaTime <= 0f)
                return;

            if (!TryGetFogMaterial(
                    out Material material,
                    out string propertyName))
            {
                CompleteFogTransition();
                return;
            }

            fogTransitionElapsed = Mathf.Min(
                fogTransitionElapsed + deltaTime,
                fogTransitionDuration);
            float progress = Mathf.Clamp01(
                fogTransitionElapsed / fogTransitionDuration);
            material.SetFloat(
                propertyName,
                Mathf.SmoothStep(
                    fogTransitionStartDensity,
                    fogTransitionTargetDensity,
                    progress));

            if (progress >= 1f)
                CompleteFogTransition();
        }

        private void CompleteFogTransition()
        {
            fogTransitionActive = false;
            fogTransitionElapsed = 0f;

            if (!disableRendererFeatureAfterFogTransition)
                return;

            disableRendererFeatureAfterFogTransition = false;
            ScriptableRendererFeature feature =
                Config.SandstormRendererFeature;
            if (feature != null && Config.ToggleRendererFeature)
                feature.SetActive(false);
        }

        private bool TryGetFogMaterial(
            out Material material,
            out string propertyName)
        {
            material = Config.VolumetricFogMaterial;
            propertyName = Config.FogDensityProperty;
            return material != null && material.HasProperty(propertyName);
        }

        private void ReportQuestWeather()
        {
            QuestController.Instance?.Report(
                QuestSignalType.WeatherChanged,
                weather.ToString().ToLowerInvariant(),
                weather.ToString(),
                cause: activeWeatherCause);
        }

        private void SynchronizeQuestWeather()
        {
            QuestController.Instance?.SynchronizeState(
                QuestSignalType.WeatherChanged,
                weather.ToString().ToLowerInvariant(),
                weather.ToString(),
                cause: activeWeatherCause);
        }

        private void OnDisable()
        {
            fogTransitionActive = false;
            disableRendererFeatureAfterFogTransition = false;
            if (Instance == this && config != null)
            {
                Material material = config.VolumetricFogMaterial;
                string propertyName =
                    config.FogDensityProperty;
                if (material != null && material.HasProperty(propertyName))
                {
                    material.SetFloat(
                        propertyName,
                        config.ClearFogDensity);
                }

                if (config.SandstormRendererFeature != null &&
                    config.ToggleRendererFeature)
                {
                    config.SandstormRendererFeature.SetActive(false);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
