using NERA.Maintenance;
using NERA.Station;
using UnityEngine;

namespace NERA.Energy
{
    [RequireComponent(typeof(StationObjectIdentity))]
    public sealed class SolarPowerSource : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float outputMultiplier = 1f;
        [SerializeField] private MaintainableObject maintenance;

        private StationObjectIdentity identity;
        private StationSystemsController stationSystems;
        private bool registered;
        private string registeredPanelId;

        private void Awake()
        {
            CacheIdentity();
            if (maintenance == null)
                maintenance = GetComponent<MaintainableObject>();

            if (maintenance != null)
                maintenance.ConditionChanged += HandleConditionChanged;

            StationSystemsController.InstanceChanged +=
                HandleStationSystemsInstanceChanged;
            BindStationSystems(StationSystemsController.Instance);
        }

        private void Start()
        {
            Register();
        }

        private void Update()
        {
            if (stationSystems == null)
                BindStationSystems(StationSystemsController.Instance);
            if (!registered)
                Register();
        }

        private void Register()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return;

            string stableId = ResolvePanelId();
            if (!energy.RegisterSolarPanel(
                    stableId,
                    EffectiveOutputMultiplier))
            {
                return;
            }

            registeredPanelId = stableId;
            registered = true;
        }

        private void UpdateRegisteredOutput()
        {
            EnergySystemController.Instance?.RegisterSolarPanel(
                ActivePanelId,
                EffectiveOutputMultiplier);
        }

        private float EffectiveOutputMultiplier =>
            IsRequestedActive()
                ? outputMultiplier *
                  (maintenance != null ? maintenance.Condition : 1f)
                : 0f;

        private bool IsRequestedActive()
        {
            if (stationSystems == null)
                return true;

            string panelId = ResolvePanelId();
            StationSystemDefinition definition =
                stationSystems.GetDefinition(
                    StationSystemType.SolarPanel,
                    panelId);
            return stationSystems.IsRequestedActive(
                StationSystemType.SolarPanel,
                panelId,
                definition?.InitialLevel ?? 1,
                definition?.InitiallyActive ?? true);
        }

        private string ActivePanelId =>
            string.IsNullOrWhiteSpace(registeredPanelId)
                ? ResolvePanelId()
                : registeredPanelId;

        private string ResolvePanelId()
        {
            CacheIdentity();
            return identity != null ? identity.ObjectId : string.Empty;
        }

        private void CacheIdentity()
        {
            if (identity == null)
                identity = GetComponent<StationObjectIdentity>();
        }

        private void OnValidate()
        {
            CacheIdentity();
        }

        private void HandleConditionChanged(float _)
        {
            if (registered)
                UpdateRegisteredOutput();
        }

        private void HandleStationSystemsInstanceChanged(
            StationSystemsController controller)
        {
            BindStationSystems(controller);
        }

        private void BindStationSystems(StationSystemsController controller)
        {
            if (stationSystems == controller)
                return;

            if (stationSystems != null)
                stationSystems.SystemsChanged -= HandleSystemsChanged;
            stationSystems = controller;
            if (stationSystems != null)
                stationSystems.SystemsChanged += HandleSystemsChanged;

            if (registered)
                UpdateRegisteredOutput();
        }

        private void HandleSystemsChanged()
        {
            if (registered)
                UpdateRegisteredOutput();
        }

        private void OnDestroy()
        {
            StationSystemsController.InstanceChanged -=
                HandleStationSystemsInstanceChanged;
            if (stationSystems != null)
                stationSystems.SystemsChanged -= HandleSystemsChanged;
            if (maintenance != null)
                maintenance.ConditionChanged -= HandleConditionChanged;
        }
    }
}
