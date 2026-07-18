using System;
using System.Collections.Generic;
using System.IO;
using NERA.Expeditions;
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

        public static SaveGameController Instance { get; private set; }
        public static string DefaultSavePath =>
            Path.Combine(Application.persistentDataPath, "nera_save.json");
        public string SavePath => Path.Combine(Application.persistentDataPath, fileName);

        private ExpeditionDiscoveryController discovery;
        private StationPowerController stationPower;
        private PlayerInventory inventory;
        private bool isLoading;

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

        public void Save()
        {
            if (isLoading)
                return;

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

            if (discovery != null)
                data.discoveredLocationIds.AddRange(discovery.DiscoveredLocationIds);

            if (inventory != null)
            {
                foreach (ItemData item in inventory.Items)
                {
                    if (item != null && !string.IsNullOrWhiteSpace(item.ItemId))
                        data.inventoryItemIds.Add(item.ItemId);
                }
            }

            return data;
        }

        private void Apply(SaveGameData data)
        {
            if (stationPower != null &&
                Enum.IsDefined(typeof(StationPowerState), data.stationPowerState))
            {
                stationPower.SetState((StationPowerState)data.stationPowerState);
            }

            if (discovery != null)
                discovery.RestoreDiscovered(data.discoveredLocationIds);

            if (inventory != null)
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

        private void ResetProgress()
        {
            isLoading = true;

            if (stationPower != null)
                stationPower.SetState(StationPowerState.Offline);

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

            if (inventory == null)
                inventory = GetComponentInChildren<PlayerInventory>(true);
        }

        private void Subscribe()
        {
            if (discovery != null)
                discovery.LocationDiscovered += HandleProgressChanged;

            if (stationPower != null)
                stationPower.StateChanged += HandleStationPowerChanged;

            if (inventory != null)
                inventory.ItemAdded += HandleItemAdded;
        }

        private void Unsubscribe()
        {
            if (discovery != null)
                discovery.LocationDiscovered -= HandleProgressChanged;

            if (stationPower != null)
                stationPower.StateChanged -= HandleStationPowerChanged;

            if (inventory != null)
                inventory.ItemAdded -= HandleItemAdded;
        }

        private void HandleProgressChanged(string _)
        {
            Save();
        }

        private void HandleStationPowerChanged(StationPowerState _)
        {
            Save();
        }

        private void HandleItemAdded(ItemData _)
        {
            Save();
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
