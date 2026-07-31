using System;
using System.Collections.Generic;
using NERA.Energy;
using NERA.Interaction;
using NERA.Quests;
using UnityEngine;

namespace NERA.Maintenance
{
    public sealed class MaintainableObject : BaseInteractable
    {
        [Header("Identity")]
        [Tooltip("Stable save/quest ID. Device components may assign it at runtime.")]
        [SerializeField] private string objectId;
        [SerializeField] private string displayName;

        [SerializeField] private MaintenanceRole role = MaintenanceRole.Generic;
        [SerializeField] private bool exposedToWeather;
        [SerializeField, Range(0f, 1f)] private float initialCondition = 1f;
        [SerializeField, Min(0.1f)] private float serviceDuration = 2f;
        [SerializeField] private ParticleSystem cleaningVfx;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color cleanColor = new Color(0.12f, 0.45f, 0.55f);
        [SerializeField] private Color dirtyColor = new Color(0.55f, 0.25f, 0.12f);

        private Material runtimeMaterial;
        private float condition = 1f;

        private static readonly Dictionary<string, MaintainableObject>
            ObjectsById = new Dictionary<string, MaintainableObject>(
                StringComparer.Ordinal);

        public event Action<float> ConditionChanged;
        public static event Action<MaintainableObject> Registered;
        public static event Action<string, float> AnyConditionChanged;

        public string ObjectId => NormalizeId(objectId);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? gameObject.name
            : displayName.Trim();
        public MaintenanceRole Role => role;
        public bool ExposedToWeather => exposedToWeather;
        public float Condition => condition;
        public bool IsOperational => condition > 0.01f;

        private void Awake()
        {
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
            Register();
        }

        private void Update()
        {
            ApplyWeatherWear(Time.deltaTime);
        }

        public override InteractionPrompt GetPrompt()
        {
            bool needsService = condition < 0.999f;
            return new InteractionPrompt(
                GetActionText(),
                InteractionMode.Hold,
                serviceDuration,
                needsService,
                needsService
                    ? $"Condition {Mathf.RoundToInt(condition * 100f)}%"
                    : "Operational"
            );
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            if (!RestoreCondition())
                return;

            if (cleaningVfx != null)
                cleaningVfx.Play();

            base.CompleteInteraction(interactor);
        }

        public void SetCondition(float value)
        {
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

        public void SetObjectIdentity(string stableId, string name = null)
        {
            string normalized = NormalizeId(stableId);
            if (string.IsNullOrEmpty(normalized))
                return;

            Unregister();
            objectId = normalized;
            if (!string.IsNullOrWhiteSpace(name))
                displayName = name.Trim();
            Register();
        }

        public static bool TryFind(
            string stableId,
            out MaintainableObject maintainable)
        {
            return ObjectsById.TryGetValue(
                NormalizeId(stableId),
                out maintainable);
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

        private string GetActionText()
        {
            switch (role)
            {
                case MaintenanceRole.SolarPanel:
                    return "Clean Solar Panel";
                case MaintenanceRole.Antenna:
                    return "Service Antenna";
                case MaintenanceRole.Turret:
                    return "Service Turret";
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
