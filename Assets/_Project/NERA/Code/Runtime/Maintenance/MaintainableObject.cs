using System;
using NERA.Energy;
using NERA.Interaction;
using UnityEngine;

namespace NERA.Maintenance
{
    public sealed class MaintainableObject : BaseInteractable
    {
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

        public event Action<float> ConditionChanged;

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
        }

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

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }
    }
}
