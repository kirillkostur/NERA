using System;
using System.Collections.Generic;
using NERA.Items;
using UnityEngine;

namespace NERA.Inventory
{
    public enum InventorySlotGroup
    {
        Backpack,
        Anomaly,
        QuickAccess
    }

    public sealed class PlayerInventory : MonoBehaviour
    {
        public const int AnomalyCapacity = 3;
        public const int QuickAccessCapacity = 5;
        public const int ActiveQuickAccessStartIndex = 1;
        public const int ActiveQuickAccessCapacity = 3;

        [SerializeField] private InventoryConfig config;
        [SerializeField] private List<ItemData> backpackSlots =
            new List<ItemData>(InventoryConfig.DefaultBackpackCapacity);
        [SerializeField] private List<ItemData> anomalySlots = new List<ItemData>(AnomalyCapacity);
        [SerializeField] private List<ItemData> quickAccessSlots = new List<ItemData>(QuickAccessCapacity);

        public event Action<ItemData> ItemAdded;
        public event Action<ItemData> ItemRemoved;
        public event Action InventoryChanged;

        public IEnumerable<ItemData> Items
        {
            get
            {
                foreach (ItemData item in backpackSlots) yield return item;
                foreach (ItemData item in anomalySlots) yield return item;
                foreach (ItemData item in quickAccessSlots) yield return item;
            }
        }

        public IReadOnlyList<ItemData> BackpackSlots => backpackSlots;
        public IReadOnlyList<ItemData> AnomalySlots => anomalySlots;
        public IReadOnlyList<ItemData> QuickAccessSlots => quickAccessSlots;
        public InventoryConfig Config => config;
        public int BackpackCapacity => config != null
            ? config.BackpackCapacity
            : InventoryConfig.DefaultBackpackCapacity;
        public int Count
        {
            get
            {
                int count = 0;
                foreach (ItemData item in Items)
                {
                    if (item != null)
                        count++;
                }
                return count;
            }
        }

        private void Awake()
        {
            config = InventoryConfig.Resolve(config);
            EnsureCapacity(backpackSlots, BackpackCapacity);
            EnsureCapacity(anomalySlots, AnomalyCapacity);
            EnsureCapacity(quickAccessSlots, QuickAccessCapacity);
        }

        public void Configure(InventoryConfig inventoryConfig)
        {
            config = InventoryConfig.Resolve(inventoryConfig);
            EnsureCapacity(backpackSlots, BackpackCapacity);
            EnsureCapacity(anomalySlots, AnomalyCapacity);
            EnsureCapacity(quickAccessSlots, QuickAccessCapacity);
            InventoryChanged?.Invoke();
        }

        public bool AddItem(ItemData item)
        {
            if (item == null)
                return false;

            InventorySlotGroup group = GetSlotGroup(item.ItemType);
            List<ItemData> slots = GetSlots(group);
            int emptySlot = group == InventorySlotGroup.QuickAccess
                ? FindEmptyQuickAccessSlot(slots)
                : FindEmptySlot(slots);
            if (emptySlot < 0)
            {
                Debug.Log($"PlayerInventory: No free {group} slot for '{item.DisplayName}'.", this);
                return false;
            }

            slots[emptySlot] = item;
            ItemAdded?.Invoke(item);
            InventoryChanged?.Invoke();

            Debug.Log(
                $"PlayerInventory: Added '{item.DisplayName}' to {group} slot {emptySlot + 1}.",
                this
            );

            return true;
        }

        public bool Contains(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            foreach (ItemData item in Items)
            {
                if (item != null &&
                    string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public bool RemoveItem(ItemData item)
        {
            if (item == null)
                return false;

            InventorySlotGroup group = GetSlotGroup(item.ItemType);
            List<ItemData> slots = GetSlots(group);
            int slotIndex = slots.IndexOf(item);
            if (slotIndex < 0)
                return false;

            return RemoveItemAt(group, slotIndex, out _);
        }

        public bool RemoveItemAt(
            InventorySlotGroup group,
            int index,
            out ItemData removedItem
        )
        {
            removedItem = null;
            List<ItemData> slots = GetSlots(group);
            if (index < 0 || index >= slots.Count || slots[index] == null)
                return false;

            removedItem = slots[index];
            slots[index] = null;

            ItemRemoved?.Invoke(removedItem);
            InventoryChanged?.Invoke();

            Debug.Log(
                $"PlayerInventory: Removed '{removedItem.DisplayName}' from {group} slot {index + 1}.",
                this
            );

            return true;
        }

        public void RestoreItems(IEnumerable<ItemData> restoredItems)
        {
            EnsureCapacity(backpackSlots, BackpackCapacity);
            EnsureCapacity(anomalySlots, AnomalyCapacity);
            EnsureCapacity(quickAccessSlots, QuickAccessCapacity);
            ClearSlots(backpackSlots);
            ClearSlots(anomalySlots);
            ClearSlots(quickAccessSlots);

            if (restoredItems == null)
                return;

            foreach (ItemData item in restoredItems)
                AddItem(item);

            InventoryChanged?.Invoke();
        }

        public void RestoreSlots(
            IReadOnlyList<ItemData> restoredBackpack,
            IReadOnlyList<ItemData> restoredAnomalies,
            IReadOnlyList<ItemData> restoredQuickAccess
        )
        {
            RestoreSlotGroup(
                backpackSlots,
                restoredBackpack,
                Mathf.Max(BackpackCapacity, restoredBackpack?.Count ?? 0)
            );
            RestoreSlotGroup(
                anomalySlots,
                restoredAnomalies,
                Mathf.Max(AnomalyCapacity, restoredAnomalies?.Count ?? 0)
            );
            RestoreSlotGroup(
                quickAccessSlots,
                restoredQuickAccess,
                Mathf.Max(QuickAccessCapacity, restoredQuickAccess?.Count ?? 0)
            );
            InventoryChanged?.Invoke();
        }

        public ItemData GetItem(InventorySlotGroup group, int index)
        {
            List<ItemData> slots = GetSlots(group);
            return index >= 0 && index < slots.Count ? slots[index] : null;
        }

        public bool DropItem(ItemData item, Vector3 position, Vector3 forward)
        {
            if (item == null)
                return false;

            InventorySlotGroup group = GetSlotGroup(item.ItemType);
            int index = GetSlots(group).IndexOf(item);
            return DropItemAt(group, index, position, forward);
        }

        public bool DropItemAt(
            InventorySlotGroup group,
            int index,
            Vector3 position,
            Vector3 forward
        )
        {
            ItemData item = GetItem(group, index);
            if (item == null)
                return false;

            if (item.WorldPrefab == null)
            {
                Debug.LogError(
                    $"PlayerInventory: '{item.DisplayName}' has no World Prefab and cannot be dropped.",
                    item
                );
                return false;
            }

            if (!RemoveItemAt(group, index, out _))
                return false;

            Vector3 spawnPosition =
                position + Vector3.up * 0.3f + forward.normalized * 1.1f;
            WorldItem worldItem = Instantiate(
                item.WorldPrefab,
                spawnPosition,
                Quaternion.identity
            );
            worldItem.name = $"Dropped_{item.DisplayName}";
            worldItem.Initialize(item);
            return true;
        }

        public bool TryMoveItem(
            InventorySlotGroup sourceGroup,
            int sourceIndex,
            InventorySlotGroup destinationGroup,
            int destinationIndex
        )
        {
            List<ItemData> sourceSlots = GetSlots(sourceGroup);
            List<ItemData> destinationSlots = GetSlots(destinationGroup);

            if (sourceIndex < 0 || sourceIndex >= sourceSlots.Count ||
                destinationIndex < 0 || destinationIndex >= destinationSlots.Count ||
                (sourceGroup == destinationGroup && sourceIndex == destinationIndex))
            {
                return false;
            }

            ItemData sourceItem = sourceSlots[sourceIndex];
            if (sourceItem == null ||
                GetSlotGroup(sourceItem.ItemType) != destinationGroup)
            {
                return false;
            }

            ItemData destinationItem = destinationSlots[destinationIndex];
            if (destinationItem != null &&
                GetSlotGroup(destinationItem.ItemType) != sourceGroup)
            {
                return false;
            }

            sourceSlots[sourceIndex] = destinationItem;
            destinationSlots[destinationIndex] = sourceItem;

            InventoryChanged?.Invoke();
            return true;
        }

        public bool TrySetItemAt(
            InventorySlotGroup group,
            int index,
            ItemData item
        )
        {
            if (item == null || GetSlotGroup(item.ItemType) != group)
                return false;

            List<ItemData> slots = GetSlots(group);
            if (index < 0 || index >= slots.Count)
                return false;

            slots[index] = item;
            ItemAdded?.Invoke(item);
            InventoryChanged?.Invoke();
            return true;
        }

        public bool TryReplaceItemAt(
            InventorySlotGroup group,
            int index,
            ItemData newItem,
            out ItemData replacedItem
        )
        {
            replacedItem = null;
            if (newItem == null || GetSlotGroup(newItem.ItemType) != group)
                return false;

            List<ItemData> slots = GetSlots(group);
            if (index < 0 || index >= slots.Count)
                return false;

            replacedItem = slots[index];
            slots[index] = newItem;

            ItemAdded?.Invoke(newItem);
            if (replacedItem != null)
                ItemRemoved?.Invoke(replacedItem);
            InventoryChanged?.Invoke();
            return true;
        }

        public static bool IsActiveQuickAccessSlot(int index)
        {
            return index >= ActiveQuickAccessStartIndex &&
                   index < ActiveQuickAccessStartIndex + ActiveQuickAccessCapacity;
        }

        private static void EnsureCapacity(List<ItemData> slots, int capacity)
        {
            if (slots == null)
                return;

            while (slots.Count < capacity)
                slots.Add(null);

            while (slots.Count > capacity && slots[slots.Count - 1] == null)
                slots.RemoveAt(slots.Count - 1);
        }

        private static void ClearSlots(List<ItemData> slots)
        {
            for (int i = 0; i < slots.Count; i++)
                slots[i] = null;
        }

        private static void RestoreSlotGroup(
            List<ItemData> destination,
            IReadOnlyList<ItemData> source,
            int capacity
        )
        {
            EnsureCapacity(destination, capacity);
            ClearSlots(destination);

            if (source == null)
                return;

            int count = Mathf.Min(source.Count, destination.Count);
            for (int i = 0; i < count; i++)
                destination[i] = source[i];
        }

        private static int FindEmptySlot(List<ItemData> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null)
                    return i;
            }
            return -1;
        }

        private static int FindEmptyQuickAccessSlot(List<ItemData> slots)
        {
            int activeEnd = Mathf.Min(
                ActiveQuickAccessStartIndex + ActiveQuickAccessCapacity,
                slots.Count
            );
            for (int i = ActiveQuickAccessStartIndex; i < activeEnd; i++)
            {
                if (slots[i] == null)
                    return i;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (!IsActiveQuickAccessSlot(i) && slots[i] == null)
                    return i;
            }

            return -1;
        }

        private List<ItemData> GetSlots(InventorySlotGroup group)
        {
            switch (group)
            {
                case InventorySlotGroup.Anomaly:
                    EnsureCapacity(anomalySlots, AnomalyCapacity);
                    return anomalySlots;
                case InventorySlotGroup.QuickAccess:
                    EnsureCapacity(quickAccessSlots, QuickAccessCapacity);
                    return quickAccessSlots;
                default:
                    EnsureCapacity(backpackSlots, BackpackCapacity);
                    return backpackSlots;
            }
        }

        public static InventorySlotGroup GetSlotGroup(ItemType itemType)
        {
            if (itemType == ItemType.Anomaly)
                return InventorySlotGroup.Anomaly;
            if (itemType == ItemType.Equipment)
                return InventorySlotGroup.QuickAccess;
            return InventorySlotGroup.Backpack;
        }
    }
}
