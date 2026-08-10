using NERA.Items;

namespace NERA.Station
{
    public readonly struct StationInstalledPartState
    {
        public StationInstalledPartState(string slotId, string itemId)
        {
            SlotId = slotId?.Trim() ?? string.Empty;
            ItemId = itemId?.Trim() ?? string.Empty;
        }

        public string SlotId { get; }
        public string ItemId { get; }
    }

    public readonly struct StationPartInstallRequest
    {
        public StationPartInstallRequest(string slotId, ItemData item)
        {
            SlotId = slotId?.Trim() ?? string.Empty;
            Item = item;
        }

        public string SlotId { get; }
        public ItemData Item { get; }
    }
}
