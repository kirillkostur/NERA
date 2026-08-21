using UnityEngine;
using UnityEngine.Rendering;

namespace NERA.Energy
{
    /// <summary>
    /// Applies the station clock to scene lighting. The visual orbit uses the
    /// same sunrise and sunset hours as solar generation.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    [AddComponentMenu("NERA/Energy/Station Time Of Day Visuals")]
    public sealed class StationTimeOfDayVisualController : MonoBehaviour
    {
        [Header("Time Source")]
        [Tooltip(
            "Optional explicit source. When empty in Play Mode, the active " +
            "StationEnvironmentController singleton is used automatically.")]
        [SerializeField]
        private StationEnvironmentController environmentOverride;
        [Tooltip("Apply the lighting while the game is not running.")]
        [SerializeField] private bool previewInEditMode = true;
        [Tooltip("Hour used for the Edit Mode preview when no source is assigned.")]
        [SerializeField, Range(0f, 24f)] private float previewHour = 12f;

        [Header("Sun Orbit")]
        [SerializeField] private Light sunLight;
        [Tooltip("Horizontal direction of the sun path in degrees.")]
        [SerializeField, Range(-180f, 180f)] private float sunAzimuth = -160f;
        [Tooltip("Roll of the sun orbit for a tilted path across the sky.")]
        [SerializeField, Range(-90f, 90f)] private float orbitRoll;
        [Tooltip(
            "Use sunrise and sunset from EnergyBalanceConfig so visuals and " +
            "solar generation change at the same time.")]
        [SerializeField] private bool useEnvironmentDaylightHours = true;
        [SerializeField, Range(0f, 24f)] private float fallbackSunriseHour = 6f;
        [SerializeField, Range(0f, 24f)] private float fallbackSunsetHour = 18f;
        [SerializeField] private bool assignAsRenderSettingsSun = true;

        [Header("Sun Appearance")]
        [Tooltip(
            "Evaluated by sun orbit: 0 sunrise, 0.25 zenith, 0.5 sunset, " +
            "0.75 midnight, 1 next sunrise.")]
        [SerializeField] private Gradient sunColor = CreateSunColorGradient();
        [SerializeField] private AnimationCurve sunIntensity =
            CreateSunIntensityCurve();
        [SerializeField, Min(0f)] private float maximumSunIntensity = 0.69f;
        [SerializeField] private AnimationCurve shadowStrength =
            CreateShadowStrengthCurve();
        [SerializeField, Range(0f, 1f)]
        private float maximumShadowStrength = 0.1f;
        [Tooltip("Disable the Light component when its evaluated intensity is zero.")]
        [SerializeField] private bool disableSunAtNight = true;

        [Header("Ambient Lighting")]
        [SerializeField] private bool controlAmbientLighting = true;
        [SerializeField] private AmbientMode ambientMode = AmbientMode.Trilight;
        [SerializeField] private Gradient ambientSkyColor =
            CreateAmbientSkyGradient();
        [SerializeField] private Gradient ambientEquatorColor =
            CreateAmbientEquatorGradient();
        [SerializeField] private Gradient ambientGroundColor =
            CreateAmbientGroundGradient();
        [SerializeField] private AnimationCurve ambientIntensity =
            CreateAmbientIntensityCurve();
        [SerializeField, Min(0f)] private float maximumAmbientIntensity = 1f;

        [Header("Built-in Fog Color")]
        [Tooltip(
            "Updates RenderSettings.fogColor. This does not enable built-in fog " +
            "and is independent from Custom_VolumetricFog.")]
        [SerializeField] private bool controlFogColor = true;
        [SerializeField] private Gradient fogColor = CreateFogColorGradient();
        [SerializeField] private bool controlFogDensity;
        [SerializeField] private AnimationCurve fogDensity =
            CreateFogDensityCurve();
        [SerializeField, Min(0f)] private float maximumFogDensity = 0.035f;

        private const float MinimumDaySegment = 0.001f;
        private const float LightDisableThreshold = 0.0001f;

        private float evaluatedHour;
        private float sunOrbit01;

        public float EvaluatedHour => evaluatedHour;
        public float SunOrbit01 => sunOrbit01;

        private void Reset()
        {
            sunLight = GetComponent<Light>();
            ApplyNow();
        }

        private void OnEnable()
        {
            CacheSunLight();
            ApplyNow();
        }

        private void LateUpdate()
        {
            if (Application.isPlaying || previewInEditMode)
                ApplyNow();
        }

        private void OnValidate()
        {
            previewHour = Mathf.Repeat(previewHour, 24f);
            fallbackSunriseHour = Mathf.Repeat(fallbackSunriseHour, 24f);
            fallbackSunsetHour = Mathf.Repeat(fallbackSunsetHour, 24f);
            maximumSunIntensity = Mathf.Max(0f, maximumSunIntensity);
            maximumAmbientIntensity = Mathf.Max(0f, maximumAmbientIntensity);
            maximumFogDensity = Mathf.Max(0f, maximumFogDensity);
            CacheSunLight();

            if (!Application.isPlaying &&
                previewInEditMode &&
                isActiveAndEnabled)
            {
                ApplyNow();
            }
        }

        [ContextMenu("Apply Time Of Day Now")]
        public void ApplyNow()
        {
            CacheSunLight();

            StationEnvironmentController environment = ResolveEnvironment();
            evaluatedHour = ResolveHour(environment);
            ResolveDaylightHours(
                environment,
                out float sunriseHour,
                out float sunsetHour);
            sunOrbit01 = CalculateSunOrbit01(
                evaluatedHour,
                sunriseHour,
                sunsetHour);

            ApplySun(sunOrbit01);
            ApplyAmbient(sunOrbit01);
            ApplyFog(sunOrbit01);
        }

        [ContextMenu("Use Current Time As Preview")]
        private void UseCurrentTimeAsPreview()
        {
            StationEnvironmentController environment = ResolveEnvironment();
            if (environment != null)
                previewHour = environment.CurrentHour;

            ApplyNow();
        }

        public void SetPreviewHour(float hour)
        {
            previewHour = Mathf.Repeat(hour, 24f);
            if (!Application.isPlaying)
                ApplyNow();
        }

        private void ApplySun(float orbit01)
        {
            if (sunLight == null)
                return;

            float solarAngle = orbit01 * 360f;
            sunLight.transform.rotation = Quaternion.Euler(
                solarAngle,
                sunAzimuth,
                orbitRoll);

            float intensity01 = EvaluateCurve(sunIntensity, orbit01, 1f);
            sunLight.intensity =
                maximumSunIntensity * Mathf.Max(0f, intensity01);
            sunLight.color = EvaluateGradient(sunColor, orbit01, Color.white);

            float shadows01 = EvaluateCurve(shadowStrength, orbit01, 1f);
            sunLight.shadowStrength =
                maximumShadowStrength * Mathf.Clamp01(shadows01);

            if (disableSunAtNight)
                sunLight.enabled = sunLight.intensity > LightDisableThreshold;
            else if (!sunLight.enabled)
                sunLight.enabled = true;

            if (assignAsRenderSettingsSun)
                RenderSettings.sun = sunLight;
        }

        private void ApplyAmbient(float orbit01)
        {
            if (!controlAmbientLighting)
                return;

            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = EvaluateGradient(
                ambientSkyColor,
                orbit01,
                RenderSettings.ambientSkyColor);
            RenderSettings.ambientEquatorColor = EvaluateGradient(
                ambientEquatorColor,
                orbit01,
                RenderSettings.ambientEquatorColor);
            RenderSettings.ambientGroundColor = EvaluateGradient(
                ambientGroundColor,
                orbit01,
                RenderSettings.ambientGroundColor);
            RenderSettings.ambientIntensity =
                maximumAmbientIntensity *
                Mathf.Max(0f, EvaluateCurve(ambientIntensity, orbit01, 1f));
        }

        private void ApplyFog(float orbit01)
        {
            if (controlFogColor)
            {
                RenderSettings.fogColor = EvaluateGradient(
                    fogColor,
                    orbit01,
                    RenderSettings.fogColor);
            }

            if (controlFogDensity)
            {
                RenderSettings.fogDensity =
                    maximumFogDensity *
                    Mathf.Max(0f, EvaluateCurve(fogDensity, orbit01, 1f));
            }
        }

        private StationEnvironmentController ResolveEnvironment()
        {
            if (environmentOverride != null)
                return environmentOverride;

            return Application.isPlaying
                ? StationEnvironmentController.Instance
                : null;
        }

        private float ResolveHour(StationEnvironmentController environment)
        {
            if (environment != null)
                return Mathf.Repeat(environment.CurrentHour, 24f);

            return Mathf.Repeat(previewHour, 24f);
        }

        private void ResolveDaylightHours(
            StationEnvironmentController environment,
            out float sunriseHour,
            out float sunsetHour)
        {
            if (useEnvironmentDaylightHours && environment != null)
            {
                sunriseHour = environment.Config.SunriseHour;
                sunsetHour = environment.Config.SunsetHour;
                return;
            }

            sunriseHour = fallbackSunriseHour;
            sunsetHour = fallbackSunsetHour;
        }

        private void CacheSunLight()
        {
            if (sunLight == null)
                sunLight = GetComponent<Light>();
        }

        private static float CalculateSunOrbit01(
            float hour,
            float sunriseHour,
            float sunsetHour)
        {
            hour = Mathf.Repeat(hour, 24f);
            sunriseHour = Mathf.Repeat(sunriseHour, 24f);
            sunsetHour = Mathf.Repeat(sunsetHour, 24f);

            float dayLength = ForwardHours(sunriseHour, sunsetHour);
            if (dayLength < MinimumDaySegment)
                return Mathf.Repeat((hour - sunriseHour) / 24f, 1f);

            float elapsedFromSunrise = ForwardHours(sunriseHour, hour);
            if (elapsedFromSunrise <= dayLength)
            {
                return 0.5f * Mathf.Clamp01(
                    elapsedFromSunrise / dayLength);
            }

            float nightLength = 24f - dayLength;
            if (nightLength < MinimumDaySegment)
                return 0.5f;

            return 0.5f + 0.5f * Mathf.Clamp01(
                (elapsedFromSunrise - dayLength) / nightLength);
        }

        private static float ForwardHours(float fromHour, float toHour)
        {
            return Mathf.Repeat(toHour - fromHour, 24f);
        }

        private static float EvaluateCurve(
            AnimationCurve curve,
            float time,
            float fallback)
        {
            return curve != null ? curve.Evaluate(time) : fallback;
        }

        private static Color EvaluateGradient(
            Gradient gradient,
            float time,
            Color fallback)
        {
            return gradient != null ? gradient.Evaluate(time) : fallback;
        }

        private static AnimationCurve CreateSunIntensityCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.08f, 0.4f),
                new Keyframe(0.25f, 1f),
                new Keyframe(0.42f, 0.4f),
                new Keyframe(0.5f, 0f),
                new Keyframe(1f, 0f));
        }

        private static AnimationCurve CreateShadowStrengthCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.08f, 0.5f),
                new Keyframe(0.25f, 1f),
                new Keyframe(0.42f, 0.5f),
                new Keyframe(0.5f, 0f),
                new Keyframe(1f, 0f));
        }

        private static AnimationCurve CreateAmbientIntensityCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.25f, 1f),
                new Keyframe(0.5f, 0.3f),
                new Keyframe(0.75f, 0.08f),
                new Keyframe(1f, 0.35f));
        }

        private static AnimationCurve CreateFogDensityCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.25f, 0.65f),
                new Keyframe(0.5f, 1f),
                new Keyframe(0.75f, 0.8f),
                new Keyframe(1f, 1f));
        }

        private static Gradient CreateSunColorGradient()
        {
            return CreateGradient(
                new GradientColorKey(new Color(1f, 0.35f, 0.12f), 0f),
                new GradientColorKey(new Color(1f, 0.62f, 0.32f), 0.08f),
                new GradientColorKey(
                    new Color(1f, 0.68835f, 0.43867922f),
                    0.25f),
                new GradientColorKey(new Color(1f, 0.55f, 0.25f), 0.42f),
                new GradientColorKey(new Color(1f, 0.2f, 0.08f), 0.5f),
                new GradientColorKey(new Color(0.2f, 0.3f, 0.6f), 0.75f),
                new GradientColorKey(new Color(1f, 0.35f, 0.12f), 1f));
        }

        private static Gradient CreateAmbientSkyGradient()
        {
            return CreateGradient(
                new GradientColorKey(new Color(0.24f, 0.14f, 0.12f), 0f),
                new GradientColorKey(new Color(0.212f, 0.227f, 0.259f), 0.25f),
                new GradientColorKey(new Color(0.22f, 0.1f, 0.08f), 0.5f),
                new GradientColorKey(new Color(0.015f, 0.025f, 0.06f), 0.75f),
                new GradientColorKey(new Color(0.24f, 0.14f, 0.12f), 1f));
        }

        private static Gradient CreateAmbientEquatorGradient()
        {
            return CreateGradient(
                new GradientColorKey(new Color(0.2f, 0.08f, 0.05f), 0f),
                new GradientColorKey(new Color(0.114f, 0.125f, 0.133f), 0.25f),
                new GradientColorKey(new Color(0.18f, 0.055f, 0.035f), 0.5f),
                new GradientColorKey(new Color(0.01f, 0.015f, 0.035f), 0.75f),
                new GradientColorKey(new Color(0.2f, 0.08f, 0.05f), 1f));
        }

        private static Gradient CreateAmbientGroundGradient()
        {
            return CreateGradient(
                new GradientColorKey(new Color(0.07f, 0.035f, 0.025f), 0f),
                new GradientColorKey(new Color(0.047f, 0.043f, 0.035f), 0.25f),
                new GradientColorKey(new Color(0.06f, 0.025f, 0.018f), 0.5f),
                new GradientColorKey(new Color(0.004f, 0.006f, 0.014f), 0.75f),
                new GradientColorKey(new Color(0.07f, 0.035f, 0.025f), 1f));
        }

        private static Gradient CreateFogColorGradient()
        {
            return CreateGradient(
                new GradientColorKey(new Color(1f, 0.45f, 0.22f), 0f),
                new GradientColorKey(new Color(1f, 0.668f, 0.38f), 0.25f),
                new GradientColorKey(new Color(0.8f, 0.25f, 0.12f), 0.5f),
                new GradientColorKey(new Color(0.025f, 0.04f, 0.09f), 0.75f),
                new GradientColorKey(new Color(1f, 0.45f, 0.22f), 1f));
        }

        private static Gradient CreateGradient(
            params GradientColorKey[] colorKeys)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                colorKeys,
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }
    }
}
