using System;
using System.Collections.Generic;

namespace NERA.Save
{
    [Serializable]
    public sealed class InventoryItemSaveData
    {
        public string instanceId;
        public string itemId;
        public float charge;
    }

    [Serializable]
    public sealed class SaveGameData
    {
        public int version = 6;
        public int stationPowerState;
        public bool energyStateInitialized;
        public float stationEnergy;
        public bool energyGridEnabled;
        public float antennaCondition = 1f;
        public string activeAntennaSignalLocationId;
        public int activeAntennaSignalSectorIndex = -1;
        public List<string> consumedAntennaSignalLocationIds = new List<string>();
        public List<string> discoveredLocationIds = new List<string>();
        public List<string> inventoryItemIds = new List<string>();
        public List<string> backpackSlotItemIds = new List<string>();
        public List<string> anomalySlotItemIds = new List<string>();
        public List<string> quickAccessSlotItemIds = new List<string>();
        public List<InventoryItemSaveData> backpackItems = new List<InventoryItemSaveData>();
        public List<InventoryItemSaveData> anomalyItems = new List<InventoryItemSaveData>();
        public List<InventoryItemSaveData> quickAccessItems = new List<InventoryItemSaveData>();
        public InventoryItemSaveData chargingTableItem;
        public InventoryItemSaveData laboratoryItem;
        public List<string> analyzedResearchIds = new List<string>();
        public List<string> unlockedLibraryEntryIds = new List<string>();
        public List<string> knownLibraryItemIds = new List<string>();
    }
}
