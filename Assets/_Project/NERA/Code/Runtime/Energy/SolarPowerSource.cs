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
        private bool registered;
        private string registeredPanelId;

        private void Awake()
        {
            CacheIdentity();
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
            outputMultiplier * (maintenance != null ? maintenance.Condition : 1f);

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

        private void OnDestroy()
        {
            if (maintenance != null)
                maintenance.ConditionChanged -= HandleConditionChanged;
        }
    }
}
