using UnityEngine;

namespace NERA.Energy
{
    [CreateAssetMenu(
        fileName = "EnergyBalance_Default",
        menuName = "NERA/Energy/Balance Config"
    )]
    public sealed class EnergyBalanceConfig : ScriptableObject
    {
        [Header("Storage")]
        [SerializeField, Min(1f)] private float batteryCapacity = 1000f;
        [SerializeField, Min(0f)] private float batteryInitialCharge = 1000f;

        [Header("Solar Generation (per panel / second)")]
        [SerializeField, Min(0f)] private float clearDayGeneration = 40f;
        [SerializeField, Min(0f)] private float cloudyDayGeneration = 18f;
        [SerializeField, Min(0f)] private float sandstormGeneration = 5f;
        [SerializeField, Min(0f)] private float outdoorDeviceConditionLossPerSecond = 0.005f;

        [Header("Station Consumers (per second)")]
        [SerializeField, Min(0f)] private float terminalConsumption = 2f;
        [SerializeField, Min(0f)] private float laboratoryConsumption = 4f;
        [SerializeField, Min(0f)] private float droneChargingConsumption = 4f;
        [SerializeField, Min(0f)] private float antennaCalibrationConsumption = 3f;
        [SerializeField, Min(0f)] private float lightingConsumption = 3f;
        [SerializeField, Min(0f)] private float turretIdleConsumption = 2f;
        [SerializeField, Min(0f)] private float turretFiringConsumption = 5f;

        [Header("Drone")]
        [SerializeField, Min(0.1f)] private float droneRechargeDuration = 20f;

        [Header("Antenna")]
        [SerializeField, Min(0.1f)] private float antennaCalibrationDuration = 8f;

        [Header("Time")]
        [SerializeField, Min(30f)] private float fullDayDurationSeconds = 600f;
        [SerializeField, Range(0f, 24f)] private float sunriseHour = 6f;
        [SerializeField, Range(0f, 24f)] private float sunsetHour = 18f;

        public float BatteryCapacity => batteryCapacity;
        public float BatteryInitialCharge => Mathf.Min(batteryInitialCharge, batteryCapacity);
        public float ClearDayGeneration => clearDayGeneration;
        public float CloudyDayGeneration => cloudyDayGeneration;
        public float SandstormGeneration => sandstormGeneration;
        public float OutdoorDeviceConditionLossPerSecond => outdoorDeviceConditionLossPerSecond;
        public float TerminalConsumption => terminalConsumption;
        public float LaboratoryConsumption => laboratoryConsumption;
        public float DroneChargingConsumption => droneChargingConsumption;
        public float AntennaCalibrationConsumption => antennaCalibrationConsumption;
        public float LightingConsumption => lightingConsumption;
        public float TurretIdleConsumption => turretIdleConsumption;
        public float TurretFiringConsumption => turretFiringConsumption;
        public float DroneRechargeDuration => droneRechargeDuration;
        public float AntennaCalibrationDuration => antennaCalibrationDuration;
        public float FullDayDurationSeconds => fullDayDurationSeconds;
        public float SunriseHour => sunriseHour;
        public float SunsetHour => sunsetHour;

        public static EnergyBalanceConfig LoadDefault()
        {
            EnergyBalanceConfig config =
                Resources.Load<EnergyBalanceConfig>("Energy/EnergyBalance_Default");
            return config != null ? config : CreateInstance<EnergyBalanceConfig>();
        }
    }
}
