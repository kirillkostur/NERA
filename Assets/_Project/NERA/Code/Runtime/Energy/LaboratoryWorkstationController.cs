using System;
using System.Collections.Generic;
using NERA.Development;
using NERA.Inventory;
using NERA.Items;
using NERA.Research;
using NERA.Station;
using UnityEngine;

namespace NERA.Energy
{
    public sealed class LaboratoryWorkstationController : MonoBehaviour,
        IDeveloperProgressSkippable
    {
        private const string ChargingConsumerId = "laboratory_charging";
        private const string SynthesisConsumerId = "laboratory_synthesis";

        public const int ChargingSlotCapacity = 4;
        public const int UpgradeSlotCapacity = 2;

        public static LaboratoryWorkstationController Instance { get; private set; }

        public event Action StateChanged;
        public event Action ItemsChanged;

        private readonly List<ItemInstance> chargingItems =
            new List<ItemInstance>(ChargingSlotCapacity);
        private readonly List<ItemInstance> upgradeItems =
            new List<ItemInstance>(UpgradeSlotCapacity);
        private float synthesisElapsed;
        private float synthesisDuration;

        public IReadOnlyList<ItemInstance> ChargingItems => chargingItems;
        public IReadOnlyList<ItemInstance> UpgradeItems => upgradeItems;
        public bool IsUpgradeProcessing { get; private set; }
        public float SynthesisProgress => IsUpgradeProcessing
            ? Mathf.Clamp01(synthesisElapsed / Mathf.Max(0.1f, synthesisDuration))
            : 0f;
        public float CurrentSynthesisDuration => IsUpgradeProcessing
            ? synthesisDuration
            : 0f;
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
        private bool HasSynthesisOperationalPower
        {
            get
            {
                EnergySystemController energy = EnergySystemController.Instance;
                if (energy == null)
                    return false;

                EnsureEnergyRegistration();
                return energy.IsConsumerPowered(SynthesisConsumerId);
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
            AdvanceSynthesis(Time.deltaTime);
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
            if (IsUpgradeProcessing ||
                destinationIndex == 1 && IsScanSlotOccupied)
                return false;

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

        public bool CanSynthesize(out string reason)
        {
            EnsureSlotCounts();
            if (IsUpgradeProcessing)
            {
                reason = "Synthesis is already in progress.";
                return false;
            }

            if (IsScanSlotOccupied)
            {
                reason = "Remove the item from the scan slot before synthesis.";
                return false;
            }

            ItemInstance equipment = upgradeItems[0];
            ItemInstance anomaly = upgradeItems[1];
            if (equipment?.ItemData == null)
            {
                reason = "Place an equipment item in the left slot.";
                return false;
            }

            if (anomaly?.ItemData == null)
            {
                reason = "Place an IO anomaly in the right slot.";
                return false;
            }

            if (!IsValidUpgradeItem(0, equipment) ||
                !IsValidUpgradeItem(1, anomaly))
            {
                reason = "The selected items cannot be synthesized.";
                return false;
            }

            if (!anomaly.ItemData.AnomalyIntegrationDefinition.Supports(
                    equipment.ItemData))
            {
                reason = "This anomaly is incompatible with the equipment.";
                return false;
            }

            if (!equipment.IsChargeable ||
                !equipment.IsFullyCharged)
            {
                reason = "Fully charge the IO integration tool first.";
                return false;
            }

            if (equipment.HasAnomalyIntegration)
            {
                reason = "The IO integration tool is already loaded.";
                return false;
            }

            if (!anomaly.IsScanned)
            {
                reason = "Scan this anomaly sample before synthesis.";
                return false;
            }

            if (!IsSystemEnabled)
            {
                reason = "Laboratory is stopped.";
                return false;
            }

            EnsureEnergyRegistration();
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null ||
                !energy.CanPowerConsumer(SynthesisConsumerId))
            {
                reason = "Laboratory has no power.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TrySynthesize()
        {
            if (!CanSynthesize(out _))
                return false;

            ItemInstance equipment = upgradeItems[0];
            ItemInstance anomalyInstance = upgradeItems[1];
            ItemData anomaly = anomalyInstance.ItemData;
            synthesisElapsed = 0f;
            synthesisDuration =
                anomaly.AnomalyIntegrationDefinition.SynthesisDuration;
            IsUpgradeProcessing = true;
            RefreshPowerRequest();
            StateChanged?.Invoke();
            Debug.Log(
                $"Laboratory: started integrating '{anomaly.DisplayName}' " +
                $"into '{equipment.ItemData.DisplayName}'.",
                this);
            return true;
        }

        public void AdvanceSynthesis(float deltaTime)
        {
            if (!IsUpgradeProcessing || deltaTime <= 0f)
                return;

            RefreshPowerRequest();
            if (!HasSynthesisOperationalPower)
                return;

            synthesisElapsed = Mathf.Min(
                synthesisElapsed + deltaTime,
                synthesisDuration);
            StateChanged?.Invoke();
            if (synthesisElapsed + 0.0001f < synthesisDuration)
                return;

            CompleteSynthesis();
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

        public bool CompleteActiveProgressForDebug()
        {
            bool completed = false;
            if (IsUpgradeProcessing)
            {
                synthesisElapsed = synthesisDuration;
                StateChanged?.Invoke();
                CompleteSynthesis();
                completed = true;
            }

            EnsureSlotCounts();
            bool chargeChanged = false;
            foreach (ItemInstance item in chargingItems)
            {
                if (item?.ItemData == null || item.IsFullyCharged)
                    continue;

                item.SetCharge(item.MaxCharge);
                chargeChanged = true;
            }

            if (chargeChanged)
            {
                RefreshPowerRequest();
                StateChanged?.Invoke();
                completed = true;
            }

            return completed;
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
            synthesisElapsed = 0f;
            synthesisDuration = 0f;
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
            EnergySystemController.Instance?.SetConsumerActive(
                SynthesisConsumerId,
                IsUpgradeProcessing && IsSystemEnabled);
        }

        private void CompleteSynthesis()
        {
            ItemInstance equipment = upgradeItems[0];
            ItemInstance anomalyInstance = upgradeItems[1];
            ItemData anomaly = anomalyInstance?.ItemData;
            bool installed =
                equipment?.TryInstallAnomaly(anomalyInstance) == true;

            if (installed)
                upgradeItems[1] = null;

            IsUpgradeProcessing = false;
            synthesisElapsed = 0f;
            synthesisDuration = 0f;
            RefreshPowerRequest();
            StateChanged?.Invoke();
            if (!installed)
                return;

            ItemsChanged?.Invoke();
            Debug.Log(
                $"Laboratory: integrated '{anomaly.DisplayName}' into " +
                $"'{equipment.ItemData.DisplayName}'.",
                this);
        }

        private void EnsureEnergyRegistration()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return;

            float synthesisConsumption =
                StationSystemsConfig.GetEffectiveStat(
                    StationSystemType.Laboratory,
                    string.Empty,
                    StationObjectStat.IdleEnergyConsumption,
                    4f);
            energy.RegisterConsumer(
                ChargingConsumerId,
                energy.Config.ItemChargingConsumption,
                energy.Config.GetMinimumCharge01(
                    StationSystemType.Laboratory),
                StationSystemType.Laboratory);
            energy.RegisterConsumer(
                SynthesisConsumerId,
                synthesisConsumption,
                energy.Config.GetMinimumCharge01(
                    StationSystemType.Laboratory),
                StationSystemType.Laboratory);
        }

        private bool IsSystemEnabled =>
            StationSystemsController.Instance == null ||
            StationSystemsController.Instance.IsRequestedActive(
                StationSystemType.Laboratory);

        private static bool IsScanSlotOccupied =>
            ResearchController.Instance?.LoadedItemInstance?.ItemData != null;

        private static bool IsValidUpgradeItem(
            int slotIndex,
            ItemInstance item)
        {
            if (item?.ItemData == null)
                return false;

            return slotIndex switch
            {
                0 => item.ItemData.ItemType == ItemType.Equipment &&
                    item.ItemData.AcceptsAnomalyIntegration,
                1 => item.ItemData.ItemType == ItemType.Anomaly &&
                    item.ItemData.AnomalyIntegrationDefinition != null,
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
            EnergySystemController.Instance?.SetConsumerActive(
                SynthesisConsumerId,
                false);
            if (Instance == this)
                Instance = null;
        }
    }
}
