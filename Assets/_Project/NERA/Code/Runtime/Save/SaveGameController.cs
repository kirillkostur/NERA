using System;
using System.Collections.Generic;
using NERA.Antenna;
using System.IO;
using NERA.Expeditions;
using NERA.Energy;
using NERA.Inventory;
using NERA.Items;
using NERA.Library;
using NERA.Research;
using NERA.Station;
using UnityEngine;

namespace NERA.Save
{
    public sealed class SaveGameController : MonoBehaviour
    {
        [SerializeField] private string fileName = "nera_save.json";
        [SerializeField] private ItemCatalogData itemDatabase;
        [SerializeField] private List<ItemData> itemCatalog = new List<ItemData>();
        [SerializeField, Min(0.05f)] private float autoSaveDelay = 0.25f;

        public static SaveGameController Instance { get; private set; }
        public static string DefaultSavePath =>
            Path.Combine(Application.persistentDataPath, "nera_save.json");
        public string SavePath => Path.Combine(Application.persistentDataPath, fileName);

        private ExpeditionDiscoveryController discovery;
        private StationPowerController stationPower;
        private EnergySystemController energySystem;
        private LaboratoryWorkstationController laboratoryWorkstation;
        private AntennaController antenna;
        private PlayerInventory inventory;
        private ResearchController research;
        private LibraryController library;
        private StationStorageController stationStorage;
        private StationSystemsController stationSystems;
        private bool isLoading;
        private bool autoSavePending;
        private float autoSaveAt;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            CacheSystems();
            Load();
            Subscribe();
        }

        private void Update()
        {
            if (!autoSavePending || Time.unscaledTime < autoSaveAt)
                return;

            Save();
        }

        public void Save()
        {
            if (isLoading)
                return;

            autoSavePending = false;
            CacheSystems();
            SaveGameData data = Capture();
            string json = JsonUtility.ToJson(data, true);
            string temporaryPath = SavePath + ".tmp";

            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(temporaryPath, json);
                File.Copy(temporaryPath, SavePath, true);
                File.Delete(temporaryPath);
                Debug.Log($"SaveGame: Progress saved to '{SavePath}'.", this);
            }
            catch (Exception exception)
            {
                Debug.LogError($"SaveGame: Could not save progress.\n{exception}", this);
            }
        }

        public void Load()
        {
            CacheSystems();

            if (!File.Exists(SavePath))
            {
                Debug.Log("SaveGame: No save file found. Starting a new game.", this);
                return;
            }

            try
            {
                string json = File.ReadAllText(SavePath);
                SaveGameData data = JsonUtility.FromJson<SaveGameData>(json);

                if (data == null)
                    throw new InvalidDataException("Save data is empty.");

                isLoading = true;
                Apply(data);
                Debug.Log($"SaveGame: Progress loaded from '{SavePath}'.", this);
            }
            catch (Exception exception)
            {
                Debug.LogError($"SaveGame: Could not load progress.\n{exception}", this);
            }
            finally
            {
                isLoading = false;
            }
        }

        public void ClearSave(bool resetProgress)
        {
            try
            {
                if (File.Exists(SavePath))
                    File.Delete(SavePath);

                string temporaryPath = SavePath + ".tmp";

                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);

                if (resetProgress)
                    ResetProgress();

                Debug.Log($"SaveGame: Save file cleared at '{SavePath}'.", this);
            }
            catch (Exception exception)
            {
                Debug.LogError($"SaveGame: Could not clear save.\n{exception}", this);
            }
        }

        private SaveGameData Capture()
        {
            SaveGameData data = new SaveGameData();

            if (stationPower != null)
                data.stationPowerState = (int)stationPower.State;

            if (energySystem != null)
            {
                data.energyStateInitialized = true;
                data.stationEnergy = energySystem.CurrentEnergy;
                data.energyGridEnabled = energySystem.GridEnabled;
            }

            if (antenna != null)
            {
                data.antennaCondition = antenna.Condition;
                data.activeAntennaSignalLocationId = antenna.ActiveSignalId;
                data.activeAntennaSignalSectorIndex =
                    antenna.ActiveSignalSectorIndex;
                data.consumedAntennaSignalLocationIds.AddRange(
                    antenna.ConsumedSignalIds
                );
            }

            if (discovery != null)
                data.discoveredLocationIds.AddRange(discovery.DiscoveredLocationIds);

            if (inventory != null)
            {
                foreach (ItemData item in inventory.Items)
                {
                    if (item != null && !string.IsNullOrWhiteSpace(item.ItemId))
                        data.inventoryItemIds.Add(item.ItemId);
                }

                CaptureSlots(
                    inventory.BackpackSlots,
                    data.backpackSlotItemIds
                );
                CaptureSlots(
                    inventory.AnomalySlots,
                    data.anomalySlotItemIds
                );
                CaptureSlots(
                    inventory.QuickAccessSlots,
                    data.quickAccessSlotItemIds
                );
                CaptureInstances(inventory.BackpackItemInstances, data.backpackItems);
                CaptureInstances(inventory.AnomalyItemInstances, data.anomalyItems);
                CaptureInstances(inventory.QuickAccessItemInstances, data.quickAccessItems);
            }

            if (laboratoryWorkstation != null)
            {
                CaptureInstances(
                    laboratoryWorkstation.ChargingItems,
                    data.laboratoryChargingItems);
                CaptureInstances(
                    laboratoryWorkstation.UpgradeItems,
                    data.laboratoryUpgradeItems);
            }

            if (research?.LoadedItemInstance != null)
                data.laboratoryItem = CaptureInstance(research.LoadedItemInstance);

            if (stationStorage != null)
            {
                CaptureInstances(
                    stationStorage.BackpackSlots,
                    data.stationBackpackItems);
                CaptureInstances(
                    stationStorage.QuickAccessSlots,
                    data.stationQuickAccessItems);
                CaptureInstances(
                    stationStorage.AnomalySlots,
                    data.stationAnomalyItems);
            }

            if (stationSystems != null)
            {
                Dictionary<StationSystemType, bool> states =
                    new Dictionary<StationSystemType, bool>();
                foreach (KeyValuePair<StationSystemType, bool> pair in
                    stationSystems.RequestedStates)
                {
                    states[pair.Key] = pair.Value;
                }

                foreach (KeyValuePair<StationSystemType, int> pair in
                    stationSystems.UpgradeLevels)
                {
                    data.stationSystems.Add(new StationSystemSaveData
                    {
                        systemType = (int)pair.Key,
                        upgradeLevel = pair.Value,
                        requestedActive = states.TryGetValue(pair.Key, out bool active) && active
                    });
                }

                foreach (StationObjectSystemState objectState in
                         stationSystems.ObjectStates)
                {
                    data.stationSystems.Add(new StationSystemSaveData
                    {
                        systemType = (int)objectState.SystemType,
                        objectId = objectState.ObjectId,
                        upgradeLevel = objectState.UpgradeLevel,
                        requestedActive = objectState.RequestedActive
                    });
                }
            }

            if (research != null)
                data.analyzedResearchIds.AddRange(research.AnalyzedResearchIds);

            if (library != null)
            {
                data.unlockedLibraryEntryIds.AddRange(library.UnlockedEntryIds);
                data.knownLibraryItemIds.AddRange(library.KnownItemIds);
            }

            return data;
        }

        private void Apply(SaveGameData data)
        {
            if (energySystem != null && data.energyStateInitialized)
            {
                energySystem.RestoreState(
                    data.stationEnergy,
                    data.energyGridEnabled
                );
            }

            if (stationPower != null &&
                Enum.IsDefined(typeof(StationPowerState), data.stationPowerState))
            {
                stationPower.SetState((StationPowerState)data.stationPowerState);
            }

            antenna?.RestoreCondition(data.antennaCondition);
            antenna?.RestoreSignalState(
                data.activeAntennaSignalLocationId,
                data.activeAntennaSignalSectorIndex,
                data.consumedAntennaSignalLocationIds
            );

            if (discovery != null)
                discovery.RestoreDiscovered(data.discoveredLocationIds);

            RegisterResearchLibraryEntries();
            research?.RestoreAnalyzed(data.analyzedResearchIds);
            library?.RestoreUnlocked(data.unlockedLibraryEntryIds);
            library?.RestoreKnownItems(data.knownLibraryItemIds);

            if (inventory != null)
            {
                if (HasInstanceInventory(data))
                {
                    inventory.RestoreInstanceSlots(
                        ResolveInstances(data.backpackItems),
                        ResolveInstances(data.anomalyItems),
                        ResolveInstances(data.quickAccessItems)
                    );
                }
                else if (HasStructuredInventory(data))
                {
                    inventory.RestoreSlots(
                        ResolveSlots(data.backpackSlotItemIds),
                        ResolveSlots(data.anomalySlotItemIds),
                        ResolveSlots(data.quickAccessSlotItemIds)
                    );
                }
                else
                {
                    List<ItemData> restoredItems = new List<ItemData>();

                    foreach (string itemId in data.inventoryItemIds)
                    {
                        ItemData item = FindItem(itemId);

                        if (item != null)
                            restoredItems.Add(item);
                        else
                            Debug.LogWarning($"SaveGame: Unknown item id '{itemId}'.", this);
                    }

                    inventory.RestoreItems(restoredItems);
                }
            }

            if (laboratoryWorkstation != null)
            {
                List<ItemInstance> chargingItems =
                    ResolveInstances(data.laboratoryChargingItems);
                if (chargingItems.Count == 0 &&
                    data.chargingTableItem != null)
                {
                    chargingItems.Add(
                        ResolveInstance(data.chargingTableItem));
                }

                laboratoryWorkstation.RestoreItems(
                    chargingItems,
                    ResolveInstances(data.laboratoryUpgradeItems));
            }
            research?.RestoreLoadedItem(
                ResolveInstance(data.laboratoryItem),
                inventory
            );

            if (stationStorage != null)
            {
                bool hasGroupedStorage = data.version >= 8 ||
                    (data.stationBackpackItems?.Count ?? 0) > 0 ||
                    (data.stationQuickAccessItems?.Count ?? 0) > 0 ||
                    (data.stationAnomalyItems?.Count ?? 0) > 0;
                if (hasGroupedStorage)
                {
                    stationStorage.RestoreGroups(
                        ResolveInstances(data.stationBackpackItems),
                        ResolveInstances(data.stationQuickAccessItems),
                        ResolveInstances(data.stationAnomalyItems));
                }
                else
                {
                    stationStorage.RestoreLegacy(
                        ResolveInstances(data.stationStorageItems));
                }
            }

            if (stationSystems != null)
            {
                Dictionary<StationSystemType, int> levels =
                    new Dictionary<StationSystemType, int>();
                Dictionary<StationSystemType, bool> states =
                    new Dictionary<StationSystemType, bool>();
                List<StationObjectSystemState> objectStates =
                    new List<StationObjectSystemState>();
                if (data.stationSystems != null)
                {
                    foreach (StationSystemSaveData saved in data.stationSystems)
                    {
                        if (saved != null && Enum.IsDefined(
                                typeof(StationSystemType), saved.systemType))
                        {
                            StationSystemType type = (StationSystemType)saved.systemType;
                            if (string.IsNullOrWhiteSpace(saved.objectId))
                            {
                                levels[type] = saved.upgradeLevel;
                                states[type] = saved.requestedActive;
                            }
                            else
                            {
                                objectStates.Add(new StationObjectSystemState(
                                    type,
                                    saved.objectId,
                                    saved.upgradeLevel,
                                    saved.requestedActive));
                            }
                        }
                    }
                }
                stationSystems.Restore(levels, states, objectStates);
            }
        }

        private static bool HasStructuredInventory(SaveGameData data)
        {
            return data.version >= 3 ||
                (data.backpackSlotItemIds?.Count ?? 0) > 0 ||
                (data.anomalySlotItemIds?.Count ?? 0) > 0 ||
                (data.quickAccessSlotItemIds?.Count ?? 0) > 0;
        }

        private static bool HasInstanceInventory(SaveGameData data)
        {
            return data.version >= 5 ||
                (data.backpackItems?.Count ?? 0) > 0 ||
                (data.anomalyItems?.Count ?? 0) > 0 ||
                (data.quickAccessItems?.Count ?? 0) > 0;
        }

        private static void CaptureInstances(
            IReadOnlyList<ItemInstance> source,
            List<InventoryItemSaveData> destination
        )
        {
            foreach (ItemInstance instance in source)
            {
                destination.Add(instance?.ItemData == null
                    ? null
                    : new InventoryItemSaveData
                    {
                        instanceId = instance.InstanceId,
                        itemId = instance.ItemData.ItemId,
                        charge = instance.Charge
                    });
            }
        }

        private static InventoryItemSaveData CaptureInstance(ItemInstance instance)
        {
            return instance?.ItemData == null
                ? null
                : new InventoryItemSaveData
                {
                    instanceId = instance.InstanceId,
                    itemId = instance.ItemData.ItemId,
                    charge = instance.Charge
                };
        }

        private List<ItemInstance> ResolveInstances(
            IReadOnlyList<InventoryItemSaveData> savedItems
        )
        {
            if (savedItems == null)
                return new List<ItemInstance>();

            List<ItemInstance> resolved = new List<ItemInstance>(savedItems.Count);
            foreach (InventoryItemSaveData saved in savedItems)
            {
                if (saved == null || string.IsNullOrWhiteSpace(saved.itemId))
                {
                    resolved.Add(null);
                    continue;
                }

                ItemData item = FindItem(saved.itemId);
                resolved.Add(item != null
                    ? ItemInstance.Restore(saved.instanceId, item, saved.charge)
                    : null);

                if (item == null)
                    Debug.LogWarning($"SaveGame: Unknown item id '{saved.itemId}'.", this);
            }
            return resolved;
        }

        private ItemInstance ResolveInstance(InventoryItemSaveData saved)
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.itemId))
                return null;

            ItemData item = FindItem(saved.itemId);
            if (item == null)
            {
                Debug.LogWarning($"SaveGame: Unknown item id '{saved.itemId}'.", this);
                return null;
            }

            return ItemInstance.Restore(saved.instanceId, item, saved.charge);
        }

        private static void CaptureSlots(
            IReadOnlyList<ItemData> source,
            List<string> destination
        )
        {
            foreach (ItemData item in source)
            {
                destination.Add(
                    item != null && !string.IsNullOrWhiteSpace(item.ItemId)
                        ? item.ItemId
                        : string.Empty
                );
            }
        }

        private List<ItemData> ResolveSlots(IReadOnlyList<string> itemIds)
        {
            if (itemIds == null)
                return new List<ItemData>();

            List<ItemData> resolved = new List<ItemData>(itemIds.Count);

            foreach (string itemId in itemIds)
            {
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    resolved.Add(null);
                    continue;
                }

                ItemData item = FindItem(itemId);
                resolved.Add(item);

                if (item == null)
                    Debug.LogWarning($"SaveGame: Unknown item id '{itemId}'.", this);
            }

            return resolved;
        }

        private void ResetProgress()
        {
            isLoading = true;

            if (stationPower != null)
                stationPower.SetState(StationPowerState.Offline);

            energySystem?.ResetForNewGame();
            antenna?.RestoreCondition(1f);
            antenna?.RestoreSignalState(string.Empty, -1, Array.Empty<string>());

            if (discovery != null)
                discovery.RestoreDiscovered(Array.Empty<string>());

            if (inventory != null)
                inventory.RestoreItems(Array.Empty<ItemData>());

            laboratoryWorkstation?.RestoreItems(
                Array.Empty<ItemInstance>(),
                Array.Empty<ItemInstance>());
            research?.RestoreLoadedItem(null, inventory);

            stationStorage?.ResetStorage();
            stationSystems?.ResetSystems();

            research?.RestoreAnalyzed(Array.Empty<string>());
            library?.RestoreUnlocked(Array.Empty<string>());
            library?.RestoreKnownItems(Array.Empty<string>());

            isLoading = false;
        }

        private ItemData FindItem(string itemId)
        {
            if (itemDatabase != null)
                return itemDatabase.Find(itemId);

            foreach (ItemData item in LegacyCatalogItems)
            {
                if (item != null &&
                    string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private void CacheSystems()
        {
            if (itemDatabase == null)
                itemDatabase = Resources.Load<ItemCatalogData>("ItemCatalog_Default");

            if (discovery == null)
                discovery = GetComponent<ExpeditionDiscoveryController>();

            if (stationPower == null)
                stationPower = GetComponent<StationPowerController>();

            if (energySystem == null)
                energySystem = GetComponent<EnergySystemController>();

            if (laboratoryWorkstation == null)
                laboratoryWorkstation =
                    GetComponent<LaboratoryWorkstationController>();

            if (antenna == null)
                antenna = GetComponent<AntennaController>();

            if (inventory == null)
                inventory = GetComponentInChildren<PlayerInventory>(true);

            if (research == null)
                research = GetComponent<ResearchController>();

            if (library == null)
                library = GetComponent<LibraryController>();

            if (stationStorage == null)
                stationStorage = GetComponent<StationStorageController>();

            if (stationSystems == null)
                stationSystems = GetComponent<StationSystemsController>();

            RegisterResearchLibraryEntries();
        }

        private void RegisterResearchLibraryEntries()
        {
            if (library == null)
                return;

            foreach (ItemData item in CatalogItems)
            {
                library.RegisterItem(item);

                ResearchDefinition definition = item != null
                    ? item.ResearchDefinition
                    : null;

                if (definition?.UnlockedEntry != null)
                    library.Register(definition.UnlockedEntry);
            }
        }

        private IEnumerable<ItemData> CatalogItems =>
            itemDatabase != null ? itemDatabase.Items : LegacyCatalogItems;

        private IEnumerable<ItemData> LegacyCatalogItems =>
            itemCatalog ?? (IEnumerable<ItemData>)Array.Empty<ItemData>();

        private void Subscribe()
        {
            if (discovery != null)
                discovery.LocationDiscovered += HandleProgressChanged;

            if (stationPower != null)
                stationPower.StateChanged += HandleStationPowerChanged;

            if (energySystem != null)
                energySystem.StateChanged += HandleEnergyStateChanged;

            if (antenna != null)
                antenna.ConditionChanged += HandleAntennaConditionChanged;

            if (antenna != null)
                antenna.ActiveSignalChanged += HandleAntennaSignalChanged;

            if (inventory != null)
                inventory.InventoryChanged += HandleInventoryChanged;

            if (research != null)
                research.ResearchAnalyzed += HandleResearchAnalyzed;

            if (research != null)
                research.StateChanged += HandleResearchStateChanged;

            if (laboratoryWorkstation != null)
                laboratoryWorkstation.ItemsChanged +=
                    HandleLaboratoryWorkstationChanged;

            if (library != null)
                library.EntryUnlocked += HandleLibraryEntryUnlocked;

            if (stationStorage != null)
                stationStorage.StorageChanged += HandleStationStorageChanged;

            if (stationSystems != null)
                stationSystems.SystemsChanged += HandleStationSystemsChanged;
        }

        private void Unsubscribe()
        {
            if (discovery != null)
                discovery.LocationDiscovered -= HandleProgressChanged;

            if (stationPower != null)
                stationPower.StateChanged -= HandleStationPowerChanged;

            if (energySystem != null)
                energySystem.StateChanged -= HandleEnergyStateChanged;

            if (antenna != null)
                antenna.ConditionChanged -= HandleAntennaConditionChanged;

            if (antenna != null)
                antenna.ActiveSignalChanged -= HandleAntennaSignalChanged;

            if (inventory != null)
                inventory.InventoryChanged -= HandleInventoryChanged;

            if (research != null)
                research.ResearchAnalyzed -= HandleResearchAnalyzed;

            if (research != null)
                research.StateChanged -= HandleResearchStateChanged;

            if (laboratoryWorkstation != null)
                laboratoryWorkstation.ItemsChanged -=
                    HandleLaboratoryWorkstationChanged;

            if (library != null)
                library.EntryUnlocked -= HandleLibraryEntryUnlocked;

            if (stationStorage != null)
                stationStorage.StorageChanged -= HandleStationStorageChanged;

            if (stationSystems != null)
                stationSystems.SystemsChanged -= HandleStationSystemsChanged;
        }

        private void HandleProgressChanged(string _)
        {
            RequestAutoSave();
        }

        private void HandleStationPowerChanged(StationPowerState _)
        {
            RequestAutoSave();
        }

        private void HandleEnergyStateChanged(EnergyState _)
        {
            RequestAutoSave();
        }

        private void HandleAntennaConditionChanged(float _)
        {
            RequestAutoSave();
        }

        private void HandleAntennaSignalChanged(ExpeditionLocationData _)
        {
            RequestAutoSave();
        }

        private void HandleInventoryChanged()
        {
            RequestAutoSave();
        }

        private void HandleResearchAnalyzed(string _)
        {
            RequestAutoSave();
        }

        private void HandleResearchStateChanged(ResearchController.ResearchState _)
        {
            RequestAutoSave();
        }

        private void HandleLaboratoryWorkstationChanged()
        {
            RequestAutoSave();
        }

        private void HandleLibraryEntryUnlocked(string _)
        {
            RequestAutoSave();
        }

        private void HandleStationStorageChanged()
        {
            RequestAutoSave();
        }

        private void HandleStationSystemsChanged()
        {
            RequestAutoSave();
        }

        private void RequestAutoSave()
        {
            if (isLoading)
                return;

            autoSavePending = true;
            autoSaveAt = Time.unscaledTime + autoSaveDelay;
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        private void OnDestroy()
        {
            Unsubscribe();

            if (Instance == this)
                Instance = null;
        }
    }
}
