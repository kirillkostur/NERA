using System;
using System.Collections.Generic;
using NERA.Items;
using NERA.Localization;
using UnityEngine;

namespace NERA.Station
{
    [Serializable]
    public sealed class StationUpgradeItemRequirement
    {
        [SerializeField] private ItemData item;
        [SerializeField, Min(1)] private int count = 1;

        public ItemData Item => item;
        public string ItemId => item != null ? item.ItemId : string.Empty;
        public string DisplayName => item != null &&
            !string.IsNullOrWhiteSpace(item.DisplayName)
                ? item.DisplayName
                : ItemId;
        public int Count => Mathf.Max(1, count);

        public StationUpgradeItemRequirement(ItemData requiredItem, int requiredCount)
        {
            item = requiredItem;
            count = Mathf.Max(1, requiredCount);
        }
    }

    [Serializable]
    public sealed class StationUpgradeLevelDefinition
    {
        [SerializeField, Min(1)] private int targetLevel = 1;
        [SerializeField] private string displayName;
        [Tooltip("Icon shown in this upgrade level slot in the station terminal.")]
        [SerializeField] private Sprite upgradeIcon;
        [SerializeField, TextArea(2, 4)] private string description;
        [SerializeField] private List<StationUpgradeItemRequirement> requiredItems =
            new List<StationUpgradeItemRequirement>();
        [SerializeField, Min(0f)] private float energyCost;

        [NonSerialized] private string localizationKey;

        public int TargetLevel => Mathf.Max(1, targetLevel);
        public string DisplayName => string.IsNullOrEmpty(localizationKey)
            ? displayName ?? string.Empty
            : NERALocalization.Get(
                NERALocalization.ContentTable,
                $"{localizationKey}.name",
                displayName ?? string.Empty);
        public Sprite UpgradeIcon => upgradeIcon;
        public string Description => string.IsNullOrEmpty(localizationKey)
            ? description ?? string.Empty
            : NERALocalization.Get(
                NERALocalization.ContentTable,
                $"{localizationKey}.description",
                description ?? string.Empty);
        public IReadOnlyList<StationUpgradeItemRequirement> RequiredItems =>
            requiredItems != null
                ? requiredItems
                : Array.Empty<StationUpgradeItemRequirement>();
        public float EnergyCost => Mathf.Max(0f, energyCost);

        public StationUpgradeLevelDefinition(
            int level,
            string name,
            string details,
            float requiredEnergy,
            params StationUpgradeItemRequirement[] items)
        {
            targetLevel = Mathf.Max(1, level);
            displayName = name;
            description = details;
            energyCost = Mathf.Max(0f, requiredEnergy);
            requiredItems = items != null
                ? new List<StationUpgradeItemRequirement>(items)
                : new List<StationUpgradeItemRequirement>();
        }

        internal void SetLocalizationKey(string parentKey)
        {
            localizationKey = $"{parentKey}.upgrade.{TargetLevel}";
        }
    }

    /// <summary>
    /// Complete configuration of one selectable station object.
    /// An empty object id marks the single shared object of its system type.
    /// Repeated systems, such as turrets, use a unique object id per instance.
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
        [SerializeField, Min(0)] private int initialLevel = 1;
        [SerializeField] private List<StationUpgradeLevelDefinition> upgradeLevels =
            new List<StationUpgradeLevelDefinition>();

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
        public int InitialLevel => Mathf.Clamp(initialLevel, 0, MaxLevel);
        public bool RequiresUpgradeToOperate => InitialLevel == 0 && MaxLevel > 0;
        public bool Upgradeable
        {
            get
            {
                foreach (StationUpgradeLevelDefinition level in UpgradeLevels)
                {
                    if (level != null && level.TargetLevel > InitialLevel)
                        return true;
                }

                return false;
            }
        }
        public int MaxLevel
        {
            get
            {
                int maximum = Mathf.Max(0, initialLevel);
                foreach (StationUpgradeLevelDefinition level in UpgradeLevels)
                {
                    if (level != null)
                        maximum = Mathf.Max(maximum, level.TargetLevel);
                }

                return maximum;
            }
        }
        public IReadOnlyList<StationUpgradeLevelDefinition> UpgradeLevels =>
            upgradeLevels != null
                ? upgradeLevels
                : Array.Empty<StationUpgradeLevelDefinition>();

        private string RawDisplayName => string.IsNullOrWhiteSpace(displayName)
            ? systemType.ToString()
            : displayName;

        public StationUpgradeLevelDefinition GetUpgradeDefinition(int targetLevel)
        {
            foreach (StationUpgradeLevelDefinition level in UpgradeLevels)
            {
                if (level != null && level.TargetLevel == targetLevel)
                    return level;
            }

            return null;
        }

        internal void SetLocalizationKey(string key)
        {
            localizationKey = key;
            foreach (StationUpgradeLevelDefinition level in UpgradeLevels)
                level?.SetLocalizationKey(localizationKey);
        }

    }

    [CreateAssetMenu(
        fileName = "StationSystems_Default",
        menuName = "NERA/Station/Systems Config")]
    public sealed class StationSystemsConfig : ScriptableObject
    {
        [SerializeField] private List<StationSystemDefinition> stationObjects =
            new List<StationSystemDefinition>();

        public IReadOnlyList<StationSystemDefinition> StationObjects
        {
            get
            {
                EnsureLocalizationKeys();
                return stationObjects != null
                    ? stationObjects
                    : Array.Empty<StationSystemDefinition>();
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
                if (definition != null &&
                    string.Equals(
                        definition.ObjectId,
                        requestedId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return definition;
                }
            }

            return null;
        }

        public StationUpgradeLevelDefinition GetUpgradeDefinition(
            StationSystemType type,
            string objectId,
            int targetLevel)
        {
            return Find(type, objectId)?.GetUpgradeDefinition(targetLevel);
        }

        public int GetMaxLevel(StationSystemType type, string objectId)
        {
            return Find(type, objectId)?.MaxLevel ?? 0;
        }

        public static StationSystemsConfig LoadDefault()
        {
            StationSystemsConfig loaded =
                Resources.Load<StationSystemsConfig>(
                    "Station/StationSystems_Default");
            if (loaded != null)
                return loaded;

            Debug.LogError(
                "StationSystems_Default is missing from Resources/Station. " +
                "Station data must be configured in that single asset.");
            return CreateInstance<StationSystemsConfig>();
        }

        private static string Normalize(string value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private void EnsureLocalizationKeys()
        {
            if (stationObjects == null)
                return;

            foreach (StationSystemDefinition definition in stationObjects)
            {
                if (definition == null)
                    continue;

                string objectId = string.IsNullOrWhiteSpace(definition.ObjectId)
                    ? "shared"
                    : NERALocalization.NormalizeKeyPart(definition.ObjectId);
                definition.SetLocalizationKey(
                    $"station.{NERALocalization.NormalizeKeyPart(definition.SystemType.ToString())}.{objectId}");
            }
        }
    }
}
