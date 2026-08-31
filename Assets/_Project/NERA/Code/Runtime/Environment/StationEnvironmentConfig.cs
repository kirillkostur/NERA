using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NERA.World
{
    [CreateAssetMenu(
        fileName = "StationEnvironment_Default",
        menuName = "NERA/Environment/Station Environment Config")]
    public sealed class StationEnvironmentConfig : ScriptableObject
    {
        [Header("Time Of Day")]
        [SerializeField, Min(30f)]
        private float fullDayDurationSeconds = 600f;
        [SerializeField, Range(0f, 24f)] private float sunriseHour = 6f;
        [SerializeField, Range(0f, 24f)] private float sunsetHour = 18f;

        [Header("Automatic Sandstorms")]
        [SerializeField] private bool automaticSandstormsEnabled = true;
        [Tooltip(
            "Chance of starting a sandstorm after each random calm interval.")]
        [SerializeField, Range(0f, 1f)]
        private float sandstormChancePerRoll = 0.35f;
        [Tooltip("Random delay between automatic sandstorm rolls.")]
        [SerializeField, Min(1f)]
        private float sandstormRollIntervalMinSeconds = 60f;
        [SerializeField, Min(1f)]
        private float sandstormRollIntervalMaxSeconds = 120f;
        [Tooltip(
            "Random sandstorm duration. Outdoor objects become fully dirty " +
            "over the selected duration.")]
        [SerializeField, Min(1f)]
        private float sandstormDurationMinSeconds = 20f;
        [SerializeField, Min(1f)]
        private float sandstormDurationMaxSeconds = 30f;

        [Header("Sandstorm Player Damage")]
        [Tooltip(
            "Damage applied to the player on each interval while exposed " +
            "outside FogExclusionVolume shelter.")]
        [SerializeField, Min(0f)] private float sandstormPlayerDamage = 5f;
        [SerializeField, Min(0.1f)]
        private float sandstormPlayerDamageIntervalSeconds = 1f;

        [Header("Sandstorm Rendering")]
        [Tooltip(
            "Disables the complete fullscreen pass outside a sandstorm, " +
            "avoiding its blit and raymarch cost in clear weather.")]
        [SerializeField] private bool toggleRendererFeature = true;
        [SerializeField]
        private ScriptableRendererFeature sandstormRendererFeature;
        [SerializeField] private Material volumetricFogMaterial;
        [SerializeField]
        private string fogDensityProperty = "_DensityMultiplier";
        [SerializeField, Min(0f)] private float clearFogDensity;
        [SerializeField, Min(0f)] private float sandstormFogDensity = 0.3f;
        [SerializeField, Min(0.1f)]
        private float sandstormFogFadeDurationSeconds = 3f;

        public float FullDayDurationSeconds =>
            Mathf.Max(30f, fullDayDurationSeconds);
        public float SunriseHour => Mathf.Repeat(sunriseHour, 24f);
        public float SunsetHour => Mathf.Repeat(sunsetHour, 24f);
        public bool AutomaticSandstormsEnabled => automaticSandstormsEnabled;
        public float SandstormChancePerRoll =>
            Mathf.Clamp01(sandstormChancePerRoll);
        public float SandstormRollIntervalMinSeconds =>
            Mathf.Max(1f, sandstormRollIntervalMinSeconds);
        public float SandstormRollIntervalMaxSeconds => Mathf.Max(
            SandstormRollIntervalMinSeconds,
            sandstormRollIntervalMaxSeconds);
        public float SandstormDurationMinSeconds =>
            Mathf.Max(1f, sandstormDurationMinSeconds);
        public float SandstormDurationMaxSeconds => Mathf.Max(
            SandstormDurationMinSeconds,
            sandstormDurationMaxSeconds);
        public float SandstormPlayerDamage =>
            Mathf.Max(0f, sandstormPlayerDamage);
        public float SandstormPlayerDamageIntervalSeconds =>
            Mathf.Max(0.1f, sandstormPlayerDamageIntervalSeconds);
        public bool ToggleRendererFeature => toggleRendererFeature;
        public ScriptableRendererFeature SandstormRendererFeature =>
            sandstormRendererFeature;
        public Material VolumetricFogMaterial => volumetricFogMaterial;
        public string FogDensityProperty =>
            string.IsNullOrWhiteSpace(fogDensityProperty)
                ? "_DensityMultiplier"
                : fogDensityProperty.Trim();
        public float ClearFogDensity => Mathf.Max(0f, clearFogDensity);
        public float SandstormFogDensity =>
            Mathf.Max(0f, sandstormFogDensity);
        public float SandstormFogFadeDurationSeconds =>
            Mathf.Max(0.1f, sandstormFogFadeDurationSeconds);

        public float GetRandomRollInterval()
        {
            return Random.Range(
                SandstormRollIntervalMinSeconds,
                SandstormRollIntervalMaxSeconds);
        }

        public float GetRandomSandstormDuration()
        {
            return Random.Range(
                SandstormDurationMinSeconds,
                SandstormDurationMaxSeconds);
        }

        public static StationEnvironmentConfig LoadDefault()
        {
            StationEnvironmentConfig config =
                Resources.Load<StationEnvironmentConfig>(
                    "Environment/StationEnvironment_Default");
            return config != null
                ? config
                : CreateInstance<StationEnvironmentConfig>();
        }

        private void OnValidate()
        {
            fullDayDurationSeconds = Mathf.Max(30f, fullDayDurationSeconds);
            sunriseHour = Mathf.Repeat(sunriseHour, 24f);
            sunsetHour = Mathf.Repeat(sunsetHour, 24f);
            sandstormRollIntervalMinSeconds = Mathf.Max(
                1f,
                sandstormRollIntervalMinSeconds);
            sandstormRollIntervalMaxSeconds = Mathf.Max(
                sandstormRollIntervalMinSeconds,
                sandstormRollIntervalMaxSeconds);
            sandstormDurationMinSeconds = Mathf.Max(
                1f,
                sandstormDurationMinSeconds);
            sandstormDurationMaxSeconds = Mathf.Max(
                sandstormDurationMinSeconds,
                sandstormDurationMaxSeconds);
            sandstormPlayerDamage = Mathf.Max(
                0f,
                sandstormPlayerDamage);
            sandstormPlayerDamageIntervalSeconds = Mathf.Max(
                0.1f,
                sandstormPlayerDamageIntervalSeconds);
            fogDensityProperty = string.IsNullOrWhiteSpace(fogDensityProperty)
                ? "_DensityMultiplier"
                : fogDensityProperty.Trim();
            clearFogDensity = Mathf.Max(0f, clearFogDensity);
            sandstormFogDensity = Mathf.Max(0f, sandstormFogDensity);
            sandstormFogFadeDurationSeconds = Mathf.Max(
                0.1f,
                sandstormFogFadeDurationSeconds);
        }
    }
}
