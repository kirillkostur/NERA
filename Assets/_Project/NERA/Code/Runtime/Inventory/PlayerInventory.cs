using System;
using System.Collections.Generic;
using NERA.Items;
using UnityEngine;
using UnityEngine.Serialization;

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
        public const int AnomalyCapacity = 4;
        public const int QuickAccessCapacity = 4;
        public const int ActiveQuickAccessStartIndex = 1;
        public const int ActiveQuickAccessCapacity = 3;

        [SerializeField] private InventoryConfig config;
        [SerializeField] private List<ItemInstance> backpackItemInstances = new List<ItemInstance>();
        [SerializeField] private List<ItemInstance> anomalyItemInstances = new List<ItemInstance>();
        [SerializeField] private List<ItemInstance> quickAccessItemInstances = new List<ItemInstance>();

        [SerializeField, HideInInspector, FormerlySerializedAs("backpackSlots")]
        private List<ItemData> legacyBackpackSlots = new List<ItemData>();
        [SerializeField, HideInInspector, FormerlySerializedAs("anomalySlots")]
        private List<ItemData> legacyAnomalySlots = new List<ItemData>();
        [SerializeField, HideInInspector, FormerlySerializedAs("quickAccessSlots")]
        private List<ItemData> legacyQuickAccessSlots = new List<ItemData>();

        public event Action<ItemData> ItemAdded;
        public event Action<ItemData> ItemRemoved;
        public event Action InventoryChanged;

        public IEnumerable<ItemData> Items
        {
            get
            {
                foreach (ItemInstance instance in ItemInstances)
                    yield return instance?.ItemData;
            }
        }

        public IEnumerable<ItemInstance> ItemInstances
        {
            get
            {
                foreach (ItemInstance item in backpackItemInstances) yield return item;
                foreach (ItemInstance item in anomalyItemInstances) yield return item;
                foreach (ItemInstance item in quickAccessItemInstances) yield return item;
            }
        }

        // ItemData views keep existing UI and gameplay integrations source-compatible.
        public IReadOnlyList<ItemData> BackpackSlots => BuildDataView(backpackItemInstances);
        public IReadOnlyList<ItemData> AnomalySlots => BuildDataView(anomalyItemInstances);
        public IReadOnlyList<ItemData> QuickAccessSlots => BuildDataView(quickAccessItemInstances);
        public IReadOnlyList<ItemInstance> BackpackItemInstances => backpackItemInstances;
        public IReadOnlyList<ItemInstance> AnomalyItemInstances => anomalyItemInstances;
        public IReadOnlyList<ItemInstance> QuickAccessItemInstances => quickAccessItemInstances;
        public InventoryConfig Config => config;
        public int BackpackCapacity => config != null
            ? config.BackpackCapacity
            : InventoryConfig.DefaultBackpackCapacity;

        public int Count
        {
            get
            {
                int count = 0;
                foreach (ItemInstance item in ItemInstances)
                {
                    if (item?.ItemData != null)
                        count++;
                }
                return count;
            }
        }

        private void Awake()
        {
            config = InventoryConfig.Resolve(config);
            MigrateLegacySlots();
            EnsureSlotCapacities();
        }

        public void Configure(InventoryConfig inventoryConfig)
        {
            config = InventoryConfig.Resolve(inventoryConfig);
            MigrateLegacySlots();
            EnsureSlotCapacities();
            InventoryChanged?.Invoke();
        }

        public bool AddItem(ItemData item)
        {
            return AddItem(ItemInstance.Create(item));
        }

        public bool AddItem(ItemInstance instance)
        {
            ItemData item = instance?.ItemData;
            if (item == null)
                return false;

            InventorySlotGroup group = GetSlotGroup(item.ItemType);
            List<ItemInstance> slots = GetInstanceSlots(group);
            int emptySlot = group == InventorySlotGroup.QuickAccess
                ? FindEmptyQuickAccessSlot(slots)
                : FindEmptySlot(slots);
            if (emptySlot < 0)
            {
                Debug.Log($"PlayerInventory: No free {group} slot for '{item.DisplayName}'.", this);
                return false;
            }

            slots[emptySlot] = instance;
            ItemAdded?.Invoke(item);
            InventoryChanged?.Invoke();
            return true;
        }

        public bool Contains(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            foreach (ItemInstance instance in ItemInstances)
            {
                if (instance?.ItemData != null &&
                    string.Equals(instance.ItemData.ItemId, itemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        public int CountItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return 0;

            int count = 0;
            foreach (ItemInstance instance in ItemInstances)
            {
                if (instance?.ItemData != null &&
                    string.Equals(instance.ItemData.ItemId, itemId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        public bool TryRemoveOne(string itemId, out ItemInstance removedInstance)
        {
            removedInstance = null;
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            InventorySlotGroup[] groups =
            {
                InventorySlotGroup.Backpack,
                InventorySlotGroup.Anomaly,
                InventorySlotGroup.QuickAccess
            };

            foreach (InventorySlotGroup group in groups)
            {
                List<ItemInstance> slots = GetInstanceSlots(group);
                for (int index = 0; index < slots.Count; index++)
                {
                    ItemInstance instance = slots[index];
                    if (instance?.ItemData != null &&
                        string.Equals(instance.ItemData.ItemId, itemId, StringComparison.Ordinal))
                    {
                        return RemoveInstanceAt(group, index, out removedInstance);
                    }
                }
            }

            return false;
        }

        public bool RemoveItem(ItemData item)
        {
            if (item == null)
                return false;

            InventorySlotGroup group = GetSlotGroup(item.ItemType);
            List<ItemInstance> slots = GetInstanceSlots(group);
            int index = slots.FindIndex(candidate => candidate?.ItemData == item);
            return index >= 0 && RemoveItemAt(group, index, out _);
        }

        public bool RemoveItemAt(InventorySlotGroup group, int index, out ItemData removedItem)
        {
            bool removed = RemoveInstanceAt(group, index, out ItemInstance instance);
            removedItem = instance?.ItemData;
            return removed;
        }

        public bool RemoveInstanceAt(
            InventorySlotGroup group,
            int index,
            out ItemInstance removedInstance
        )
        {
            removedInstance = null;
            List<ItemInstance> slots = GetInstanceSlots(group);
            if (index < 0 || index >= slots.Count || slots[index]?.ItemData == null)
                return false;

            removedInstance = slots[index];
            slots[index] = null;
            ItemRemoved?.Invoke(removedInstance.ItemData);
            InventoryChanged?.Invoke();
            return true;
        }

        public void RestoreItems(IEnumerable<ItemData> restoredItems)
        {
            ClearAllSlots();
            if (restoredItems != null)
            {
                foreach (ItemData item in restoredItems)
                    AddItem(item);
            }
            InventoryChanged?.Invoke();
        }

        public void RestoreItemInstances(IEnumerable<ItemInstance> restoredItems)
        {
            ClearAllSlots();
            if (restoredItems != null)
            {
                foreach (ItemInstance item in restoredItems)
                    AddItem(item);
            }
            InventoryChanged?.Invoke();
        }

        public void RestoreSlots(
            IReadOnlyList<ItemData> backpack,
            IReadOnlyList<ItemData> anomalies,
            IReadOnlyList<ItemData> quickAccess
        )
        {
            RestoreInstanceSlots(
                CreateInstances(backpack),
                CreateInstances(anomalies),
                CreateInstances(quickAccess)
            );
        }

        public void RestoreInstanceSlots(
            IReadOnlyList<ItemInstance> backpack,
            IReadOnlyList<ItemInstance> anomalies,
            IReadOnlyList<ItemInstance> quickAccess
        )
        {
            RestoreSlotGroup(backpackItemInstances, backpack, Mathf.Max(BackpackCapacity, backpack?.Count ?? 0));
            RestoreSlotGroup(anomalyItemInstances, anomalies, Mathf.Max(AnomalyCapacity, anomalies?.Count ?? 0));
            RestoreSlotGroup(quickAccessItemInstances, quickAccess, Mathf.Max(QuickAccessCapacity, quickAccess?.Count ?? 0));
            InventoryChanged?.Invoke();
        }

        public ItemData GetItem(InventorySlotGroup group, int index)
        {
            return GetItemInstance(group, index)?.ItemData;
        }

        public ItemInstance GetItemInstance(InventorySlotGroup group, int index)
        {
            List<ItemInstance> slots = GetInstanceSlots(group);
            return index >= 0 && index < slots.Count ? slots[index] : null;
        }

        public bool TryConsumeCharge(ItemInstance instance, float amount)
        {
            if (instance == null || !ContainsInstance(instance) || !instance.TryConsume(amount))
                return false;

            InventoryChanged?.Invoke();
            return true;
        }

        public void NotifyItemStateChanged(ItemInstance instance)
        {
            if (instance != null && ContainsInstance(instance))
                InventoryChanged?.Invoke();
        }

        public bool DropItem(ItemData item, Vector3 position, Vector3 forward)
        {
            if (item == null)
                return false;

            InventorySlotGroup group = GetSlotGroup(item.ItemType);
            int index = GetInstanceSlots(group).FindIndex(candidate => candidate?.ItemData == item);
            return DropItemAt(group, index, position, forward);
        }

        public bool DropItemAt(
            InventorySlotGroup group,
            int index,
            Vector3 position,
            Vector3 forward
        )
        {
            ItemInstance instance = GetItemInstance(group, index);
            ItemData item = instance?.ItemData;
            if (item == null || item.WorldPrefab == null)
                return false;

            if (!RemoveInstanceAt(group, index, out instance))
                return false;

            Vector3 spawnPosition = position + Vector3.up * 0.3f + forward.normalized * 1.1f;
            WorldItem worldItem = Instantiate(item.WorldPrefab, spawnPosition, Quaternion.identity);
            worldItem.name = $"Dropped_{item.DisplayName}";
            worldItem.Initialize(instance);
            return true;
        }

        public bool TryMoveItem(
            InventorySlotGroup sourceGroup,
            int sourceIndex,
            InventorySlotGroup destinationGroup,
            int destinationIndex
        )
        {
            List<ItemInstance> source = GetInstanceSlots(sourceGroup);
            List<ItemInstance> destination = GetInstanceSlots(destinationGroup);
            if (!IsValidIndex(source, sourceIndex) || !IsValidIndex(destination, destinationIndex) ||
                (sourceGroup == destinationGroup && sourceIndex == destinationIndex))
            {
                return false;
            }

            ItemInstance sourceItem = source[sourceIndex];
            if (sourceItem?.ItemData == null || GetSlotGroup(sourceItem.ItemData.ItemType) != destinationGroup)
                return false;

            ItemInstance destinationItem = destination[destinationIndex];
            if (destinationItem?.ItemData != null &&
                GetSlotGroup(destinationItem.ItemData.ItemType) != sourceGroup)
            {
                return false;
            }

            source[sourceIndex] = destinationItem;
            destination[destinationIndex] = sourceItem;
            InventoryChanged?.Invoke();
            return true;
        }

        public bool TrySetItemAt(InventorySlotGroup group, int index, ItemData item)
        {
            return TrySetInstanceAt(group, index, ItemInstance.Create(item));
        }

        public bool TrySetInstanceAt(InventorySlotGroup group, int index, ItemInstance instance)
        {
            if (instance?.ItemData == null || GetSlotGroup(instance.ItemData.ItemType) != group)
                return false;

            List<ItemInstance> slots = GetInstanceSlots(group);
            if (!IsValidIndex(slots, index))
                return false;

            slots[index] = instance;
            ItemAdded?.Invoke(instance.ItemData);
            InventoryChanged?.Invoke();
            return true;
        }

        public bool TryReplaceItemAt(
            InventorySlotGroup group,
            int index,
            ItemData item,
            out ItemData replacedItem
        )
        {
            bool result = TryReplaceInstanceAt(
                group,
                index,
                ItemInstance.Create(item),
                out ItemInstance replaced
            );
            replacedItem = replaced?.ItemData;
            return result;
        }

        public bool TryReplaceInstanceAt(
            InventorySlotGroup group,
            int index,
            ItemInstance instance,
            out ItemInstance replacedInstance
        )
        {
            replacedInstance = null;
            if (instance?.ItemData == null || GetSlotGroup(instance.ItemData.ItemType) != group)
                return false;

            List<ItemInstance> slots = GetInstanceSlots(group);
            if (!IsValidIndex(slots, index))
                return false;

            replacedInstance = slots[index];
            slots[index] = instance;
            ItemAdded?.Invoke(instance.ItemData);
            if (replacedInstance?.ItemData != null)
                ItemRemoved?.Invoke(replacedInstance.ItemData);
            InventoryChanged?.Invoke();
            return true;
        }

        public static bool IsActiveQuickAccessSlot(int index)
        {
            return index >= ActiveQuickAccessStartIndex &&
                   index < ActiveQuickAccessStartIndex + ActiveQuickAccessCapacity;
        }

        public static InventorySlotGroup GetSlotGroup(ItemType itemType)
        {
            if (itemType == ItemType.Anomaly)
                return InventorySlotGroup.Anomaly;
            if (itemType == ItemType.Equipment)
                return InventorySlotGroup.QuickAccess;
            return InventorySlotGroup.Backpack;
        }

        private bool ContainsInstance(ItemInstance instance)
        {
            return backpackItemInstances.Contains(instance) ||
                   anomalyItemInstances.Contains(instance) ||
                   quickAccessItemInstances.Contains(instance);
        }

        private void MigrateLegacySlots()
        {
            MigrateLegacySlotGroup(legacyBackpackSlots, backpackItemInstances);
            MigrateLegacySlotGroup(legacyAnomalySlots, anomalyItemInstances);
            MigrateLegacySlotGroup(legacyQuickAccessSlots, quickAccessItemInstances);
            legacyBackpackSlots.Clear();
            legacyAnomalySlots.Clear();
            legacyQuickAccessSlots.Clear();
        }

        private static void MigrateLegacySlotGroup(
            IReadOnlyList<ItemData> legacy,
            List<ItemInstance> destination
        )
        {
            if (destination.Count > 0 || legacy == null || legacy.Count == 0)
                return;

            foreach (ItemData item in legacy)
                destination.Add(ItemInstance.Create(item));
        }

        private void EnsureSlotCapacities()
        {
            SanitizeSlots(backpackItemInstances);
            SanitizeSlots(anomalyItemInstances);
            SanitizeSlots(quickAccessItemInstances);
            EnsureCapacity(backpackItemInstances, BackpackCapacity);
            EnsureCapacity(anomalyItemInstances, AnomalyCapacity);
            EnsureCapacity(quickAccessItemInstances, QuickAccessCapacity);
        }

        private void ClearAllSlots()
        {
            EnsureSlotCapacities();
            ClearSlots(backpackItemInstances);
            ClearSlots(anomalyItemInstances);
            ClearSlots(quickAccessItemInstances);
        }

        private List<ItemInstance> GetInstanceSlots(InventorySlotGroup group)
        {
            EnsureSlotCapacities();
            return group switch
            {
                InventorySlotGroup.Anomaly => anomalyItemInstances,
                InventorySlotGroup.QuickAccess => quickAccessItemInstances,
                _ => backpackItemInstances
            };
        }

        private static IReadOnlyList<ItemData> BuildDataView(IReadOnlyList<ItemInstance> slots)
        {
            ItemData[] items = new ItemData[slots.Count];
            for (int i = 0; i < slots.Count; i++)
                items[i] = slots[i]?.ItemData;
            return items;
        }

        private static IReadOnlyList<ItemInstance> CreateInstances(IReadOnlyList<ItemData> items)
        {
            if (items == null)
                return Array.Empty<ItemInstance>();

            ItemInstance[] instances = new ItemInstance[items.Count];
            for (int i = 0; i < items.Count; i++)
                instances[i] = ItemInstance.Create(items[i]);
            return instances;
        }

        private static void EnsureCapacity(List<ItemInstance> slots, int capacity)
        {
            while (slots.Count < capacity)
                slots.Add(null);
            while (slots.Count > capacity && slots[slots.Count - 1] == null)
                slots.RemoveAt(slots.Count - 1);
        }

        private static void ClearSlots(List<ItemInstance> slots)
        {
            for (int i = 0; i < slots.Count; i++)
                slots[i] = null;
        }

        private static void RestoreSlotGroup(
            List<ItemInstance> destination,
            IReadOnlyList<ItemInstance> source,
            int capacity
        )
        {
            EnsureCapacity(destination, capacity);
            ClearSlots(destination);
            if (source == null)
                return;

            int count = Mathf.Min(source.Count, destination.Count);
            for (int i = 0; i < count; i++)
                destination[i] = source[i]?.ItemData != null ? source[i] : null;
        }

        private static void SanitizeSlots(List<ItemInstance> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i]?.ItemData == null)
                    slots[i] = null;
            }
        }

        private static int FindEmptySlot(IReadOnlyList<ItemInstance> slots)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i]?.ItemData == null)
                    return i;
            }
            return -1;
        }

        private static int FindEmptyQuickAccessSlot(IReadOnlyList<ItemInstance> slots)
        {
            int activeEnd = Mathf.Min(ActiveQuickAccessStartIndex + ActiveQuickAccessCapacity, slots.Count);
            for (int i = ActiveQuickAccessStartIndex; i < activeEnd; i++)
            {
                if (slots[i]?.ItemData == null)
                    return i;
            }
            for (int i = 0; i < slots.Count; i++)
            {
                if (!IsActiveQuickAccessSlot(i) && slots[i]?.ItemData == null)
                    return i;
            }
            return -1;
        }

        private static bool IsValidIndex<T>(IReadOnlyList<T> slots, int index)
        {
            return index >= 0 && index < slots.Count;
        }
    }
}
