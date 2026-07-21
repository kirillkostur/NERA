using System;
using NERA.Inventory;
using NERA.Items;
using UnityEngine;

namespace NERA.Energy
{
    public sealed class ItemChargingController : MonoBehaviour
    {
        private const string ConsumerId = "item_charging_table";

        public static ItemChargingController Instance { get; private set; }

        public event Action StateChanged;
        public event Action LoadedItemChanged;

        public ItemInstance LoadedItem { get; private set; }
        public string StatusMessage { get; private set; } = "Charging table ready.";
        public bool WantsToCharge => LoadedItem != null && !LoadedItem.IsFullyCharged;
        public bool IsCharging => WantsToCharge && HasOperationalPower;
        public bool HasOperationalPower
        {
            get
            {
                EnergySystemController energy = EnergySystemController.Instance;
                if (energy == null)
                    return false;

                EnsureEnergyRegistration();
                return energy.IsConsumerPowered(ConsumerId);
            }
        }
        public float Progress => LoadedItem?.Charge01 ?? 0f;

        private PlayerInventory sourceInventory;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            EnsureEnergyRegistration();
        }

        private void Start()
        {
            EnsureEnergyRegistration();
            RefreshState();
        }

        private void Update()
        {
            AdvanceCharging(Time.deltaTime);
        }

        public bool LoadItem(
            PlayerInventory inventory,
            InventorySlotGroup sourceGroup,
            int sourceIndex
        )
        {
            if (inventory == null)
                return false;

            ItemInstance source = inventory.GetItemInstance(sourceGroup, sourceIndex);
            if (source == null || !source.IsChargeable)
                return false;

            ItemInstance previous = LoadedItem;
            if (previous?.ItemData != null &&
                PlayerInventory.GetSlotGroup(previous.ItemData.ItemType) != sourceGroup)
            {
                return false;
            }

            if (!inventory.RemoveInstanceAt(sourceGroup, sourceIndex, out source))
                return false;

            if (previous != null &&
                !inventory.TrySetInstanceAt(sourceGroup, sourceIndex, previous))
            {
                inventory.TrySetInstanceAt(sourceGroup, sourceIndex, source);
                return false;
            }

            LoadedItem = source;
            sourceInventory = inventory;
            RefreshState();
            LoadedItemChanged?.Invoke();
            return true;
        }

        public bool MoveLoadedItemToInventory(
            PlayerInventory inventory,
            InventorySlotGroup destinationGroup,
            int destinationIndex
        )
        {
            if (LoadedItem?.ItemData == null || inventory == null ||
                PlayerInventory.GetSlotGroup(LoadedItem.ItemData.ItemType) != destinationGroup)
            {
                return false;
            }

            ItemInstance moving = LoadedItem;
            if (!inventory.TryReplaceInstanceAt(
                    destinationGroup,
                    destinationIndex,
                    moving,
                    out ItemInstance replaced))
            {
                return false;
            }

            LoadedItem = replaced;
            sourceInventory = replaced != null ? inventory : null;
            RefreshState();
            LoadedItemChanged?.Invoke();
            return true;
        }

        public bool RetrieveLoadedItem()
        {
            if (LoadedItem == null || sourceInventory == null || !sourceInventory.AddItem(LoadedItem))
                return false;

            LoadedItem = null;
            sourceInventory = null;
            RefreshState();
            LoadedItemChanged?.Invoke();
            return true;
        }

        public void AdvanceCharging(float deltaTime)
        {
            if (LoadedItem == null || deltaTime <= 0f)
                return;

            if (LoadedItem.IsFullyCharged)
            {
                RefreshState();
                return;
            }

            EnergySystemController energy = EnergySystemController.Instance;
            EnsureEnergyRegistration();
            if (energy == null)
            {
                StatusMessage = "Charging paused - station energy system unavailable.";
                StateChanged?.Invoke();
                return;
            }

            energy.SetConsumerActive(ConsumerId, true);

            if (!energy.IsConsumerPowered(ConsumerId))
            {
                StatusMessage = "Charging paused - insufficient station energy.";
                StateChanged?.Invoke();
                return;
            }

            float rate = LoadedItem.ItemData.EnergyDefinition.RechargePerSecond;
            if (LoadedItem.Recharge(rate * deltaTime) <= 0f)
                return;

            StatusMessage = LoadedItem.IsFullyCharged
                ? $"{LoadedItem.ItemData.DisplayName} fully charged."
                : $"Charging {LoadedItem.ItemData.DisplayName}...";

            if (LoadedItem.IsFullyCharged)
                energy?.SetConsumerActive(ConsumerId, false);

            StateChanged?.Invoke();
        }

        public void RestoreLoadedItem(ItemInstance instance, PlayerInventory inventory)
        {
            LoadedItem = instance;
            sourceInventory = instance != null ? inventory : null;
            RefreshState();
            LoadedItemChanged?.Invoke();
        }

        private void RefreshState()
        {
            EnsureEnergyRegistration();
            EnergySystemController energy = EnergySystemController.Instance;
            energy?.SetConsumerActive(ConsumerId, WantsToCharge);
            StatusMessage = LoadedItem == null
                ? "Charging table ready."
                : LoadedItem.IsFullyCharged
                    ? $"{LoadedItem.ItemData.DisplayName} fully charged."
                    : energy == null || !energy.IsConsumerPowered(ConsumerId)
                        ? "Charging paused - restore station power."
                        : $"Charging {LoadedItem.ItemData.DisplayName}...";
            StateChanged?.Invoke();
        }

        private void EnsureEnergyRegistration()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return;

            energy.RegisterConsumer(ConsumerId, energy.Config.ItemChargingConsumption, true);
            energy.SetConsumerActive(ConsumerId, WantsToCharge);
        }

        private void OnDestroy()
        {
            EnergySystemController.Instance?.SetConsumerActive(ConsumerId, false);
            if (Instance == this)
                Instance = null;
        }
    }
}
