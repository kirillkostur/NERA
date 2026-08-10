using System;
using System.Collections.Generic;
using NERA.Station;
using UnityEngine;

namespace NERA.Items
{
    public enum StationStatModifierMode
    {
        Add,
        Multiply
    }

    [Serializable]
    public sealed class StationObjectStatModifierDefinition
    {
        [SerializeField] private StationObjectStat stat;
        [SerializeField] private StationStatModifierMode mode;
        [SerializeField] private float value;

        public StationObjectStat Stat => stat;
        public StationStatModifierMode Mode => mode;
        public float Value => value;
    }

    [Serializable]
    public sealed class EngineeringPartCompatibility
    {
        [SerializeField] private StationSystemType systemType;
        [Tooltip("Empty means every object of the selected system type.")]
        [SerializeField] private string objectId;
        [Tooltip("Must match the StationUpgradeSlot ID on the target prefab.")]
        [SerializeField] private string slotId;
        [SerializeField] private List<StationObjectStatModifierDefinition>
            modifiers = new List<StationObjectStatModifierDefinition>();

        public StationSystemType SystemType => systemType;
        public string ObjectId => Normalize(objectId);
        public string SlotId => Normalize(slotId);
        public IReadOnlyList<StationObjectStatModifierDefinition> Modifiers =>
            modifiers ??
                (IReadOnlyList<StationObjectStatModifierDefinition>)
                    Array.Empty<StationObjectStatModifierDefinition>();

        public bool Matches(
            StationSystemType targetType,
            string targetObjectId,
            string targetSlotId)
        {
            return systemType == targetType &&
                (string.IsNullOrEmpty(ObjectId) ||
                 string.Equals(
                     ObjectId,
                     Normalize(targetObjectId),
                     StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(
                    SlotId,
                    Normalize(targetSlotId),
                    StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string value) =>
            value?.Trim() ?? string.Empty;
    }

    [Serializable]
    public sealed class EngineeringPartDefinition
    {
        [Tooltip("Prefab instantiated dynamically when this part is installed.")]
        [SerializeField] private GameObject installedVisualPrefab;
        [SerializeField] private List<EngineeringPartCompatibility>
            compatibleInstallations = new List<EngineeringPartCompatibility>();

        public GameObject InstalledVisualPrefab => installedVisualPrefab;
        public IReadOnlyList<EngineeringPartCompatibility>
            CompatibleInstallations => compatibleInstallations ??
                (IReadOnlyList<EngineeringPartCompatibility>)
                    Array.Empty<EngineeringPartCompatibility>();

        public EngineeringPartCompatibility Find(
            StationSystemType systemType,
            string objectId,
            string slotId)
        {
            foreach (EngineeringPartCompatibility compatibility in
                     CompatibleInstallations)
            {
                if (compatibility != null &&
                    compatibility.Matches(systemType, objectId, slotId))
                {
                    return compatibility;
                }
            }

            return null;
        }
    }
}
