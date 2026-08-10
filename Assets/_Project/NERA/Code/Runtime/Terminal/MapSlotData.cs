using UnityEngine;
using NERA.Localization;

namespace NERA.Terminal
{
    [CreateAssetMenu(
        fileName = "MapSlot",
        menuName = "NERA/Terminal Map/Slot"
    )]
    public sealed class MapSlotData : ScriptableObject
    {
        [Tooltip("Stable unique ID used by save games. Do not change after release.")]
        [SerializeField] private string slotId;
        [SerializeField] private string displayName;
        [Tooltip("Only used to migrate saves created before map slots became data-driven.")]
        [SerializeField, HideInInspector] private int legacySectorIndex = -1;

        public string SlotId => slotId?.Trim() ?? string.Empty;
        public string DisplayName => NERALocalization.Content(
            "map_slot",
            SlotId,
            "name",
            string.IsNullOrWhiteSpace(displayName)
                ? SlotId
                : displayName.Trim());
        public int LegacySectorIndex => legacySectorIndex;
    }
}
