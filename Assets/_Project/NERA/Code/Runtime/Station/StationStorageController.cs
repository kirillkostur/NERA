using System;
using System.Collections.Generic;
using NERA.Inventory;
using NERA.Items;
using UnityEngine;

namespace NERA.Station
{
    public sealed class StationStorageController : MonoBehaviour
    {
        [Header("Authored StorageScreen capacities")]
        [SerializeField, Min(1)] private int backpackCapacity = 16;
        [SerializeField, Min(1)] private int quickAccessCapacity = 16;
        [SerializeField, Min(1)] private int anomalyCapacity = 16;

        [SerializeField] private List<ItemInstance> backpackSlots =
            new List<ItemInstance>();
        [SerializeField] private List<ItemInstance> quickAccessSlots =
            new List<ItemInstance>();
        [SerializeField] private List<ItemInstance> anomalySlots =
            new List<ItemInstance>();

        public static StationStorageController Instance { get; private set; }
        public event Action StorageChanged;

        public int Capacity => backpackCapacity + quickAccessCapacity + anomalyCapacity;
        public int Count => CountOccupied(backpackSlots) +
            CountOccupied(quickAccessSlots) + CountOccupied(anomalySlots);
        public IReadOnlyList<ItemInstance> BackpackSlots => backpackSlots;
        public IReadOnlyList<ItemInstance> QuickAccessSlots => quickAccessSlots;
        public IReadOnlyList<ItemInstance> AnomalySlots => anomalySlots;

        public void ConfigureCapacities(
            int backpack,
            int quickAccess,
            int anomaly)
        {
            backpackCapacity = Mathf.Max(1, backpack);
            quickAccessCapacity = Mathf.Max(1, quickAccess);
            anomalyCapacity = Mathf.Max(1, anomaly);
            EnsureCapacities();
            StorageChanged?.Invoke();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            EnsureCapacities();
        }

        public IReadOnlyList<ItemInstance> GetSlots(InventorySlotGroup group)
        {
            EnsureCapacities();
            return GetMutableSlots(group);
        }

        public bool Deposit(ItemInstance instance)
        {
            if (instance?.ItemData == null)
                return false;

            InventorySlotGroup group = PlayerInventory.GetSlotGroup(
                instance.ItemData.ItemType);
            List<ItemInstance> destination = GetMutableSlots(group);
            int empty = FindEmptySlot(destination);
            if (empty < 0)
                return false;

            destination[empty] = instance;
            StorageChanged?.Invoke();
            return true;
        }

        public bool DepositFrom(
            PlayerInventory inventory,
            InventorySlotGroup group,
            int index)
        {
            if (inventory == null)
                return false;

            ItemInstance source = inventory.GetItemInstance(group, index);
            if (source?.ItemData == null ||
                PlayerInventory.GetSlotGroup(source.ItemData.ItemType) != group ||
                FindEmptySlot(GetMutableSlots(group)) < 0 ||
                !inventory.RemoveInstanceAt(group, index, out source))
            {
                return false;
            }

            if (Deposit(source))
                return true;

            inventory.TrySetInstanceAt(group, index, source);
            return false;
        }

        public int DepositAll(PlayerInventory inventory)
        {
            if (inventory == null)
                return 0;

            int moved = 0;
            moved += DepositGroup(inventory, InventorySlotGroup.Backpack);
            moved += DepositGroup(inventory, InventorySlotGroup.QuickAccess);
            moved += DepositGroup(inventory, InventorySlotGroup.Anomaly);
            return moved;
        }

        public int DepositBackpack(PlayerInventory inventory)
        {
            return DepositGroup(inventory, InventorySlotGroup.Backpack);
        }

        public bool WithdrawTo(
            InventorySlotGroup group,
            int index,
            PlayerInventory inventory)
        {
            List<ItemInstance> source = GetMutableSlots(group);
            if (inventory == null || index < 0 || index >= source.Count ||
                source[index]?.ItemData == null)
            {
                return false;
            }

            ItemInstance instance = source[index];
            if (PlayerInventory.GetSlotGroup(instance.ItemData.ItemType) != group ||
                !inventory.AddItem(instance))
            {
                return false;
            }

            source[index] = null;
            StorageChanged?.Invoke();
            return true;
        }

        public bool MoveFromInventory(
            PlayerInventory inventory,
            InventorySlotGroup sourceGroup,
            int sourceIndex,
            InventorySlotGroup destinationGroup,
            int destinationIndex)
        {
            if (inventory == null || sourceGroup != destinationGroup)
                return false;

            List<ItemInstance> destination = GetMutableSlots(destinationGroup);
            ItemInstance moving = inventory.GetItemInstance(sourceGroup, sourceIndex);
            if (moving?.ItemData == null ||
                destinationIndex < 0 || destinationIndex >= destination.Count ||
                PlayerInventory.GetSlotGroup(moving.ItemData.ItemType) != destinationGroup)
            {
                return false;
            }

            ItemInstance replaced = destination[destinationIndex];
            if (!inventory.RemoveInstanceAt(sourceGroup, sourceIndex, out moving))
                return false;

            if (replaced != null &&
                !inventory.TrySetInstanceAt(sourceGroup, sourceIndex, replaced))
            {
                inventory.TrySetInstanceAt(sourceGroup, sourceIndex, moving);
                return false;
            }

            destination[destinationIndex] = moving;
            StorageChanged?.Invoke();
            return true;
        }

        public bool MoveToInventory(
            InventorySlotGroup sourceGroup,
            int sourceIndex,
            PlayerInventory inventory,
            InventorySlotGroup destinationGroup,
            int destinationIndex)
        {
            if (inventory == null || sourceGroup != destinationGroup)
                return false;

            List<ItemInstance> source = GetMutableSlots(sourceGroup);
            if (sourceIndex < 0 || sourceIndex >= source.Count ||
                source[sourceIndex]?.ItemData == null)
            {
                return false;
            }

            if (!inventory.TryReplaceInstanceAt(
                    destinationGroup,
                    destinationIndex,
                    source[sourceIndex],
                    out ItemInstance replaced))
            {
                return false;
            }

            source[sourceIndex] = replaced;
            StorageChanged?.Invoke();
            return true;
        }

        public bool MoveWithinStorage(
            InventorySlotGroup sourceGroup,
            int sourceIndex,
            InventorySlotGroup destinationGroup,
            int destinationIndex)
        {
            if (sourceGroup != destinationGroup)
                return false;

            List<ItemInstance> slots = GetMutableSlots(sourceGroup);
            if (sourceIndex < 0 || sourceIndex >= slots.Count ||
                destinationIndex < 0 || destinationIndex >= slots.Count ||
                sourceIndex == destinationIndex ||
                slots[sourceIndex]?.ItemData == null)
            {
                return false;
            }

            (slots[sourceIndex], slots[destinationIndex]) =
                (slots[destinationIndex], slots[sourceIndex]);
            StorageChanged?.Invoke();
            return true;
        }

        public int CountItem(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return 0;

            return CountItem(backpackSlots, itemId) +
                CountItem(quickAccessSlots, itemId) +
                CountItem(anomalySlots, itemId);
        }

        public bool TryRemoveOne(string itemId, out ItemInstance removed)
        {
            removed = null;
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            return TryRemoveOne(backpackSlots, itemId, out removed) ||
                TryRemoveOne(quickAccessSlots, itemId, out removed) ||
                TryRemoveOne(anomalySlots, itemId, out removed);
        }

        public void RestoreGroups(
            IReadOnlyList<ItemInstance> backpack,
            IReadOnlyList<ItemInstance> quickAccess,
            IReadOnlyList<ItemInstance> anomalies)
        {
            EnsureCapacities();
            RestoreGroup(backpackSlots, backpack, InventorySlotGroup.Backpack);
            RestoreGroup(quickAccessSlots, quickAccess, InventorySlotGroup.QuickAccess);
            RestoreGroup(anomalySlots, anomalies, InventorySlotGroup.Anomaly);
            StorageChanged?.Invoke();
        }

        public void RestoreLegacy(IReadOnlyList<ItemInstance> restoredSlots)
        {
            ClearAll();
            if (restoredSlots != null)
            {
                foreach (ItemInstance instance in restoredSlots)
                {
                    if (instance?.ItemData != null)
                        DepositWithoutNotification(instance);
                }
            }
            StorageChanged?.Invoke();
        }

        public void ResetStorage()
        {
            RestoreGroups(
                Array.Empty<ItemInstance>(),
                Array.Empty<ItemInstance>(),
                Array.Empty<ItemInstance>());
        }

        private int DepositGroup(PlayerInventory inventory, InventorySlotGroup group)
        {
            IReadOnlyList<ItemInstance> source = group switch
            {
                InventorySlotGroup.Anomaly => inventory.AnomalyItemInstances,
                InventorySlotGroup.QuickAccess => inventory.QuickAccessItemInstances,
                _ => inventory.BackpackItemInstances
            };

            int moved = 0;
            for (int index = source.Count - 1; index >= 0; index--)
            {
                if (FindEmptySlot(GetMutableSlots(group)) < 0)
                    break;

                if (inventory.GetItemInstance(group, index) != null &&
                    DepositFrom(inventory, group, index))
                {
                    moved++;
                }
            }
            return moved;
        }

        private List<ItemInstance> GetMutableSlots(InventorySlotGroup group)
        {
            EnsureCapacities();
            return group switch
            {
                InventorySlotGroup.Anomaly => anomalySlots,
                InventorySlotGroup.QuickAccess => quickAccessSlots,
                _ => backpackSlots
            };
        }

        private bool DepositWithoutNotification(ItemInstance instance)
        {
            InventorySlotGroup group = PlayerInventory.GetSlotGroup(
                instance.ItemData.ItemType);
            List<ItemInstance> destination = GetMutableSlots(group);
            int empty = FindEmptySlot(destination);
            if (empty < 0)
                return false;
            destination[empty] = instance;
            return true;
        }

        private bool TryRemoveOne(
            List<ItemInstance> slots,
            string itemId,
            out ItemInstance removed)
        {
            removed = null;
            for (int index = 0; index < slots.Count; index++)
            {
                ItemInstance instance = slots[index];
                if (instance?.ItemData != null &&
                    string.Equals(instance.ItemData.ItemId, itemId, StringComparison.Ordinal))
                {
                    removed = instance;
                    slots[index] = null;
                    StorageChanged?.Invoke();
                    return true;
                }
            }
            return false;
        }

        private static int CountItem(
            IReadOnlyList<ItemInstance> slots,
            string itemId)
        {
            int count = 0;
            foreach (ItemInstance instance in slots)
            {
                if (instance?.ItemData != null &&
                    string.Equals(instance.ItemData.ItemId, itemId, StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        private static int CountOccupied(IReadOnlyList<ItemInstance> slots)
        {
            int count = 0;
            foreach (ItemInstance instance in slots)
            {
                if (instance?.ItemData != null)
                    count++;
            }
            return count;
        }

        private static int FindEmptySlot(IReadOnlyList<ItemInstance> slots)
        {
            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index]?.ItemData == null)
                    return index;
            }
            return -1;
        }

        private void EnsureCapacities()
        {
            SanitizeSlots(backpackSlots);
            SanitizeSlots(quickAccessSlots);
            SanitizeSlots(anomalySlots);
            EnsureCapacity(backpackSlots, backpackCapacity);
            EnsureCapacity(quickAccessSlots, quickAccessCapacity);
            EnsureCapacity(anomalySlots, anomalyCapacity);
        }

        private static void SanitizeSlots(List<ItemInstance> slots)
        {
            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index]?.ItemData == null)
                    slots[index] = null;
            }
        }

        private static void EnsureCapacity(List<ItemInstance> slots, int capacity)
        {
            capacity = Mathf.Max(1, capacity);
            while (slots.Count < capacity)
                slots.Add(null);
            while (slots.Count > capacity && slots[slots.Count - 1] == null)
                slots.RemoveAt(slots.Count - 1);
        }

        private void ClearAll()
        {
            Clear(backpackSlots);
            Clear(quickAccessSlots);
            Clear(anomalySlots);
        }

        private static void Clear(List<ItemInstance> slots)
        {
            for (int index = 0; index < slots.Count; index++)
                slots[index] = null;
        }

        private static void RestoreGroup(
            List<ItemInstance> destination,
            IReadOnlyList<ItemInstance> source,
            InventorySlotGroup requiredGroup)
        {
            Clear(destination);
            if (source == null)
                return;

            int count = Mathf.Min(destination.Count, source.Count);
            for (int index = 0; index < count; index++)
            {
                ItemInstance instance = source[index];
                if (instance?.ItemData == null ||
                    PlayerInventory.GetSlotGroup(instance.ItemData.ItemType) != requiredGroup)
                {
                    continue;
                }

                destination[index] = instance;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
