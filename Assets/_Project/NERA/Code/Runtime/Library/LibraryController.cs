using System;
using System.Collections.Generic;
using NERA.Items;
using UnityEngine;

namespace NERA.Library
{
    public sealed class LibraryController : MonoBehaviour
    {
        public static LibraryController Instance { get; private set; }

        public event Action<string> EntryUnlocked;

        private readonly HashSet<string> unlockedEntryIds = new HashSet<string>();
        private readonly Dictionary<string, LibraryEntryData> entriesById =
            new Dictionary<string, LibraryEntryData>();
        private readonly Dictionary<string, ItemData> itemsById =
            new Dictionary<string, ItemData>();
        private readonly HashSet<string> knownItemIds = new HashSet<string>();

        public IReadOnlyCollection<string> UnlockedEntryIds => unlockedEntryIds;
        public IReadOnlyCollection<LibraryEntryData> Entries => entriesById.Values;
        public IReadOnlyCollection<string> KnownItemIds => knownItemIds;
        public string LastUnlockedEntryId { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            Unlock("station_primer");
        }

        public bool IsUnlocked(LibraryEntryData entry)
        {
            return entry != null && IsUnlocked(entry.EntryId);
        }

        public bool IsUnlocked(string entryId)
        {
            return !string.IsNullOrWhiteSpace(entryId) &&
                   unlockedEntryIds.Contains(entryId);
        }

        public bool Unlock(string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId) || !unlockedEntryIds.Add(entryId))
                return false;

            EntryUnlocked?.Invoke(entryId);
            LastUnlockedEntryId = entryId;
            Debug.Log($"Library: entry unlocked '{entryId}'.", this);
            return true;
        }

        public bool Unlock(LibraryEntryData entry)
        {
            if (entry == null)
                return false;

            Register(entry);
            return Unlock(entry.EntryId);
        }

        public void Register(LibraryEntryData entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.EntryId))
                return;

            entriesById[entry.EntryId] = entry;
        }

        public void RegisterRange(IEnumerable<LibraryEntryData> entries)
        {
            if (entries == null)
                return;

            foreach (LibraryEntryData entry in entries)
                Register(entry);
        }

        public LibraryEntryData GetEntry(string entryId)
        {
            return !string.IsNullOrWhiteSpace(entryId) &&
                   entriesById.TryGetValue(entryId, out LibraryEntryData entry)
                ? entry
                : null;
        }

        public void RegisterItem(ItemData item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
                return;

            itemsById[item.ItemId] = item;
        }

        public void RegisterItems(IEnumerable<ItemData> items)
        {
            if (items == null)
                return;

            foreach (ItemData item in items)
                RegisterItem(item);
        }

        public ItemData GetItem(string itemId)
        {
            return !string.IsNullOrWhiteSpace(itemId) &&
                   itemsById.TryGetValue(itemId, out ItemData item)
                ? item
                : null;
        }

        public List<ItemData> GetKnownItems()
        {
            List<ItemData> knownItems = new List<ItemData>();

            foreach (string itemId in knownItemIds)
            {
                ItemData item = GetItem(itemId);

                if (item != null)
                    knownItems.Add(item);
            }

            return knownItems;
        }

        public bool RegisterKnownItem(ItemData item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId) ||
                item.ResearchDefinition != null ||
                item.ItemType == ItemType.Anomaly)
                return false;

            RegisterItem(item);

            if (!knownItemIds.Add(item.ItemId))
                return false;

            EntryUnlocked?.Invoke(item.ItemId);
            LastUnlockedEntryId = item.ItemId;
            Debug.Log($"Library: known station item registered '{item.ItemId}'.", this);
            return true;
        }

        public bool IsKnownItem(ItemData item) =>
            item != null && knownItemIds.Contains(item.ItemId);

        public void RestoreKnownItems(IEnumerable<string> itemIds)
        {
            knownItemIds.Clear();
            if (itemIds == null) return;
            foreach (string itemId in itemIds)
                if (!string.IsNullOrWhiteSpace(itemId)) knownItemIds.Add(itemId);
        }

        public void RestoreUnlocked(IEnumerable<string> entryIds)
        {
            unlockedEntryIds.Clear();
            LastUnlockedEntryId = null;
            Unlock("station_primer");

            if (entryIds == null)
                return;

            foreach (string entryId in entryIds)
                Unlock(entryId);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
