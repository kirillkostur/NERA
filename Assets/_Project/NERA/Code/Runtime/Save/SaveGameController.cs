using System;
using System.Collections.Generic;
using NERA.Antenna;
using NERA.Core;
using NERA.Drone;
using System.IO;
using NERA.Expeditions;
using NERA.Energy;
using NERA.Inventory;
using NERA.Items;
using NERA.Library;
using NERA.Maintenance;
using NERA.Quests;
using NERA.Research;
using NERA.Station;
using UnityEngine;

namespace NERA.Save
{
    public sealed class SaveGameController : MonoBehaviour
    {
        [SerializeField] private ItemCatalogData itemDatabase;
        [SerializeField] private List<ItemData> itemCatalog = new List<ItemData>();
        [SerializeField, Range(0, SaveSlotStorage.MaxBackupGenerations)]
        private int backupGenerations = 3;

        public static SaveGameController Instance { get; private set; }
        public static string DefaultSavePath =>
            SaveSlotStorage.GetSlotPath(SaveSlotStorage.DefaultSlot);
        public int ActiveSaveSlot { get; private set; } =
            SaveSlotStorage.DefaultSlot;
        public string SavePath => SaveSlotStorage.GetSlotPath(ActiveSaveSlot);
        public string CheckpointPath =>
            SaveSlotStorage.GetCheckpointPath(ActiveSaveSlot);
        public string CheckpointSceneName => checkpointSceneName;
        public string CheckpointSpawnPointId => checkpointSpawnPointId;
        public bool CheckpointUsesWorldPose => checkpointUsesWorldPose;
        public Vector3 CheckpointPosition => checkpointPosition;
        public Quaternion CheckpointRotation => checkpointRotation;
        public bool HasCheckpoint =>
            !string.IsNullOrWhiteSpace(checkpointSceneName) &&
            (checkpointUsesWorldPose ||
             !string.IsNullOrWhiteSpace(checkpointSpawnPointId));
        public bool IsBusy => isLoading || isSaving;

        public event Action SaveStarted;
        public event Action<bool> SaveCompleted;

        private ExpeditionDiscoveryController discovery;
        private StationPowerController stationPower;
        private EnergySystemController energySystem;
        private DroneScanController drone;
        private LaboratoryWorkstationController laboratoryWorkstation;
        private AntennaController antenna;
        private PlayerInventory inventory;
        private ResearchController research;
        private LibraryController library;
        private StationStorageController stationStorage;
        private StationSystemsController stationSystems;
        private QuestController quests;
        private WorldStateController worldState;
        private readonly Dictionary<string, float> maintenanceConditions =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private bool isLoading;
        private bool isSaving;
        private bool sessionInitialized;
        private string checkpointSceneName = string.Empty;
        private string checkpointSpawnPointId = string.Empty;
        private bool checkpointUsesWorldPose;
        private Vector3 checkpointPosition;
        private Quaternion checkpointRotation = Quaternion.identity;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        public void InitializeSession(GameSessionLaunchRequest request)
        {
            if (sessionInitialized)
                return;

            sessionInitialized = true;
            ActiveSaveSlot = request.SaveSlot;
            CacheSystems();
            if (request.Mode == GameLaunchMode.NewGame)
                ClearSave(true);
            else
                Load();
            Subscribe();
        }

        public void SetBackupGenerations(int generations)
        {
            backupGenerations = Mathf.Clamp(
                generations,
                0,
                SaveSlotStorage.MaxBackupGenerations);
        }

        public bool Save()
        {
            if (IsBusy)
                return false;

            bool saved = false;
            isSaving = true;
            SaveStarted?.Invoke();

            try
            {
                CacheSystems();
                SaveGameData data = Capture();
                saved = WriteSaveData(
                    data,
                    SavePath,
                    rotatePrimaryBackups: true,
                    backupCheckpoint: false);
                Debug.Log($"SaveGame: Progress saved to '{SavePath}'.", this);
            }
            catch (Exception exception)
            {
                Debug.LogError($"SaveGame: Could not save progress.\n{exception}", this);
            }

            finally
            {
                isSaving = false;
                SaveCompleted?.Invoke(saved);
            }

            return saved;
        }

        public bool SaveCheckpoint(
            string sceneName,
            string spawnPointId)
        {
            return SaveCheckpointInternal(
                sceneName,
                spawnPointId,
                false,
                Vector3.zero,
                Quaternion.identity);
        }

        public bool SaveCheckpointAtPosition(
            string sceneName,
            string checkpointId,
            Vector3 position,
            Quaternion rotation)
        {
            return SaveCheckpointInternal(
                sceneName,
                checkpointId,
                true,
                position,
                rotation);
        }

        private bool SaveCheckpointInternal(
            string sceneName,
            string checkpointId,
            bool usesWorldPose,
            Vector3 position,
            Quaternion rotation)
        {
            if (IsBusy ||
                string.IsNullOrWhiteSpace(sceneName) ||
                string.IsNullOrWhiteSpace(checkpointId))
            {
                return false;
            }

            checkpointSceneName = sceneName.Trim();
            checkpointSpawnPointId = checkpointId.Trim();
            checkpointUsesWorldPose = usesWorldPose;
            checkpointPosition = position;
            checkpointRotation = NormalizeRotation(rotation);
            bool saved = false;
            isSaving = true;
            SaveStarted?.Invoke();

            try
            {
                CacheSystems();
                SaveGameData data = Capture();
                bool checkpointSaved = WriteSaveData(
                    data,
                    CheckpointPath,
                    rotatePrimaryBackups: false,
                    backupCheckpoint: true);
                bool primarySaved = checkpointSaved && WriteSaveData(
                    data,
                    SavePath,
                    rotatePrimaryBackups: true,
                    backupCheckpoint: false);
                saved = checkpointSaved && primarySaved;
                if (saved)
                {
                    Debug.Log(
                        $"SaveGame: Checkpoint '{checkpointSpawnPointId}' " +
                        $"saved in scene '{checkpointSceneName}'.",
                        this);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"SaveGame: Could not save checkpoint.\n{exception}",
                    this);
            }
            finally
            {
                isSaving = false;
                SaveCompleted?.Invoke(saved);
            }

            return saved;
        }

        public bool Load()
        {
            CacheSystems();
            if (ActiveSaveSlot == SaveSlotStorage.DefaultSlot)
                SaveSlotStorage.TryMigrateLegacySingleSaveToSlotOne();

            Exception lastException = null;
            var candidates = new List<string>(
                SaveSlotStorage.GetLoadCandidates(
                    ActiveSaveSlot,
                    backupGenerations))
            {
                CheckpointPath,
                SaveSlotStorage.GetCheckpointBackupPath(ActiveSaveSlot)
            };
            foreach (string candidatePath in candidates)
            {
                if (!File.Exists(candidatePath))
                    continue;

                try
                {
                    string json = File.ReadAllText(candidatePath);
                    SaveGameData data = JsonUtility.FromJson<SaveGameData>(json);
                    if (data == null)
                        throw new InvalidDataException("Save data is empty.");

                    ApplyLoadedData(data);
                    if (!string.Equals(
                            candidatePath,
                            SavePath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.LogWarning(
                            $"SaveGame: Primary save was unavailable or invalid. " +
                            $"Loaded backup '{candidatePath}'.",
                            this);
                    }
                    else
                    {
                        Debug.Log(
                            $"SaveGame: Progress loaded from '{SavePath}'.",
                            this);
                    }

                    return true;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }
            }

            if (lastException == null)
                Debug.Log("SaveGame: No save file found. Starting a new game.", this);
            else
                Debug.LogError(
                    $"SaveGame: Could not load the primary save or any " +
                    $"backup.\n{lastException}",
                    this);
            return false;
        }

        public bool LoadCheckpoint()
        {
            if (IsBusy)
                return false;

            CacheSystems();
            string[] candidates =
            {
                CheckpointPath,
                SaveSlotStorage.GetCheckpointBackupPath(ActiveSaveSlot)
            };
            Exception lastException = null;
            foreach (string candidatePath in candidates)
            {
                if (!File.Exists(candidatePath))
                    continue;

                try
                {
                    SaveGameData data = JsonUtility.FromJson<SaveGameData>(
                        File.ReadAllText(candidatePath));
                    if (data == null)
                        throw new InvalidDataException(
                            "Checkpoint data is empty.");

                    ApplyLoadedData(data);
                    bool persistedRollback = WriteSaveData(
                        data,
                        SavePath,
                        rotatePrimaryBackups: true,
                        backupCheckpoint: false);
                    if (!persistedRollback)
                        throw new IOException(
                            "Checkpoint loaded but rollback could not be " +
                            "written to the current save.");

                    Debug.Log(
                        $"SaveGame: Restored checkpoint from " +
                        $"'{candidatePath}'.",
                        this);
                    return true;
                }
                catch (Exception exception)
                {
                    lastException = exception;
                }
            }

            Debug.LogError(
                "SaveGame: Could not restore a checkpoint." +
                (lastException != null ? $"\n{lastException}" : string.Empty),
                this);
            return false;
        }

        public void ClearSave(bool resetProgress)
        {
            AutoSaveService autoSave = AutoSaveService.Instance;
            bool wasSuspended = autoSave != null && autoSave.IsSuspended;
            autoSave?.CancelPending();
            autoSave?.SetSuspended(true);
            try
            {
                SaveSlotStorage.DeleteSlot(ActiveSaveSlot);

                if (resetProgress)
                    ResetProgress();

                Debug.Log($"SaveGame: Save file cleared at '{SavePath}'.", this);
            }
            catch (Exception exception)
            {
                Debug.LogError($"SaveGame: Could not clear save.\n{exception}", this);
            }
            finally
            {
                if (!wasSuspended)
                    autoSave?.SetSuspended(false);
            }
        }

        private void ApplyLoadedData(SaveGameData data)
        {
            AutoSaveService autoSave = AutoSaveService.Instance;
            bool wasSuspended = autoSave != null && autoSave.IsSuspended;
            isLoading = true;
            autoSave?.SetSuspended(true);
            try
            {
                Apply(data);
            }
            finally
            {
                isLoading = false;
                if (!wasSuspended)
                    autoSave?.SetSuspended(false);
            }
        }

        private bool WriteSaveData(
            SaveGameData data,
            string destinationPath,
            bool rotatePrimaryBackups,
            bool backupCheckpoint)
        {
            string temporaryPath = destinationPath + ".tmp";
            try
            {
                string json = JsonUtility.ToJson(data, true);
                Directory.CreateDirectory(
                    Path.GetDirectoryName(destinationPath));
                File.WriteAllText(temporaryPath, json);

                if (rotatePrimaryBackups)
                {
                    SaveSlotStorage.RotateBackups(
                        ActiveSaveSlot,
                        backupGenerations);
                }
                else if (backupCheckpoint && File.Exists(destinationPath))
                {
                    File.Copy(
                        destinationPath,
                        SaveSlotStorage.GetCheckpointBackupPath(
                            ActiveSaveSlot),
                        true);
                }

                CommitTemporarySave(temporaryPath, destinationPath);
                return true;
            }
            finally
            {
                DeleteTemporaryFile(temporaryPath);
            }
        }

        private static void CommitTemporarySave(
            string temporaryPath,
            string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                try
                {
                    File.Replace(temporaryPath, destinationPath, null);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    // File.Copy below is the portable fallback.
                }
                catch (IOException)
                {
                    // File.Copy below handles filesystems without Replace.
                }
            }

            File.Copy(temporaryPath, destinationPath, true);
        }

        private static void DeleteTemporaryFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception)
            {
                // A stale .tmp is harmless and will be replaced next save.
            }
        }

        private static Quaternion NormalizeRotation(Quaternion rotation)
        {
            float squareMagnitude =
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w;
            if (squareMagnitude < 0.000001f)
                return Quaternion.identity;

            float inverseMagnitude = 1f / Mathf.Sqrt(squareMagnitude);
            return new Quaternion(
                rotation.x * inverseMagnitude,
                rotation.y * inverseMagnitude,
                rotation.z * inverseMagnitude,
                rotation.w * inverseMagnitude);
        }

        private SaveGameData Capture()
        {
            SaveGameData data = new SaveGameData
            {
                checkpointSceneName = checkpointSceneName,
                checkpointSpawnPointId = checkpointSpawnPointId,
                checkpointUsesWorldPose = checkpointUsesWorldPose,
                checkpointPositionX = checkpointPosition.x,
                checkpointPositionY = checkpointPosition.y,
                checkpointPositionZ = checkpointPosition.z,
                checkpointRotationX = checkpointRotation.x,
                checkpointRotationY = checkpointRotation.y,
                checkpointRotationZ = checkpointRotation.z,
                checkpointRotationW = checkpointRotation.w
            };
            worldState?.Capture(data);

            if (stationPower != null)
                data.stationPowerState = (int)stationPower.State;

            if (energySystem != null)
            {
                data.energyStateInitialized = true;
                data.stationEnergy = energySystem.CurrentEnergy;
                data.backupReserveStateInitialized = true;
                data.stationBackupReserve =
                    energySystem.CurrentBackupReserve;
                data.energyGridEnabled = energySystem.GridEnabled;
            }

            if (drone != null)
            {
                data.hasDroneBatteryCharge = true;
                data.droneBatteryCharge = drone.CurrentBatteryCharge;
            }

            if (antenna != null)
            {
                data.antennaCondition = antenna.Condition;
                data.activeAntennaSignalLocationId = antenna.ActiveSignalId;
                data.activeAntennaSignalMapSlotId =
                    antenna.ActiveSignalMapSlot != null
                        ? antenna.ActiveSignalMapSlot.SlotId
                        : string.Empty;
                data.activeAntennaSignalSectorIndex =
                    antenna.ActiveSignalMapSlot != null
                        ? antenna.ActiveSignalMapSlot.LegacySectorIndex
                        : -1;
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

                foreach (KeyValuePair<StationSystemType, bool> pair in
                    stationSystems.RequestedStates)
                {
                    StationSystemSaveData savedSystem = new StationSystemSaveData
                    {
                        systemType = (int)pair.Key,
                        requestedActive = pair.Value
                    };
                    CaptureInstalledParts(
                        stationSystems.GetInstalledParts(pair.Key, string.Empty),
                        savedSystem.installedParts);
                    data.stationSystems.Add(savedSystem);
                }

                foreach (StationObjectSystemState objectState in
                         stationSystems.ObjectStates)
                {
                    StationSystemSaveData savedObject = new StationSystemSaveData
                    {
                        systemType = (int)objectState.SystemType,
                        objectId = objectState.ObjectId,
                        requestedActive = objectState.RequestedActive
                    };
                    CaptureInstalledParts(
                        objectState.InstalledParts,
                        savedObject.installedParts);
                    data.stationSystems.Add(savedObject);
                }
            }

            if (research != null)
                data.analyzedResearchIds.AddRange(research.AnalyzedResearchIds);

            if (library != null)
            {
                data.unlockedLibraryEntryIds.AddRange(library.UnlockedEntryIds);
                data.knownLibraryItemIds.AddRange(library.KnownItemIds);
            }

            if (quests != null)
            {
                data.activeQuests.AddRange(quests.CaptureActiveQuests());
                data.questHistory.AddRange(quests.CaptureHistory());
                data.pendingQuestActivations.AddRange(
                    quests.CapturePendingActivations());
            }

            foreach (MaintainableObject maintainable in
                     MaintainableObject.ActiveObjects)
            {
                if (maintainable != null &&
                    !string.IsNullOrWhiteSpace(maintainable.ObjectId))
                {
                    maintenanceConditions[maintainable.ObjectId] =
                        maintainable.Condition;
                }
            }

            List<string> maintenanceIds =
                new List<string>(maintenanceConditions.Keys);
            maintenanceIds.Sort(StringComparer.Ordinal);
            foreach (string objectId in maintenanceIds)
            {
                data.maintenanceObjects.Add(new MaintenanceSaveData
                {
                    objectId = objectId,
                    condition = maintenanceConditions[objectId]
                });
            }

            return data;
        }

        private void Apply(SaveGameData data)
        {
            checkpointSceneName = data.checkpointSceneName?.Trim() ??
                string.Empty;
            checkpointSpawnPointId =
                data.checkpointSpawnPointId?.Trim() ?? string.Empty;
            checkpointUsesWorldPose = data.checkpointUsesWorldPose;
            checkpointPosition = new Vector3(
                data.checkpointPositionX,
                data.checkpointPositionY,
                data.checkpointPositionZ);
            checkpointRotation = NormalizeRotation(new Quaternion(
                data.checkpointRotationX,
                data.checkpointRotationY,
                data.checkpointRotationZ,
                data.checkpointRotationW));
            worldState?.Restore(data);

            if (energySystem != null && data.energyStateInitialized)
            {
                if (data.backupReserveStateInitialized)
                {
                    energySystem.RestoreState(
                        data.stationEnergy,
                        data.stationBackupReserve,
                        data.energyGridEnabled);
                }
                else
                {
                    energySystem.RestoreState(
                        data.stationEnergy,
                        data.energyGridEnabled);
                }
            }

            if (stationPower != null &&
                Enum.IsDefined(typeof(StationPowerState), data.stationPowerState))
            {
                stationPower.SetState((StationPowerState)data.stationPowerState);
            }

            antenna?.RestoreCondition(data.antennaCondition);
            antenna?.RestoreSignalState(
                data.activeAntennaSignalLocationId,
                data.activeAntennaSignalMapSlotId,
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
                            StationSystemDefinition definition =
                                stationSystems.GetDefinition(type, saved.objectId);
                            string restoredObjectId =
                                string.IsNullOrWhiteSpace(saved.objectId)
                                    ? definition?.ObjectId ?? string.Empty
                                    : saved.objectId;
                            if (string.IsNullOrWhiteSpace(restoredObjectId))
                            {
                                states[type] = saved.requestedActive;
                                objectStates.Add(new StationObjectSystemState(
                                    type,
                                    string.Empty,
                                    saved.requestedActive,
                                    ResolveInstalledParts(saved.installedParts)));
                            }
                            else
                            {
                                objectStates.Add(new StationObjectSystemState(
                                    type,
                                    restoredObjectId,
                                    saved.requestedActive,
                                    ResolveInstalledParts(saved.installedParts)));
                            }
                        }
                    }
                }
                stationSystems.Restore(states, objectStates);
            }

            if (drone != null)
            {
                if (data.hasDroneBatteryCharge)
                    drone.RestoreBatteryCharge(data.droneBatteryCharge);
                else
                    drone.ResetBatteryCharge();
            }

            RestoreMaintenanceState(data.maintenanceObjects);
            quests?.RestoreProgress(
                data.activeQuests,
                data.questHistory,
                data.pendingQuestActivations);
            SynchronizeQuestStates();
            if (data.version < 14)
                SynchronizeQuestFacts();
        }

        private static void CaptureInstalledParts(
            IReadOnlyList<StationInstalledPartState> source,
            List<StationInstalledPartSaveData> destination)
        {
            if (source == null || destination == null)
                return;

            foreach (StationInstalledPartState part in source)
            {
                destination.Add(new StationInstalledPartSaveData
                {
                    slotId = part.SlotId,
                    itemId = part.ItemId
                });
            }
        }

        private static IReadOnlyList<StationInstalledPartState>
            ResolveInstalledParts(
                IReadOnlyList<StationInstalledPartSaveData> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<StationInstalledPartState>();

            var result = new List<StationInstalledPartState>(source.Count);
            foreach (StationInstalledPartSaveData saved in source)
            {
                if (saved != null &&
                    !string.IsNullOrWhiteSpace(saved.slotId) &&
                    !string.IsNullOrWhiteSpace(saved.itemId))
                {
                    result.Add(new StationInstalledPartState(
                        saved.slotId,
                        saved.itemId));
                }
            }
            return result;
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
                        charge = instance.Charge,
                        isScanned = instance.IsScanned,
                        integratedAnomalyItemId =
                            instance.IntegratedAnomaly?.ItemId,
                        anomalyCharges = instance.AnomalyCharges
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
                    charge = instance.Charge,
                    isScanned = instance.IsScanned,
                    integratedAnomalyItemId =
                        instance.IntegratedAnomaly?.ItemId,
                    anomalyCharges = instance.AnomalyCharges
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
                    ? ItemInstance.Restore(
                        saved.instanceId,
                        item,
                        saved.charge,
                        FindItem(saved.integratedAnomalyItemId),
                        saved.anomalyCharges,
                        saved.isScanned)
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

            return ItemInstance.Restore(
                saved.instanceId,
                item,
                saved.charge,
                FindItem(saved.integratedAnomalyItemId),
                saved.anomalyCharges,
                saved.isScanned);
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
            checkpointSceneName = string.Empty;
            checkpointSpawnPointId = string.Empty;
            checkpointUsesWorldPose = false;
            checkpointPosition = Vector3.zero;
            checkpointRotation = Quaternion.identity;
            worldState?.ResetState();

            if (stationPower != null)
                stationPower.SetState(StationPowerState.Offline);

            energySystem?.ResetForNewGame();
            antenna?.RestoreSignalState(
                string.Empty,
                string.Empty,
                -1,
                Array.Empty<string>());

            if (discovery != null)
                discovery.RestoreDiscovered(Array.Empty<string>());

            if (inventory != null)
                inventory.RestoreItems(Array.Empty<ItemData>());

            laboratoryWorkstation?.RestoreItems(
                Array.Empty<ItemInstance>(),
                Array.Empty<ItemInstance>());
            research?.RestoreLoadedItem(null, inventory);

            stationStorage?.ResetStorage();
            stationSystems?.ResetSystemsForNewGame();
            drone?.ResetBatteryCharge();
            quests?.ResetProgress();

            maintenanceConditions.Clear();
            foreach (MaintainableObject maintainable in
                     MaintainableObject.ActiveObjects)
            {
                maintainable?.ResetToInitialCondition();
            }

            research?.RestoreAnalyzed(Array.Empty<string>());
            library?.RestoreUnlocked(Array.Empty<string>());
            library?.RestoreKnownItems(Array.Empty<string>());
            SynchronizeQuestStates();

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

            if (drone == null)
                drone = GetComponent<DroneScanController>() ??
                    DroneScanController.Instance;

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

            if (quests == null)
                quests = GetComponent<QuestController>();

            if (worldState == null)
                worldState = GetComponent<WorldStateController>();

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
            MaintainableObject.Registered += HandleMaintainableRegistered;
            MaintainableObject.AnyConditionChanged +=
                HandleMaintainableConditionChanged;
        }

        private void Unsubscribe()
        {
            MaintainableObject.Registered -= HandleMaintainableRegistered;
            MaintainableObject.AnyConditionChanged -=
                HandleMaintainableConditionChanged;
        }

        private void HandleMaintainableRegistered(
            MaintainableObject maintainable)
        {
            if (maintainable == null ||
                string.IsNullOrWhiteSpace(maintainable.ObjectId))
            {
                return;
            }

            if (!maintenanceConditions.TryGetValue(
                    maintainable.ObjectId,
                    out float savedCondition))
            {
                maintenanceConditions[maintainable.ObjectId] =
                    maintainable.Condition;
                return;
            }

            maintainable.SetCondition(savedCondition);
        }

        private void HandleMaintainableConditionChanged(
            string objectId,
            float condition)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return;

            maintenanceConditions[objectId.Trim().ToLowerInvariant()] =
                Mathf.Clamp01(condition);
        }

        private void RestoreMaintenanceState(
            IEnumerable<MaintenanceSaveData> savedObjects)
        {
            maintenanceConditions.Clear();
            if (savedObjects == null)
                return;

            foreach (MaintenanceSaveData saved in savedObjects)
            {
                if (saved == null ||
                    string.IsNullOrWhiteSpace(saved.objectId))
                {
                    continue;
                }

                string objectId = saved.objectId.Trim().ToLowerInvariant();
                float condition = Mathf.Clamp01(saved.condition);
                maintenanceConditions[objectId] = condition;
                if (MaintainableObject.TryFind(
                        objectId,
                        out MaintainableObject maintainable))
                {
                    maintainable.SetCondition(condition);
                }
            }
        }

        private void SynchronizeQuestFacts()
        {
            if (quests == null)
                return;

            if (discovery != null)
            {
                foreach (string locationId in discovery.DiscoveredLocationIds)
                {
                    string displayName = discovery.TryGetKnownLocation(
                            locationId,
                            out ExpeditionLocationData location)
                        ? location.DisplayName
                        : locationId;
                    quests.Report(
                        QuestSignalType.LocationDiscovered,
                        locationId,
                        displayName);
                }
            }

            if (inventory != null)
            {
                foreach (ItemData item in inventory.Items)
                {
                    if (item != null)
                    {
                        quests.Report(
                            QuestSignalType.ItemCollected,
                            item.ItemId,
                            item.DisplayName);
                    }
                }
            }

            if (research != null)
            {
                foreach (string researchId in research.AnalyzedResearchIds)
                {
                    quests.Report(
                        QuestSignalType.ResearchAnalyzed,
                        researchId,
                        researchId);
                }
            }
        }

        private void SynchronizeQuestStates()
        {
            if (quests == null)
                return;

            if (discovery != null)
            {
                foreach (string locationId in discovery.DiscoveredLocationIds)
                {
                    string displayName = discovery.TryGetKnownLocation(
                            locationId,
                            out ExpeditionLocationData location)
                        ? location.DisplayName
                        : locationId;
                    quests.SynchronizeState(
                        QuestSignalType.LocationDiscovered,
                        locationId,
                        displayName);
                }
            }

            if (inventory != null)
            {
                Dictionary<string, ItemData> itemsById =
                    new Dictionary<string, ItemData>(StringComparer.Ordinal);
                foreach (ItemData item in inventory.Items)
                {
                    if (item != null &&
                        !string.IsNullOrWhiteSpace(item.ItemId))
                    {
                        itemsById[item.ItemId] = item;
                    }
                }

                foreach (KeyValuePair<string, ItemData> pair in itemsById)
                {
                    quests.SynchronizeState(
                        QuestSignalType.InventoryItemCountChanged,
                        pair.Key,
                        pair.Value.DisplayName,
                        value: inventory.CountItem(pair.Key));
                }
            }

            if (research != null)
            {
                foreach (string researchId in research.AnalyzedResearchIds)
                {
                    quests.SynchronizeState(
                        QuestSignalType.ResearchAnalyzed,
                        researchId,
                        researchId);
                }
            }

            if (stationSystems != null)
            {
                foreach (StationSystemDefinition definition in
                         stationSystems.Config.StationObjects)
                {
                    if (definition == null)
                        continue;

                    string objectId = definition.ObjectId;
                    string targetId = string.IsNullOrWhiteSpace(objectId)
                        ? definition.SystemType.ToString()
                        : objectId;
                    bool active = stationSystems.IsRequestedActive(
                        definition.SystemType,
                        objectId);
                    quests.SynchronizeState(
                        active
                            ? QuestSignalType.StationSystemActivated
                            : QuestSignalType.StationSystemDeactivated,
                        targetId,
                        definition.DisplayName);
                    quests.SynchronizeState(
                        QuestSignalType.StationSystemUpgraded,
                        targetId,
                        definition.DisplayName,
                        value: stationSystems.GetInstalledPartCount(
                            definition.SystemType,
                            objectId));
                }
            }

            if (stationPower != null)
            {
                quests.SynchronizeState(
                    stationPower.IsPowered
                        ? QuestSignalType.StationPowerOnline
                        : QuestSignalType.StationPowerOffline,
                    "station_power",
                    "Station Power");
            }

            if (energySystem != null)
            {
                quests.SynchronizeState(
                    QuestSignalType.EnergyChargeChanged,
                    "station_energy",
                    "Station Energy",
                    value: energySystem.Charge01);
            }

            StationEnvironmentController environment =
                StationEnvironmentController.Instance;
            if (environment != null)
            {
                quests.SynchronizeState(
                    QuestSignalType.WeatherChanged,
                    environment.Weather.ToString().ToLowerInvariant(),
                    environment.Weather.ToString());
            }

            foreach (MaintainableObject maintainable in
                     MaintainableObject.ActiveObjects)
            {
                if (maintainable != null &&
                    !string.IsNullOrWhiteSpace(maintainable.ObjectId))
                {
                    quests.SynchronizeState(
                        QuestSignalType.DeviceConditionBelow,
                        maintainable.ObjectId,
                        maintainable.DisplayName,
                        value: maintainable.Condition);
                    quests.SynchronizeState(
                        QuestSignalType.DeviceConditionRestored,
                        maintainable.ObjectId,
                        maintainable.DisplayName,
                        value: maintainable.Condition);
                }
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();

            if (Instance == this)
                Instance = null;
        }
    }
}
