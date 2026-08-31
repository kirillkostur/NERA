using System;
using NERA.Combat;
using NERA.Graphics;
using NERA.Energy;
using NERA.Quests;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace NERA.World
{
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class StationWeatherController : MonoBehaviour
    {
        private const float MinimumStormDuration = 0.1f;
        private const float PlayerSearchRetrySeconds = 0.5f;

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
        private PlayerHealth playerHealth;
        private Collider playerExposureCollider;
        private float playerDamageElapsed;
        private float playerSearchCooldown;


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
        public bool IsRenderingAllowedForActiveScene =>
            !Application.isPlaying ||
            StationEnvironmentController.IsPlayerStationSceneActive;
        public bool IsSandstormRendererFeatureActive =>
            Config.SandstormRendererFeature?.isActive == true;

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
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
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
            ResetPlayerExposureState(false);
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
                float stormDeltaTime = Mathf.Min(
                    deltaTime,
                    Mathf.Max(
                        0f,
                        activeSandstormDuration - sandstormElapsed));
                AdvancePlayerSandstormDamage(stormDeltaTime);
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
            ResetPlayerExposureState(false);
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
            ResetPlayerExposureState(false);
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
            if (!IsRenderingAllowedForActiveScene)
            {
                DisableRenderingOutsideStation();
                return;
            }

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

        private void DisableRenderingOutsideStation()
        {
            SetFogDensityImmediately(Config.ClearFogDensity);
            ScriptableRendererFeature feature =
                Config.SandstormRendererFeature;
            if (feature != null)
                feature.SetActive(false);
        }

        private void HandleActiveSceneChanged(Scene _, Scene __)
        {
            ResetPlayerExposureState(true);
            if (Instance == this)
                ApplyRenderingState();
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

        private void AdvancePlayerSandstormDamage(float deltaTime)
        {
            AdvancePlayerSandstormDamage(
                deltaTime,
                StationEnvironmentController.IsPlayerStationSceneActive);
        }

        private void AdvancePlayerSandstormDamage(
            float deltaTime,
            bool isPlayerStationSceneActive)
        {
            if (deltaTime <= 0f ||
                !isPlayerStationSceneActive ||
                Config.SandstormPlayerDamage <= 0f)
            {
                playerDamageElapsed = 0f;
                return;
            }

            if (!TryResolvePlayer(deltaTime) || !playerHealth.IsAlive)
            {
                playerDamageElapsed = 0f;
                return;
            }

            Vector3 exposurePoint =
                playerExposureCollider != null &&
                playerExposureCollider.enabled
                    ? playerExposureCollider.bounds.center
                    : playerHealth.transform.position + Vector3.up;
            if (FogExclusionVolume.IsWorldPointExcluded(exposurePoint))
            {
                playerDamageElapsed = 0f;
                return;
            }

            float interval = Config.SandstormPlayerDamageIntervalSeconds;
            playerDamageElapsed += deltaTime;
            while (playerDamageElapsed >= interval && playerHealth.IsAlive)
            {
                playerDamageElapsed -= interval;
                playerHealth.TakeDamage(
                    Config.SandstormPlayerDamage,
                    gameObject);
            }
        }

        private bool TryResolvePlayer(float deltaTime)
        {
            if (playerHealth != null)
                return true;

            playerExposureCollider = null;
            playerSearchCooldown -= deltaTime;
            if (playerSearchCooldown > 0f)
                return false;

            playerSearchCooldown = PlayerSearchRetrySeconds;
            playerHealth = FindFirstObjectByType<PlayerHealth>(
                FindObjectsInactive.Exclude);
            if (playerHealth != null)
                playerExposureCollider = playerHealth.GetComponent<Collider>();
            return playerHealth != null;
        }

        private void ResetPlayerExposureState(bool clearTarget)
        {
            playerDamageElapsed = 0f;
            playerSearchCooldown = 0f;
            if (!clearTarget)
                return;

            playerHealth = null;
            playerExposureCollider = null;
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
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            ResetPlayerExposureState(true);
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
