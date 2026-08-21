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

        [Header("Shared Activity Consumers (per second)")]
        [SerializeField, Min(0f)] private float itemChargingConsumption = 4f;

        [Header("Power Management")]
        [Tooltip(
            "Fallback cutoff for consumers that do not have an object-specific setting.")]
        [SerializeField, Range(0f, 100f)]
        private float defaultConsumerMinimumChargePercent = 25f;
        [Tooltip(
            "Consumers at or above this priority may use battery backup reserve.")]
        [SerializeField, Min(0)] private int backupReserveMinimumPriority = 80;
        [Tooltip(
            "Per-object battery cutoffs. Repeated systems use their station object id.")]
        [SerializeField]
        private List<StationObjectPowerCutoff> stationObjectCutoffs =
            new List<StationObjectPowerCutoff>();

        public float ClearDayGeneration => clearDayGeneration;
        public float CloudyDayGeneration => cloudyDayGeneration;
        public float SandstormGeneration => sandstormGeneration;
        public float ItemChargingConsumption => itemChargingConsumption;
        public float DefaultConsumerMinimumCharge01 =>
            Mathf.Clamp(defaultConsumerMinimumChargePercent, 0f, 100f) / 100f;
        public int BackupReserveMinimumPriority =>
            Mathf.Max(0, backupReserveMinimumPriority);
        public IReadOnlyList<StationObjectPowerCutoff> StationObjectCutoffs =>
            stationObjectCutoffs != null
                ? stationObjectCutoffs
                : Array.Empty<StationObjectPowerCutoff>();
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
