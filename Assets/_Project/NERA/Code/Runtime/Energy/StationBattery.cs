using NERA.Station;
using UnityEngine;

namespace NERA.Energy
{
    [RequireComponent(typeof(StationObjectIdentity))]
    public sealed class StationBattery : MonoBehaviour
    {
        private StationObjectIdentity identity;
        private EnergySystemController registeredEnergy;
        private StationSystemsController subscribedSystems;

        private void OnEnable()
        {
            identity = GetComponent<StationObjectIdentity>();
            StationSystemsController.InstanceChanged += HandleSystemsChanged;
            BindSystems(StationSystemsController.Instance);
            Register();
        }

        private void Start()
        {
            Register();
        }

        private void Update()
        {
            if (registeredEnergy != EnergySystemController.Instance)
                Register();
        }

        private void HandleSystemsChanged(StationSystemsController systems)
        {
            BindSystems(systems);
            Register();
        }

        private void BindSystems(StationSystemsController systems)
        {
            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged -= Register;
            subscribedSystems = systems;
            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged += Register;
        }

        private void Register()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null || identity == null)
                return;

            float capacity = StationSystemsConfig.GetEffectiveStat(
                StationSystemType.Battery,
                identity.ObjectId,
                StationObjectStat.Capacity,
                1000f);
            float initialCharge = StationSystemsConfig.GetEffectiveStat(
                StationSystemType.Battery,
                identity.ObjectId,
                StationObjectStat.InitialCharge,
                capacity);

            if (energy.RegisterBattery(
                    identity.ObjectId,
                    Mathf.Max(1f, capacity),
                    Mathf.Clamp(initialCharge, 0f, capacity)))
            {
                registeredEnergy = energy;
            }
        }

        private void OnDisable()
        {
            StationSystemsController.InstanceChanged -= HandleSystemsChanged;
            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged -= Register;
            subscribedSystems = null;
            registeredEnergy = null;
        }
    }
}
