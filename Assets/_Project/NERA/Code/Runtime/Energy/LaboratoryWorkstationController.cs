using System;
using System.Collections.Generic;
using NERA.Inventory;
using NERA.Items;
using NERA.Station;
using UnityEngine;

namespace NERA.Energy
{
    public sealed class LaboratoryWorkstationController : MonoBehaviour
    {
        private const string ChargingConsumerId = "laboratory_charging";

        public const int ChargingSlotCapacity = 4;
        public const int UpgradeSlotCapacity = 2;

        public static LaboratoryWorkstationController Instance { get; private set; }

        public event Action StateChanged;
        public event Action ItemsChanged;

        private readonly List<ItemInstance> chargingItems =
            new List<ItemInstance>(ChargingSlotCapacity);
        private readonly List<ItemInstance> upgradeItems =
            new List<ItemInstance>(UpgradeSlotCapacity);

        public IReadOnlyList<ItemInstance> ChargingItems => chargingItems;
        public IReadOnlyList<ItemInstance> UpgradeItems => upgradeItems;
        public bool IsUpgradeProcessing { get; private set; }
        public bool WantsToCharge
        {
            get
            {
                EnsureSlotCounts();
                for (int index = 0; index < chargingItems.Count; index++)
                {
                    ItemInstance item = chargingItems[index];
                    if (item?.ItemData != null && !item.IsFullyCharged)
                        return true;
                }

                return false;
            }
        }
        public bool IsCharging =>
            WantsToCharge && IsSystemEnabled && HasOperationalPower;
        public bool HasOperationalPower
        {
            get
            {
                EnergySystemController energy = EnergySystemController.Instance;
                if (energy == null)
                    return false;

                EnsureEnergyRegistration();
                return energy.IsConsumerPowered(ChargingConsumerId);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            EnsureSlotCounts();
            EnsureEnergyRegistration();
        }

        private void Start()
        {
            EnsureEnergyRegistration();
            RefreshPowerRequest();
        }

        private void Update()
        {
            AdvanceCharging(Time.deltaTime);
        }

        public ItemInstance GetChargingItem(int index)
        {
            EnsureSlotCounts();
            return IsValidIndex(chargingItems, index)
                ? chargingItems[index]
                : null;
        }

        public ItemInstance GetUpgradeItem(int index)
        {
            EnsureSlotCounts();
            return IsValidIndex(upgradeItems, index)
                ? upgradeItems[index]
                : null;
        }

        public bool LoadChargingItem(
            int destinationIndex,
            PlayerInventory inventory,
            InventorySlotGroup sourceGroup,
            int sourceIndex)
        {
            return MoveFromInventory(
                chargingItems,
                destinationIndex,
                inventory,
                sourceGroup,
                sourceIndex,
                candidate => candidate.IsChargeable);
        }

        public bool LoadUpgradeItem(
            int destinationIndex,
            PlayerInventory inventory,
            InventorySlotGroup sourceGroup,
            int sourceIndex)
        {
            return MoveFromInventory(
                upgradeItems,
                destinationIndex,
                inventory,
                sourceGroup,
                sourceIndex,
                candidate => IsValidUpgradeItem(destinationIndex, candidate));
        }

        public bool MoveChargingItemToInventory(
            int sourceIndex,
            PlayerInventory inventory,
            InventorySlotGroup destinationGroup,
            int destinationIndex)
        {
            return MoveToInventory(
                chargingItems,
                sourceIndex,
                inventory,
                destinationGroup,
                destinationIndex,
                candidate => candidate.IsChargeable);
        }

        public bool MoveUpgradeItemToInventory(
            int sourceIndex,
            PlayerInventory inventory,
            InventorySlotGroup destinationGroup,
            int destinationIndex)
        {
            if (IsUpgradeProcessing)
                return false;

            return MoveToInventory(
                upgradeItems,
                sourceIndex,
                inventory,
                destinationGroup,
                destinationIndex,
                candidate => IsValidUpgradeItem(sourceIndex, candidate));
        }

        public int RetrieveAllChargingItems(PlayerInventory inventory)
        {
            return RetrieveAll(chargingItems, inventory);
        }

        public int RetrieveAllUpgradeItems(PlayerInventory inventory)
        {
            return IsUpgradeProcessing
                ? 0
                : RetrieveAll(upgradeItems, inventory);
        }

        public void AdvanceCharging(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            EnsureSlotCounts();
            RefreshPowerRequest();
            if (!IsCharging)
                return;

            bool changed = false;
            for (int index = 0; index < chargingItems.Count; index++)
            {
                ItemInstance item = chargingItems[index];
                if (item?.ItemData == null || item.IsFullyCharged)
                    continue;

                float rate = item.ItemData.EnergyDefinition.RechargePerSecond;
                changed |= item.Recharge(rate * deltaTime) > 0f;
            }

            RefreshPowerRequest();
            if (changed)
                StateChanged?.Invoke();
        }

        public void RestoreItems(
            IReadOnlyList<ItemInstance> charging,
            IReadOnlyList<ItemInstance> upgrade)
        {
            RestoreGroup(
                chargingItems,
                charging,
                ChargingSlotCapacity,
                candidate => candidate.IsChargeable);
            RestoreGroup(
                upgradeItems,
                upgrade,
                UpgradeSlotCapacity,
                _ => true);
            IsUpgradeProcessing = false;
            RefreshPowerRequest();
            ItemsChanged?.Invoke();
        }

        private bool MoveFromInventory(
            List<ItemInstance> destination,
            int destinationIndex,
            PlayerInventory inventory,
            InventorySlotGroup sourceGroup,
            int sourceIndex,
            Func<ItemInstance, bool> accepts)
        {
            EnsureSlotCounts();
            if (!IsValidIndex(destination, destinationIndex) ||
                inventory == null)
            {
                return false;
            }

            ItemInstance incoming =
                inventory.GetItemInstance(sourceGroup, sourceIndex);
            if (incoming?.ItemData == null || !accepts(incoming))
                return false;

            ItemInstance replaced = destination[destinationIndex];
            if (replaced?.ItemData != null &&
                PlayerInventory.GetSlotGroup(replaced.ItemData.ItemType) !=
                sourceGroup)
            {
                return false;
            }

            if (!inventory.RemoveInstanceAt(
                    sourceGroup,
                    sourceIndex,
                    out incoming))
            {
                return false;
            }

            if (replaced != null &&
                !inventory.TrySetInstanceAt(
                    sourceGroup,
                    sourceIndex,
                    replaced))
            {
                inventory.TrySetInstanceAt(
                    sourceGroup,
                    sourceIndex,
                    incoming);
                return false;
            }

            destination[destinationIndex] = incoming;
            RefreshPowerRequest();
            ItemsChanged?.Invoke();
            return true;
        }

        private bool MoveToInventory(
            List<ItemInstance> source,
            int sourceIndex,
            PlayerInventory inventory,
            InventorySlotGroup destinationGroup,
            int destinationIndex,
            Func<ItemInstance, bool> acceptsReplacement)
        {
            EnsureSlotCounts();
            if (!IsValidIndex(source, sourceIndex) || inventory == null)
                return false;

            ItemInstance moving = source[sourceIndex];
            if (moving?.ItemData == null ||
                PlayerInventory.GetSlotGroup(moving.ItemData.ItemType) !=
                destinationGroup)
            {
                return false;
            }

            ItemInstance replaced =
                inventory.GetItemInstance(destinationGroup, destinationIndex);
            if (replaced?.ItemData != null && !acceptsReplacement(replaced))
                return false;

            if (!inventory.TryReplaceInstanceAt(
                    destinationGroup,
                    destinationIndex,
                    moving,
                    out replaced))
            {
                return false;
            }

            source[sourceIndex] = replaced;
            RefreshPowerRequest();
            ItemsChanged?.Invoke();
            return true;
        }

        private int RetrieveAll(
            List<ItemInstance> source,
            PlayerInventory inventory)
        {
            EnsureSlotCounts();
            if (inventory == null)
                return 0;

            int retrieved = 0;
            for (int index = 0; index < source.Count; index++)
            {
                ItemInstance item = source[index];
                if (item?.ItemData == null)
                    continue;

                if (!inventory.AddItem(item))
                    continue;

                source[index] = null;
                retrieved++;
            }

            if (retrieved > 0)
            {
                RefreshPowerRequest();
                ItemsChanged?.Invoke();
            }

            return retrieved;
        }

        private void RefreshPowerRequest()
        {
            EnsureEnergyRegistration();
            EnergySystemController.Instance?.SetConsumerActive(
                ChargingConsumerId,
                WantsToCharge && IsSystemEnabled);
        }

        private void EnsureEnergyRegistration()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return;

            energy.RegisterConsumer(
                ChargingConsumerId,
                energy.Config.ItemChargingConsumption,
                true);
        }

        private bool IsSystemEnabled =>
            StationSystemsController.Instance == null ||
            StationSystemsController.Instance.IsRequestedActive(
                StationSystemType.Laboratory);

        private static bool IsValidUpgradeItem(
            int slotIndex,
            ItemInstance item)
        {
            if (item?.ItemData == null)
                return false;

            return slotIndex switch
            {
                0 => item.ItemData.ItemType == ItemType.Equipment,
                1 => item.ItemData.ItemType == ItemType.Anomaly,
                _ => false
            };
        }

        private void EnsureSlotCounts()
        {
            EnsureCount(chargingItems, ChargingSlotCapacity);
            EnsureCount(upgradeItems, UpgradeSlotCapacity);
        }

        private static void EnsureCount(
            List<ItemInstance> items,
            int count)
        {
            while (items.Count < count)
                items.Add(null);
            if (items.Count > count)
                items.RemoveRange(count, items.Count - count);
        }

        private static bool IsValidIndex(
            List<ItemInstance> items,
            int index)
        {
            return index >= 0 && index < items.Count;
        }

        private static void RestoreGroup(
            List<ItemInstance> destination,
            IReadOnlyList<ItemInstance> source,
            int capacity,
            Func<ItemInstance, bool> accepts)
        {
            destination.Clear();
            for (int index = 0; index < capacity; index++)
            {
                ItemInstance item =
                    source != null && index < source.Count
                        ? source[index]
                        : null;
                destination.Add(
                    item?.ItemData != null && accepts(item)
                        ? item
                        : null);
            }
        }

        private void OnDestroy()
        {
            EnergySystemController.Instance?.SetConsumerActive(
                ChargingConsumerId,
                false);
            if (Instance == this)
                Instance = null;
        }
    }
}
