using System;
using System.Collections.Generic;
using NERA.Quests;

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
        public bool requestedActive;
        public List<StationInstalledPartSaveData> installedParts =
            new List<StationInstalledPartSaveData>();
    }

    [Serializable]
    public sealed class StationInstalledPartSaveData
    {
        public string slotId;
        public string itemId;
    }

    [Serializable]
    public sealed class MaintenanceSaveData
    {
        public string objectId;
        public float condition = 1f;
    }

    [Serializable]
    public sealed class EnemySpawnerWaveSaveData
    {
        public string spawnerId;
        public string waveId;
        public int order;
    }

    [Serializable]
    public sealed class SaveGameData
    {
        public const int CurrentVersion = 21;

        public int version = CurrentVersion;
        public string checkpointSceneName;
        public string checkpointSpawnPointId;
        public bool checkpointUsesWorldPose;
        public float checkpointPositionX;
        public float checkpointPositionY;
        public float checkpointPositionZ;
        public float checkpointRotationX;
        public float checkpointRotationY;
        public float checkpointRotationZ;
        public float checkpointRotationW = 1f;
        public List<string> consumedWorldObjectIds = new List<string>();
        public List<string> defeatedEnemyObjectIds = new List<string>();
        public List<EnemySpawnerWaveSaveData> enemySpawnerWaves =
            new List<EnemySpawnerWaveSaveData>();
        public List<string> completedWorldFlagIds = new List<string>();
        public float completionPercent;
        public int stationPowerState;
        public bool energyStateInitialized;
        public float stationEnergy;
        public bool backupReserveStateInitialized;
        public float stationBackupReserve;
        public bool energyGridEnabled;
        public bool hasDroneBatteryCharge;
        public float droneBatteryCharge;
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
        public List<QuestInstanceSaveData> activeQuests =
            new List<QuestInstanceSaveData>();
        public List<QuestHistorySaveData> questHistory =
            new List<QuestHistorySaveData>();
        public List<QuestActivationSaveData> pendingQuestActivations =
            new List<QuestActivationSaveData>();
        public List<MaintenanceSaveData> maintenanceObjects =
            new List<MaintenanceSaveData>();
    }
}
