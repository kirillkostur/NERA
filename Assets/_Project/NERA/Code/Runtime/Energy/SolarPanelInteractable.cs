using NERA.Maintenance;
using UnityEngine;

namespace NERA.Energy
{
    public sealed class SolarPanelInteractable : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float outputMultiplier = 1f;
        [SerializeField] private MaintainableObject maintenance;

        private bool registered;
        private string registeredPanelId;

        private void Awake()
        {
            if (maintenance == null)
                maintenance = GetComponent<MaintainableObject>();

            if (maintenance != null)
                maintenance.ConditionChanged += HandleConditionChanged;
        }

        private void Start()
        {
            Register();
        }

        private void Update()
        {
            if (!registered)
                Register();
        }

        private void Register()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return;

            string hierarchyId = StationEnergyDeviceId.Build(this, "solar");
            if (energy.RegisterSolarPanel(
                    hierarchyId,
                    EffectiveOutputMultiplier))
            {
                registeredPanelId = hierarchyId;
            }
            else
                return;

            registered = true;
        }

        private void UpdateRegisteredOutput()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return;

            energy.RegisterSolarPanel(
                ActivePanelId,
                EffectiveOutputMultiplier
            );
        }

        private float EffectiveOutputMultiplier =>
            outputMultiplier * (maintenance != null ? maintenance.Condition : 1f);

        private string ActivePanelId =>
            string.IsNullOrWhiteSpace(registeredPanelId)
                ? StationEnergyDeviceId.Build(this, "solar")
                : registeredPanelId;

        private void HandleConditionChanged(float _)
        {
            if (registered)
                UpdateRegisteredOutput();
        }

        private void OnDestroy()
        {
            if (maintenance != null)
                maintenance.ConditionChanged -= HandleConditionChanged;
        }
    }
}
