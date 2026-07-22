using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.Station
{
    [Serializable]
    public sealed class StationSystemDefinition
    {
        [SerializeField] private StationSystemType systemType;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private bool controllable;
        [SerializeField] private bool initiallyActive = true;
        [SerializeField] private bool upgradeable;
        [SerializeField] private bool requiresUpgradeToOperate;
        [SerializeField] private string requiredItemId;
        [SerializeField, Min(1)] private int requiredItemCount = 1;
        [SerializeField, Min(1)] private int maxLevel = 1;

        public StationSystemType SystemType => systemType;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? systemType.ToString()
            : displayName;
        public string Description => description ?? string.Empty;
        public bool Controllable => controllable;
        public bool InitiallyActive => initiallyActive;
        public bool Upgradeable => upgradeable;
        public bool RequiresUpgradeToOperate => requiresUpgradeToOperate;
        public string RequiredItemId => requiredItemId ?? string.Empty;
        public int RequiredItemCount => Mathf.Max(1, requiredItemCount);
        public int MaxLevel => Mathf.Max(1, maxLevel);

        public StationSystemDefinition(
            StationSystemType type,
            string name,
            string details,
            bool canControl,
            bool activeAtStart,
            bool canUpgrade = false,
            bool lockedUntilUpgrade = false,
            string upgradeItemId = "",
            int upgradeItemCount = 1)
        {
            systemType = type;
            displayName = name;
            description = details;
            controllable = canControl;
            initiallyActive = activeAtStart;
            upgradeable = canUpgrade;
            requiresUpgradeToOperate = lockedUntilUpgrade;
            requiredItemId = upgradeItemId;
            requiredItemCount = Mathf.Max(1, upgradeItemCount);
            maxLevel = 1;
        }
    }

    [CreateAssetMenu(
        fileName = "StationSystems_Default",
        menuName = "NERA/Station/Systems Config")]
    public sealed class StationSystemsConfig : ScriptableObject
    {
        [SerializeField] private List<StationSystemDefinition> systems =
            new List<StationSystemDefinition>();

        public IReadOnlyList<StationSystemDefinition> Systems => systems;

        public StationSystemDefinition Find(StationSystemType type)
        {
            foreach (StationSystemDefinition definition in systems)
            {
                if (definition != null && definition.SystemType == type)
                    return definition;
            }

            return null;
        }

        public static StationSystemsConfig LoadDefault()
        {
            StationSystemsConfig loaded =
                Resources.Load<StationSystemsConfig>("Station/StationSystems_Default");
            if (loaded != null)
                return loaded;

            StationSystemsConfig fallback = CreateInstance<StationSystemsConfig>();
            fallback.systems = CreateDefaultDefinitions();
            return fallback;
        }

        public static List<StationSystemDefinition> CreateDefaultDefinitions()
        {
            return new List<StationSystemDefinition>
            {
                new StationSystemDefinition(
                    StationSystemType.SolarPanel, "SOLAR PANEL",
                    "Generates station power. Clean it outside to restore efficiency.",
                    false, true),
                new StationSystemDefinition(
                    StationSystemType.Battery, "BATTERY",
                    "Stores generated energy and supplies all active consumers.",
                    false, true),
                new StationSystemDefinition(
                    StationSystemType.Computer, "COMPUTER",
                    "Central terminal. It cannot be stopped or upgraded from itself.",
                    false, true),
                new StationSystemDefinition(
                    StationSystemType.Drone, "DRONE",
                    "Surveys nearby sectors. Upgrade its drive to reach distant expeditions.",
                    true, true, true, false, "servo_drive_01", 1),
                new StationSystemDefinition(
                    StationSystemType.Laboratory, "LABORATORY",
                    "Analyzes recovered objects and unlocks Library records.",
                    true, true),
                new StationSystemDefinition(
                    StationSystemType.Charger, "CHARGER",
                    "Restores charge to energy-powered equipment.",
                    true, true),
                new StationSystemDefinition(
                    StationSystemType.Antenna, "ANTENNA",
                    "Finds unknown signals. Install a replacement drive before first use.",
                    true, false, true, true, "servo_drive_01", 1),
                new StationSystemDefinition(
                    StationSystemType.Turret, "TURRET",
                    "Automatic station defense. Install the drive and keep it serviced.",
                    true, false, true, true, "servo_drive_01", 1)
            };
        }
    }
}
