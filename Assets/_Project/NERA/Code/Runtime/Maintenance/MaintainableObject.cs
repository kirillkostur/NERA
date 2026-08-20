using System;
using System.Collections.Generic;
using NERA.Energy;
using NERA.Quests;
using NERA.Station;
using UnityEngine;

namespace NERA.Maintenance
{
    [DisallowMultipleComponent]
    public sealed class MaintainableObject : MonoBehaviour
    {
        [SerializeField] private MaintenanceRole role = MaintenanceRole.Generic;
        [SerializeField] private bool exposedToWeather;
        [SerializeField, Range(0f, 1f)] private float initialCondition = 1f;
        [SerializeField] private ParticleSystem cleaningVfx;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color cleanColor = new Color(0.12f, 0.45f, 0.55f);
        [SerializeField] private Color dirtyColor = new Color(0.55f, 0.25f, 0.12f);

        private Material runtimeMaterial;
        private float condition = 1f;
        private StationObjectIdentity identity;

        private static readonly Dictionary<string, MaintainableObject>
            ObjectsById = new Dictionary<string, MaintainableObject>(
                StringComparer.Ordinal);

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
        public float InitialCondition => Mathf.Clamp01(initialCondition);
        public bool IsOperational => condition > 0.01f;
        public bool NeedsService => condition < 0.999f;
        public string ServiceActionText => GetServiceActionText();

        private void Awake()
        {
            CacheIdentity();
            condition = Mathf.Clamp01(initialCondition);

            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            if (targetRenderer != null)
                runtimeMaterial = targetRenderer.material;

            RefreshVisual();
            Register();
        }

        private void OnEnable()
        {
            CacheIdentity();
            Register();
        }

        private void Update()
        {
            ApplyWeatherWear(Time.deltaTime);
        }

        public bool Service()
        {
            if (!RestoreCondition())
                return false;

            if (cleaningVfx != null)
                cleaningVfx.Play();
            return true;
        }

        public void SetCondition(float value)
        {
            // Identity can be configured immediately before this component
            // becomes active (including in EditMode tests and prefab tools).
            // Re-register before evaluating an unchanged value.
            Register();
            float newCondition = Mathf.Clamp01(value);
            if (Mathf.Approximately(condition, newCondition))
                return;

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

        public static IEnumerable<MaintainableObject> ActiveObjects =>
            ObjectsById.Values;

        public bool RestoreCondition()
        {
            if (condition >= 0.999f)
                return false;

            SetCondition(1f);
            return true;
        }

        public void ResetToInitialCondition()
        {
            SetCondition(InitialCondition);
        }

        private void ApplyWeatherWear(float deltaTime)
        {
            if (!exposedToWeather ||
                deltaTime <= 0f ||
                StationEnvironmentController.Instance == null ||
                StationEnvironmentController.Instance.Weather != StationWeather.Sandstorm)
            {
                return;
            }

            EnergyBalanceConfig config = EnergySystemController.Instance != null
                ? EnergySystemController.Instance.Config
                : EnergyBalanceConfig.LoadDefault();
            SetCondition(
                condition - config.OutdoorDeviceConditionLossPerSecond * deltaTime
            );
        }

        private void RefreshVisual()
        {
            if (runtimeMaterial == null)
                return;

            runtimeMaterial.color = Color.Lerp(dirtyColor, cleanColor, condition);
        }

        private string GetServiceActionText()
        {
            switch (role)
            {
                case MaintenanceRole.SolarPanel:
                    return "Clean Solar Panel";
                case MaintenanceRole.Antenna:
                    return "Service Antenna";
                case MaintenanceRole.Turret:
                    return "Service Turret";
                case MaintenanceRole.Drone:
                    return "Service Drone";
                default:
                    return "Service Device";
            }
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
            Registered?.Invoke(this);
            QuestController.Instance?.ReportDeviceCondition(
                id,
                DisplayName,
                condition);
        }

        private void Unregister()
        {
            string id = ObjectId;
            if (!string.IsNullOrEmpty(id) &&
                ObjectsById.TryGetValue(
                    id,
                    out MaintainableObject existing) &&
                existing == this)
            {
                ObjectsById.Remove(id);
            }
        }

        private static string NormalizeId(string value)
        {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private void CacheIdentity()
        {
            if (identity == null)
            {
                identity = GetComponentInParent<StationObjectIdentity>(true);
            }
        }

        private void OnValidate()
        {
            CacheIdentity();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void OnDestroy()
        {
            Unregister();
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }
    }
}
