using UnityEngine;

namespace NERA.Energy
{
    public sealed class StationBattery : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float capacity = 1000f;
        [SerializeField, Min(0f)] private float initialCharge = 1000f;

        private bool registered;

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

            if (!energy.RegisterBattery(
                    StationEnergyDeviceId.Build(this, "battery"),
                    capacity,
                    initialCharge))
                return;

            registered = true;
            enabled = false;
        }
    }
}
