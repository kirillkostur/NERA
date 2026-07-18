using System;
using System.Collections.Generic;
using System.IO;
using NERA.Expeditions;
using NERA.Energy;
using NERA.Inventory;
using NERA.Items;
using NERA.Station;
using UnityEngine;

namespace NERA.Save
{
    public sealed class SaveGameController : MonoBehaviour
    {
        [SerializeField] private string fileName = "nera_save.json";
        [SerializeField] private List<ItemData> itemCatalog = new List<ItemData>();
        [SerializeField, Min(0.05f)] private float autoSaveDelay = 0.25f;

        public static SaveGameController Instance { get; private set; }
        public static string DefaultSavePath =>
            Path.Combine(Application.persistentDataPath, "nera_save.json");
        public string SavePath => Path.Combine(Application.persistentDataPath, fileName);

        private ExpeditionDiscoveryController discovery;
        private StationPowerController stationPower;
        private EnergySystemController energySystem;
        private PlayerInventory inventory;
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

            if (discovery != null)
                discovery.RestoreDiscovered(data.discoveredLocationIds);

            if (inventory != null)
            {
                if (HasStructuredInventory(data))
                {
                    inventory.RestoreSlots(
                        ResolveSlots(data.backpackSlotItemIds),
                        ResolveSlots(data.anomalySlotItemIds),
                        ResolveSlots(data.quickAccessSlotItemIds)
                    );
                    return;
                }

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

        private static bool HasStructuredInventory(SaveGameData data)
        {
            return data.version >= 3 ||
                (data.backpackSlotItemIds?.Count ?? 0) > 0 ||
                (data.anomalySlotItemIds?.Count ?? 0) > 0 ||
                (data.quickAccessSlotItemIds?.Count ?? 0) > 0;
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

            if (discovery != null)
                discovery.RestoreDiscovered(Array.Empty<string>());

            if (inventory != null)
                inventory.RestoreItems(Array.Empty<ItemData>());

            isLoading = false;
        }

        private ItemData FindItem(string itemId)
        {
            foreach (ItemData item in itemCatalog)
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
            if (discovery == null)
                discovery = GetComponent<ExpeditionDiscoveryController>();

            if (stationPower == null)
                stationPower = GetComponent<StationPowerController>();

            if (energySystem == null)
                energySystem = GetComponent<EnergySystemController>();

            if (inventory == null)
                inventory = GetComponentInChildren<PlayerInventory>(true);
        }

        private void Subscribe()
        {
            if (discovery != null)
                discovery.LocationDiscovered += HandleProgressChanged;

            if (stationPower != null)
                stationPower.StateChanged += HandleStationPowerChanged;

            if (energySystem != null)
                energySystem.StateChanged += HandleEnergyStateChanged;

            if (inventory != null)
                inventory.InventoryChanged += HandleInventoryChanged;
        }

        private void Unsubscribe()
        {
            if (discovery != null)
                discovery.LocationDiscovered -= HandleProgressChanged;

            if (stationPower != null)
                stationPower.StateChanged -= HandleStationPowerChanged;

            if (energySystem != null)
                energySystem.StateChanged -= HandleEnergyStateChanged;

            if (inventory != null)
                inventory.InventoryChanged -= HandleInventoryChanged;
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

        private void HandleInventoryChanged()
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
