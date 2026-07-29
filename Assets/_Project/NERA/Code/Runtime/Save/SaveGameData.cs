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
        public bool isScanned;
        public string integratedAnomalyItemId;
        public int anomalyCharges;
    }

    [Serializable]
    public sealed class StationSystemSaveData
    {
        public int systemType;
        public string objectId;
        public int upgradeLevel;
        public bool requestedActive;
    }

    [Serializable]
    public sealed class SaveGameData
    {
        public int version = 13;
        public int stationPowerState;
        public bool energyStateInitialized;
        public float stationEnergy;
        public bool energyGridEnabled;
        public float antennaCondition = 1f;
        public string activeAntennaSignalLocationId;
        public string activeAntennaSignalMapSlotId;
        // Version 12 and older: migrated through MapSlotData.legacySectorIndex.
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
        public List<InventoryItemSaveData> stationStorageItems = new List<InventoryItemSaveData>();
        public List<InventoryItemSaveData> stationBackpackItems = new List<InventoryItemSaveData>();
        public List<InventoryItemSaveData> stationQuickAccessItems = new List<InventoryItemSaveData>();
        public List<InventoryItemSaveData> stationAnomalyItems = new List<InventoryItemSaveData>();
        public List<StationSystemSaveData> stationSystems = new List<StationSystemSaveData>();
        public List<InventoryItemSaveData> laboratoryChargingItems =
            new List<InventoryItemSaveData>();
        public List<InventoryItemSaveData> laboratoryUpgradeItems =
            new List<InventoryItemSaveData>();
        // Version 9 and older: migrated into laboratoryChargingItems[0].
        public InventoryItemSaveData chargingTableItem;
        public InventoryItemSaveData laboratoryItem;
        public List<string> analyzedResearchIds = new List<string>();
        public List<string> unlockedLibraryEntryIds = new List<string>();
        public List<string> knownLibraryItemIds = new List<string>();
    }
}
