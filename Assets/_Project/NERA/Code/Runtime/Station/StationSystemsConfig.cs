using System;
using System.Collections.Generic;
using NERA.Localization;
using UnityEngine;

namespace NERA.Station
{
    public enum StationObjectStat
    {
        Damage,
        DetectionRange,
        RotationSpeed,
        FireInterval,
        IdleEnergyConsumption,
        DamageTaken,
        Capacity,
        Generation,
        ScanRange,
        TravelRange,
        FiringEnergyConsumption,
        InitialCharge,
        EnergyConsumption,
        CalibrationEnergyConsumption,
        CalibrationDuration,
        AimTolerance,
        BatteryCharge,
        FlightEnergyConsumption
    }

    [Serializable]
    public sealed class StationObjectStatDefinition
    {
        [SerializeField] private StationObjectStat stat;
        [SerializeField] private string displayName;
        [SerializeField] private float baseValue;
        [SerializeField] private string unit;
        [SerializeField, Range(0, 3)] private int decimals = 1;

        public StationObjectStat Stat => stat;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? stat.ToString()
            : displayName.Trim();
        public float BaseValue => baseValue;
        public string Unit => unit?.Trim() ?? string.Empty;
        public int Decimals => Mathf.Clamp(decimals, 0, 3);

        public string Format(float value)
        {
            return $"{value.ToString($"F{Decimals}")}{Unit}";
        }
    }

    [Serializable]
    public sealed class StationObjectSlotDefinition
    {
        [SerializeField] private string slotId;
        [SerializeField] private string displayName;

        public string SlotId => slotId?.Trim() ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? SlotId
            : displayName.Trim();
    }

    /// <summary>
    /// Central configuration for one concrete station object. Base values and
    /// physical slots live here; installed engineering parts only modify them.
    /// </summary>
    [Serializable]
    public sealed class StationSystemDefinition
    {
        [SerializeField] private StationSystemType systemType;
        [SerializeField] private string objectId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private bool controllable;
        [SerializeField] private bool initiallyActive = true;
        [Header("Base Object Stats")]
        [SerializeField] private List<StationObjectStatDefinition> baseStats =
            new List<StationObjectStatDefinition>();
        [Header("Physical Upgrade Slots")]
        [SerializeField] private List<StationObjectSlotDefinition> slots =
            new List<StationObjectSlotDefinition>();

        [NonSerialized] private string localizationKey;

        public StationSystemType SystemType => systemType;
        public string ObjectId => objectId?.Trim() ?? string.Empty;
        public string DisplayName => string.IsNullOrEmpty(localizationKey)
            ? RawDisplayName
            : NERALocalization.Get(
                NERALocalization.ContentTable,
                $"{localizationKey}.name",
                RawDisplayName);
        public string Description => string.IsNullOrEmpty(localizationKey)
            ? description ?? string.Empty
            : NERALocalization.Get(
                NERALocalization.ContentTable,
                $"{localizationKey}.description",
                description ?? string.Empty);
        public bool Controllable => controllable;
        public bool InitiallyActive => initiallyActive;
        public IReadOnlyList<StationObjectStatDefinition> BaseStats =>
            baseStats ?? (IReadOnlyList<StationObjectStatDefinition>)
                Array.Empty<StationObjectStatDefinition>();
        public IReadOnlyList<StationObjectSlotDefinition> Slots =>
            slots ?? (IReadOnlyList<StationObjectSlotDefinition>)
                Array.Empty<StationObjectSlotDefinition>();
        public bool SupportsPhysicalUpgrades => Slots.Count > 0;

        private string RawDisplayName => string.IsNullOrWhiteSpace(displayName)
            ? systemType.ToString()
            : displayName.Trim();

        public StationObjectStatDefinition FindStat(StationObjectStat stat)
        {
            foreach (StationObjectStatDefinition definition in BaseStats)
            {
                if (definition != null && definition.Stat == stat)
                    return definition;
            }
            return null;
        }

        public float GetBaseStat(StationObjectStat stat, float fallback = 0f)
        {
            return FindStat(stat)?.BaseValue ?? fallback;
        }

        public StationObjectSlotDefinition FindSlot(string slotId)
        {
            string requested = slotId?.Trim() ?? string.Empty;
            foreach (StationObjectSlotDefinition slot in Slots)
            {
                if (slot != null && string.Equals(
                        slot.SlotId,
                        requested,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return slot;
                }
            }
            return null;
        }

        internal void SetLocalizationKey(string key)
        {
            localizationKey = key;
        }
    }

    [CreateAssetMenu(
        fileName = "StationSystems_Default",
        menuName = "NERA/Station/Object Configuration")]
    public sealed class StationSystemsConfig : ScriptableObject
    {
        [SerializeField] private List<StationSystemDefinition> stationObjects =
            new List<StationSystemDefinition>();

        public IReadOnlyList<StationSystemDefinition> StationObjects
        {
            get
            {
                EnsureLocalizationKeys();
                return stationObjects ??
                    (IReadOnlyList<StationSystemDefinition>)
                        Array.Empty<StationSystemDefinition>();
            }
        }

        public StationSystemDefinition Find(
            StationSystemType type,
            string objectId = null)
        {
            string requestedId = Normalize(objectId);
            StationSystemDefinition firstOfType = null;
            foreach (StationSystemDefinition definition in StationObjects)
            {
                if (definition == null || definition.SystemType != type)
                    continue;

                firstOfType ??= definition;
                if (string.Equals(
                        definition.ObjectId,
                        requestedId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return definition;
                }
            }
            return string.IsNullOrEmpty(requestedId) ? firstOfType : null;
        }

        public StationSystemDefinition FindByObjectId(string objectId)
        {
            string requestedId = Normalize(objectId);
            if (string.IsNullOrEmpty(requestedId))
                return null;

            foreach (StationSystemDefinition definition in StationObjects)
            {
                if (definition != null && string.Equals(
                        definition.ObjectId,
                        requestedId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return definition;
                }
            }
            return null;
        }

        public static StationSystemsConfig LoadDefault()
        {
            return Resources.Load<StationSystemsConfig>(
                "Station/StationSystems_Default");
        }

        public static float GetEffectiveStat(
            StationSystemType type,
            string objectId,
            StationObjectStat stat,
            float fallback = 0f)
        {
            StationSystemsController runtime =
                StationSystemsController.Instance;
            if (runtime != null)
                return runtime.GetStat(type, objectId, stat, fallback);

            StationSystemDefinition definition =
                LoadDefault()?.Find(type, objectId);
            return definition?.GetBaseStat(stat, fallback) ?? fallback;
        }

        private void EnsureLocalizationKeys()
        {
            for (int index = 0; index < stationObjects.Count; index++)
            {
                StationSystemDefinition definition = stationObjects[index];
                if (definition == null)
                    continue;
                string type =
                    definition.SystemType.ToString().ToLowerInvariant();
                string id = string.IsNullOrWhiteSpace(definition.ObjectId)
                    ? "shared"
                    : definition.ObjectId.ToLowerInvariant();
                definition.SetLocalizationKey($"station.{type}.{id}");
            }
        }

        private static string Normalize(string value)
        {
            return value?.Trim() ?? string.Empty;
        }
    }
}
