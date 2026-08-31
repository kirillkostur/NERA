using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.UI
{
    public enum HUDNotificationSeverity
    {
        Success,
        Warning,
        Critical
    }

    [Serializable]
    public sealed class HUDNotificationDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string localizationKey;
        [SerializeField] private HUDNotificationSeverity severity;
        [SerializeField, Min(0.5f)] private float visibleSeconds = 4f;

        public string Id => id?.Trim() ?? string.Empty;
        public string LocalizationKey => localizationKey?.Trim() ?? string.Empty;
        public HUDNotificationSeverity Severity => severity;
        public float VisibleSeconds => Mathf.Max(0.5f, visibleSeconds);
    }

    [CreateAssetMenu(
        fileName = "HUDNotificationCatalog_Default",
        menuName = "NERA/UI/HUD Notification Catalog")]
    public sealed class HUDNotificationCatalog : ScriptableObject
    {
        public const string DefaultResourcePath =
            "UI/HUDNotificationCatalog_Default";

        [SerializeField]
        private List<HUDNotificationDefinition> entries =
            new List<HUDNotificationDefinition>();

        [Header("Success")]
        [SerializeField] private Color successBackground =
            new Color(0.025f, 0.16f, 0.09f, 0.94f);
        [SerializeField] private Color successAccent =
            new Color(0.12f, 0.95f, 0.42f, 1f);

        [Header("Warning")]
        [SerializeField] private Color warningBackground =
            new Color(0.22f, 0.115f, 0.015f, 0.94f);
        [SerializeField] private Color warningAccent =
            new Color(1f, 0.55f, 0.08f, 1f);

        [Header("Critical")]
        [SerializeField] private Color criticalBackground =
            new Color(0.22f, 0.025f, 0.035f, 0.94f);
        [SerializeField] private Color criticalAccent =
            new Color(1f, 0.1f, 0.12f, 1f);

        private Dictionary<string, HUDNotificationDefinition> byId;

        public IReadOnlyList<HUDNotificationDefinition> Entries => entries;

        public bool TryGet(
            string id,
            out HUDNotificationDefinition definition)
        {
            EnsureLookup();
            definition = null;
            return !string.IsNullOrWhiteSpace(id) &&
                byId.TryGetValue(id.Trim(), out definition);
        }

        public Color GetBackground(HUDNotificationSeverity severity)
        {
            return severity switch
            {
                HUDNotificationSeverity.Success => successBackground,
                HUDNotificationSeverity.Warning => warningBackground,
                _ => criticalBackground
            };
        }

        public Color GetAccent(HUDNotificationSeverity severity)
        {
            return severity switch
            {
                HUDNotificationSeverity.Success => successAccent,
                HUDNotificationSeverity.Warning => warningAccent,
                _ => criticalAccent
            };
        }

        public static HUDNotificationCatalog LoadDefault()
        {
            return Resources.Load<HUDNotificationCatalog>(
                DefaultResourcePath);
        }

        private void EnsureLookup()
        {
            if (byId != null)
                return;

            byId = new Dictionary<string, HUDNotificationDefinition>(
                StringComparer.OrdinalIgnoreCase);
            if (entries == null)
                return;

            foreach (HUDNotificationDefinition entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                    continue;
                byId[entry.Id] = entry;
            }
        }

        private void OnValidate()
        {
            byId = null;
        }
    }

    public static class HUDNotificationIds
    {
        public const string StormStarted = "weather.storm_started";
        public const string StormEnded = "weather.storm_ended";
        public const string BatteryLow = "energy.battery_low";
        public const string BatteryDisabled = "energy.battery_disabled";
        public const string BatteryEnabled = "energy.battery_enabled";
        public const string PowerLost = "energy.power_lost";
        public const string PowerRestored = "energy.power_restored";
        public const string DroneDeparted = "drone.departed";
        public const string DroneReturned = "drone.returned";
        public const string DroneLocationDiscovered =
            "drone.location_discovered";
        public const string DroneNoNewLocations =
            "drone.no_new_locations";
        public const string AntennaSignalFound = "antenna.signal_found";
        public const string AntennaSignalNotFound =
            "antenna.signal_not_found";
        public const string StationObjectContaminated =
            "station.object_contaminated";
        public const string StationObjectDisabled =
            "station.object_disabled";
        public const string ResearchCompleted = "research.completed";
    }
}
