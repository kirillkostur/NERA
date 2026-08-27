using System;
using System.Collections.Generic;
using NERA.Drone;
using NERA.World;
using NERA.Quests;
using NERA.Station;
using UnityEngine;

namespace NERA.Maintenance
{
    [DisallowMultipleComponent]
    public sealed class MaintainableObject : MonoBehaviour
    {
        private const string DefaultSandProperty = "_DissolveStrength";
        private const float RuntimeTickInterval = 0.1f;

        [SerializeField] private MaintenanceRole role = MaintenanceRole.Generic;
        [SerializeField] private bool exposedToWeather;
        [SerializeField, Range(0f, 1f)] private float initialCondition = 1f;
        [Tooltip("Time required to completely clean the object.")]
        [SerializeField, Min(0.1f)] private float cleaningDurationSeconds = 3f;
        [Tooltip("Played while the object is being cleaned.")]
        [SerializeField] private ParticleSystem cleaningVfx;
        [Tooltip("Renderer of the Sand overlay mesh.")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private string sandAmountProperty =
            DefaultSandProperty;

        private MaterialPropertyBlock sandPropertyBlock;
        private float condition = 1f;
        private float cleaningElapsedSeconds;
        private float cleaningStartCondition;
        private bool isCleaning;
        private bool participatedInCurrentSandstorm;
        private bool continuouslyExposedDuringCurrentSandstorm;
        private StationObjectIdentity identity;
        private float runtimeTickAccumulator;
        private string registeredObjectId;

        private static readonly Dictionary<string, MaintainableObject>
            ObjectsById = new Dictionary<string, MaintainableObject>(
                StringComparer.Ordinal);
        private static readonly List<string> StaleObjectIds =
            new List<string>();

        public event Action<float> ConditionChanged;
        public static event Action<MaintainableObject> Registered;
        public static event Action<string, float> AnyConditionChanged;

        public string ObjectId
        {
            get
            {
                CacheIdentity();
                return NormalizeId(identity?.ObjectId);
            }
        }
        public string DisplayName
        {
            get
            {
                CacheIdentity();
                return identity != null
                    ? identity.DisplayName
                    : gameObject.name;
            }
        }
        public MaintenanceRole Role => role;
        public bool ExposedToWeather => exposedToWeather;
        public float Condition => condition;
        public float SandAmount => 1f - condition;
        public float InitialCondition => Mathf.Clamp01(initialCondition);
        public float CleaningDurationSeconds
        {
            get => Mathf.Max(0.1f, cleaningDurationSeconds);
            set => cleaningDurationSeconds = Mathf.Max(0.1f, value);
        }
        public bool IsCleaning => isCleaning;
        public float CleaningProgress01 => isCleaning
            ? Mathf.Clamp01(
                cleaningElapsedSeconds / CleaningDurationSeconds)
            : NeedsService ? 0f : 1f;
        public bool IsOperational => condition > 0.01f;
        public bool IsSandClogged => condition <= 0.01f;
        public bool NeedsService => condition < 0.999f;
        public bool CanService =>
            NeedsService &&
            !isCleaning &&
            !IsSandstormActive() &&
            IsPhysicallyPresentAtStation();
        public string ServiceActionText => GetServiceActionText();

        private void Awake()
        {
            CacheIdentity();
            condition = Mathf.Clamp01(initialCondition);
            sandPropertyBlock = new MaterialPropertyBlock();
            CacheSandRenderer();
            RefreshVisual();
            Register();
        }

        private void OnEnable()
        {
            CacheIdentity();
            StationWeatherController.AnySandstormStarted +=
                HandleSandstormStarted;
            StationWeatherController.AnySandstormEnded +=
                HandleSandstormEnded;
            Register();
        }

        private void Update()
        {
            bool appliesWeatherWear = exposedToWeather &&
                IsSandstormActive();
            if (!isCleaning && !appliesWeatherWear)
            {
                runtimeTickAccumulator = 0f;
                return;
            }

            runtimeTickAccumulator += Time.deltaTime;
            if (runtimeTickAccumulator < RuntimeTickInterval)
                return;

            float elapsed = runtimeTickAccumulator;
            runtimeTickAccumulator = 0f;
            AdvanceCleaning(elapsed);
            ApplyWeatherWear(elapsed);
        }

        public bool Service()
        {
            if (!CanService)
                return false;

            isCleaning = true;
            runtimeTickAccumulator = 0f;
            cleaningElapsedSeconds = 0f;
            cleaningStartCondition = condition;

            if (cleaningVfx != null)
            {
                cleaningVfx.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                cleaningVfx.Play(true);
            }
            return true;
        }

        public void SetCondition(float value)
        {
            CancelCleaning(true);
            ApplyCondition(value);
        }

        public void AdvanceCleaning(float deltaTime)
        {
            if (!isCleaning || deltaTime <= 0f)
                return;

            if (IsSandstormActive())
            {
                CancelCleaning(true);
                return;
            }

            cleaningElapsedSeconds = Mathf.Min(
                cleaningElapsedSeconds + deltaTime,
                CleaningDurationSeconds);
            float progress = CleaningProgress01;
            if (progress >= 1f)
            {
                CompleteCleaning();
                return;
            }

            ApplyCondition(Mathf.Lerp(
                cleaningStartCondition,
                1f,
                progress));
        }

        private void ApplyCondition(float value)
        {
            // Identity can be configured immediately before this component
            // becomes active (including in EditMode tests and prefab tools).
            // Re-register before evaluating an unchanged value.
            Register();
            float newCondition = Mathf.Clamp01(value);
            if (Mathf.Approximately(condition, newCondition))
            {
                RefreshVisual();
                return;
            }

            condition = newCondition;
            RefreshVisual();
            ConditionChanged?.Invoke(condition);
            if (!string.IsNullOrEmpty(ObjectId))
            {
                AnyConditionChanged?.Invoke(ObjectId, condition);
                QuestController.Instance?.ReportDeviceCondition(
                    ObjectId,
                    DisplayName,
                    condition);
            }
        }

        public void AdvanceSandExposure(
            float deltaTime,
            float fullContaminationDuration)
        {
            if (!CanReceiveSandExposure())
            {
                if (IsSandstormActive())
                    continuouslyExposedDuringCurrentSandstorm = false;
                return;
            }

            if (deltaTime <= 0f ||
                IsSandClogged)
            {
                return;
            }

            participatedInCurrentSandstorm = true;
            float duration = Mathf.Max(0.1f, fullContaminationDuration);
            SetCondition(condition - deltaTime / duration);
        }

        public static bool TryFind(
            string stableId,
            out MaintainableObject maintainable)
        {
            string normalized = NormalizeId(stableId);
            if (ObjectsById.TryGetValue(normalized, out maintainable) &&
                maintainable != null &&
                maintainable.isActiveAndEnabled)
            {
                return true;
            }

            if (!string.IsNullOrEmpty(normalized))
                ObjectsById.Remove(normalized);
            maintainable = null;
            return false;
        }

        public static IEnumerable<MaintainableObject> ActiveObjects
        {
            get
            {
                RemoveDestroyedRegistrations();
                return ObjectsById.Values;
            }
        }

        public bool RestoreCondition()
        {
            if ((!NeedsService && !isCleaning) ||
                IsSandstormActive() ||
                !IsPhysicallyPresentAtStation())
                return false;

            CancelCleaning(true);
            ApplyCondition(1f);
            return true;
        }

        public void ResetToInitialCondition()
        {
            SetCondition(InitialCondition);
        }

        private void ApplyWeatherWear(float deltaTime)
        {
            StationWeatherController weather =
                StationWeatherController.Instance;
            if (!exposedToWeather ||
                deltaTime <= 0f ||
                weather == null ||
                !weather.IsSandstormActive)
            {
                return;
            }

            AdvanceSandExposure(
                deltaTime,
                weather.ActiveSandstormDuration);
        }

        private void RefreshVisual()
        {
            CacheSandRenderer();
            if (targetRenderer == null)
                return;

            string propertyName = string.IsNullOrWhiteSpace(sandAmountProperty)
                ? DefaultSandProperty
                : sandAmountProperty.Trim();
            Material sharedMaterial = targetRenderer.sharedMaterial;
            if (sharedMaterial == null ||
                !sharedMaterial.HasProperty(propertyName))
            {
                return;
            }

            sandPropertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(sandPropertyBlock);
            sandPropertyBlock.SetFloat(propertyName, SandAmount);
            targetRenderer.SetPropertyBlock(sandPropertyBlock);
        }

        private void HandleSandstormStarted(float _)
        {
            runtimeTickAccumulator = 0f;
            CancelCleaning(true);
            participatedInCurrentSandstorm = false;
            continuouslyExposedDuringCurrentSandstorm =
                CanReceiveSandExposure();
        }

        private void HandleSandstormEnded(bool completed)
        {
            if (completed &&
                participatedInCurrentSandstorm &&
                continuouslyExposedDuringCurrentSandstorm &&
                !IsSandClogged)
            {
                SetCondition(0f);
            }

            participatedInCurrentSandstorm = false;
            continuouslyExposedDuringCurrentSandstorm = false;
        }

        private bool CanReceiveSandExposure()
        {
            return exposedToWeather && IsPhysicallyPresentAtStation();
        }

        private bool IsPhysicallyPresentAtStation()
        {
            return role != MaintenanceRole.Drone ||
                DroneScanController.Instance?.IsAtStation != false;
        }

        private static bool IsSandstormActive()
        {
            return StationWeatherController.Instance?.IsSandstormActive == true;
        }

        private void CompleteCleaning()
        {
            isCleaning = false;
            cleaningElapsedSeconds = CleaningDurationSeconds;
            ApplyCondition(1f);

            if (cleaningVfx != null)
            {
                cleaningVfx.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void CancelCleaning(bool clearParticles)
        {
            if (!isCleaning)
                return;

            isCleaning = false;
            cleaningElapsedSeconds = 0f;
            cleaningStartCondition = condition;

            if (cleaningVfx != null)
            {
                cleaningVfx.Stop(
                    true,
                    clearParticles
                        ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private string GetServiceActionText()
        {
            return role switch
            {
                MaintenanceRole.SolarPanel => "Clean Solar Panel",
                MaintenanceRole.Antenna => "Clean Antenna",
                MaintenanceRole.Turret => "Clean Turret",
                MaintenanceRole.Drone => "Clean Drone",
                _ => "Service Device"
            };
        }

        private void Register()
        {
            string id = ObjectId;
            if (string.IsNullOrEmpty(id) || !isActiveAndEnabled)
                return;

            if (ObjectsById.TryGetValue(
                    id,
                    out MaintainableObject existing) &&
                existing == this)
            {
                return;
            }

            ObjectsById[id] = this;
            registeredObjectId = id;
            Registered?.Invoke(this);
            QuestController.Instance?.ReportDeviceCondition(
                id,
                DisplayName,
                condition);
        }

        private void Unregister()
        {
            string id = registeredObjectId;
            if (!string.IsNullOrEmpty(id) &&
                ObjectsById.TryGetValue(
                    id,
                    out MaintainableObject existing) &&
                existing == this)
            {
                ObjectsById.Remove(id);
            }
            registeredObjectId = string.Empty;
        }

        private static void RemoveDestroyedRegistrations()
        {
            StaleObjectIds.Clear();
            foreach (KeyValuePair<string, MaintainableObject> pair in
                     ObjectsById)
            {
                if (pair.Value == null)
                    StaleObjectIds.Add(pair.Key);
            }

            foreach (string id in StaleObjectIds)
                ObjectsById.Remove(id);
            StaleObjectIds.Clear();
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ObjectsById.Clear();
            StaleObjectIds.Clear();
        }

        private static string NormalizeId(string value)
        {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private void CacheIdentity()
        {
            if (identity == null)
                identity = GetComponentInParent<StationObjectIdentity>(true);
        }

        private void CacheSandRenderer()
        {
            if (targetRenderer != null)
                return;

            foreach (Renderer candidate in GetComponentsInChildren<Renderer>(true))
            {
                if (string.Equals(
                        candidate.gameObject.name,
                        "Sand",
                        StringComparison.OrdinalIgnoreCase))
                {
                    targetRenderer = candidate;
                    return;
                }
            }
        }

        private void OnValidate()
        {
            CacheIdentity();
            CacheSandRenderer();
            initialCondition = Mathf.Clamp01(initialCondition);
            cleaningDurationSeconds = CleaningDurationSeconds;
            sandAmountProperty = string.IsNullOrWhiteSpace(sandAmountProperty)
                ? DefaultSandProperty
                : sandAmountProperty.Trim();
            if (!Application.isPlaying)
                condition = InitialCondition;
        }

        private void OnDisable()
        {
            runtimeTickAccumulator = 0f;
            CancelCleaning(true);
            StationWeatherController.AnySandstormStarted -=
                HandleSandstormStarted;
            StationWeatherController.AnySandstormEnded -=
                HandleSandstormEnded;
            participatedInCurrentSandstorm = false;
            continuouslyExposedDuringCurrentSandstorm = false;
            Unregister();
        }

        private void OnDestroy()
        {
            Unregister();
        }
    }
}
