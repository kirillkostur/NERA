using UnityEngine;

namespace NERA.Energy
{
    public sealed class EnergyConsumerComponent : MonoBehaviour
    {
        [SerializeField] private string consumerId = "station_device";
        [SerializeField, Min(0f)] private float consumptionPerSecond = 1f;
        [SerializeField] private bool disableInEmergency;
        [SerializeField] private bool activeByDefault = true;

        private bool registered;
        private bool requestedActive;

        public bool IsPowered =>
            EnergySystemController.Instance != null &&
            EnergySystemController.Instance.IsConsumerPowered(consumerId);

        private void Start()
        {
            requestedActive = activeByDefault;
            Register();
        }

        private void Update()
        {
            if (EnergySystemController.Instance == null)
                return;

            if (!registered)
                Register();
        }

        public void SetConsumptionActive(bool active)
        {
            requestedActive = active;
            EnergySystemController.Instance?.SetConsumerActive(consumerId, active);
        }

        private void Register()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return;

            energy.RegisterConsumer(
                consumerId,
                consumptionPerSecond,
                disableInEmergency
            );
            energy.SetConsumerActive(consumerId, requestedActive);
            registered = true;
        }
    }
}
