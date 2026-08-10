using System;
using System.Collections.Generic;
using NERA.Station;
using UnityEngine;

namespace NERA.Energy
{
    [Serializable]
    public sealed class StationObjectPowerCutoff
    {
        [SerializeField] private StationSystemType systemType;
        [SerializeField] private string objectId;
        [SerializeField, Range(0f, 100f)]
        private float minimumChargePercent = 25f;

        public StationSystemType SystemType => systemType;
        public string ObjectId => objectId?.Trim() ?? string.Empty;
        public float MinimumChargePercent =>
            Mathf.Clamp(minimumChargePercent, 0f, 100f);
    }

    [CreateAssetMenu(
        fileName = "EnergyBalance_Default",
        menuName = "NERA/Energy/Balance Config"
    )]
    public sealed class EnergyBalanceConfig : ScriptableObject
    {
        [Header("Solar Generation (per panel / second)")]
        [SerializeField, Min(0f)] private float clearDayGeneration = 40f;
        [SerializeField, Min(0f)] private float cloudyDayGeneration = 18f;
        [SerializeField, Min(0f)] private float sandstormGeneration = 5f;
        [SerializeField, Min(0f)] private float outdoorDeviceConditionLossPerSecond = 0.005f;

        [Header("Shared Activity Consumers (per second)")]
        [SerializeField, Min(0f)] private float itemChargingConsumption = 4f;

        [Header("Power Management")]
        [Tooltip(
            "Fallback cutoff for consumers that do not have an object-specific setting.")]
        [SerializeField, Range(0f, 100f)]
        private float defaultConsumerMinimumChargePercent = 25f;
        [Tooltip(
            "Per-object battery cutoffs. Repeated systems use their station object id.")]
        [SerializeField]
        private List<StationObjectPowerCutoff> stationObjectCutoffs =
            new List<StationObjectPowerCutoff>();

        [Header("Drone")]
        [SerializeField, Min(0.1f)] private float droneRechargeDuration = 20f;

        [Header("Time")]
        [SerializeField, Min(30f)] private float fullDayDurationSeconds = 600f;
        [SerializeField, Range(0f, 24f)] private float sunriseHour = 6f;
        [SerializeField, Range(0f, 24f)] private float sunsetHour = 18f;

        public float ClearDayGeneration => clearDayGeneration;
        public float CloudyDayGeneration => cloudyDayGeneration;
        public float SandstormGeneration => sandstormGeneration;
        public float OutdoorDeviceConditionLossPerSecond => outdoorDeviceConditionLossPerSecond;
        public float ItemChargingConsumption => itemChargingConsumption;
        public float DefaultConsumerMinimumCharge01 =>
            Mathf.Clamp(defaultConsumerMinimumChargePercent, 0f, 100f) / 100f;
        public IReadOnlyList<StationObjectPowerCutoff> StationObjectCutoffs =>
            stationObjectCutoffs != null
                ? stationObjectCutoffs
                : Array.Empty<StationObjectPowerCutoff>();
        public float DroneRechargeDuration => droneRechargeDuration;
        public float FullDayDurationSeconds => fullDayDurationSeconds;
        public float SunriseHour => sunriseHour;
        public float SunsetHour => sunsetHour;

        public float GetMinimumChargePercent(
            StationSystemType systemType,
            string objectId = null)
        {
            string requestedId = objectId?.Trim() ?? string.Empty;
            StationObjectPowerCutoff firstOfType = null;
            StationObjectPowerCutoff sharedDefault = null;

            foreach (StationObjectPowerCutoff cutoff in StationObjectCutoffs)
            {
                if (cutoff == null || cutoff.SystemType != systemType)
                    continue;

                firstOfType ??= cutoff;
                if (string.IsNullOrEmpty(cutoff.ObjectId))
                    sharedDefault ??= cutoff;
                if (string.Equals(
                        cutoff.ObjectId,
                        requestedId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return cutoff.MinimumChargePercent;
                }
            }

            StationObjectPowerCutoff fallback = !string.IsNullOrEmpty(requestedId)
                ? sharedDefault
                : sharedDefault ?? firstOfType;
            return fallback?.MinimumChargePercent ??
                DefaultConsumerMinimumCharge01 * 100f;
        }

        public float GetMinimumCharge01(
            StationSystemType systemType,
            string objectId = null)
        {
            return GetMinimumChargePercent(systemType, objectId) / 100f;
        }

        public static EnergyBalanceConfig LoadDefault()
        {
            EnergyBalanceConfig config =
                Resources.Load<EnergyBalanceConfig>("Energy/EnergyBalance_Default");
            return config != null ? config : CreateInstance<EnergyBalanceConfig>();
        }
    }
}
