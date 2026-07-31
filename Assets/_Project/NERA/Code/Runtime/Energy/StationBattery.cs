using UnityEngine;

namespace NERA.Energy
{
    public sealed class StationBattery : MonoBehaviour
    {
        [Tooltip("Leave empty to generate a stable unique ID from the scene hierarchy.")]
        [SerializeField] private string batteryId;
        [SerializeField, Min(1f)] private float capacity = 1000f;
        [SerializeField, Min(0f)] private float initialCharge = 1000f;

        private EnergySystemController registeredEnergy;

        private void OnEnable()
        {
            registeredEnergy = null;
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

        private void Register()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return;

            if (!energy.RegisterBattery(
                    string.IsNullOrWhiteSpace(batteryId)
                        ? StationEnergyDeviceId.Build(this, "battery")
                        : batteryId,
                    capacity,
                    initialCharge))
                return;

            registeredEnergy = energy;
        }

        private void OnDisable()
        {
            registeredEnergy = null;
        }
    }
}
