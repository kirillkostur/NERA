using System;
using System.Collections.Generic;
using NERA.Items;
using UnityEngine;

namespace NERA.Station
{
    /// <summary>
    /// Shared visual presenter for both world objects and StationUIPreview.
    /// The same saved slot state therefore produces the same attached parts.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StationObjectIdentity))]
    public sealed class StationObjectVisual : MonoBehaviour
    {
        [SerializeField] private StationObjectIdentity identity;
        [SerializeField] private StationUpgradeSlot[] slots =
            Array.Empty<StationUpgradeSlot>();
        [Tooltip("Allows empty slot silhouettes while this object is being upgraded.")]
        [SerializeField] private bool showFakeSlotsWhenEmpty = true;
        [Tooltip("Removes colliders brought in by installed-part prefabs. " +
                 "Use for passive world decoration only.")]
        [SerializeField] private bool removeRuntimePartColliders;

        private StationSystemsController subscribedSystems;
        private ItemCatalogData catalog;
        private readonly HashSet<string> availableEmptySlots =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool upgradeModeActive;
        private bool filterEmptySlots;

        public IReadOnlyList<StationUpgradeSlot> Slots => slots;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            StationSystemsController.InstanceChanged += HandleSystemsChanged;
            BindSystems(StationSystemsController.Instance);
        }

        public StationUpgradeSlot FindSlot(string slotId)
        {
            foreach (StationUpgradeSlot slot in slots)
            {
                if (slot != null && string.Equals(
                        slot.SlotId,
                        slotId?.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return slot;
                }
            }
            return null;
        }

        public void Refresh()
        {
            ResolveReferences();
            catalog ??= Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            StationSystemsController systems = StationSystemsController.Instance;
            foreach (StationUpgradeSlot slot in slots)
                RestoreSlot(slot, systems);
        }

        public void ShowPart(StationUpgradeSlot slot, ItemData item)
        {
            if (slot != null)
                slot.ShowPart(item);
        }

        public void RestoreSlot(StationUpgradeSlot slot)
        {
            RestoreSlot(slot, StationSystemsController.Instance);
        }

        public void SetUpgradeModeActive(bool active)
        {
            upgradeModeActive = active;
            if (!active)
            {
                filterEmptySlots = false;
                availableEmptySlots.Clear();
            }
            foreach (StationUpgradeSlot slot in slots)
                slot?.SetUpgradeModeActive(active);
            Refresh();
        }

        public void SetAvailableEmptySlots(IEnumerable<string> slotIds)
        {
            ResolveReferences();
            filterEmptySlots = true;
            availableEmptySlots.Clear();
            if (slotIds != null)
            {
                foreach (string slotId in slotIds)
                {
                    if (!string.IsNullOrWhiteSpace(slotId))
                        availableEmptySlots.Add(slotId.Trim());
                }
            }

            // Availability changes while a part is staged. Updating the
            // silhouettes must not restore committed slot data, otherwise
            // the staged preview is destroyed before Apply is pressed.
            foreach (StationUpgradeSlot slot in slots)
            {
                if (slot == null)
                    continue;

                slot.SetEmptyFakeVisible(
                    showFakeSlotsWhenEmpty &&
                    upgradeModeActive &&
                    availableEmptySlots.Contains(slot.SlotId));
            }
        }

        public void Configure(bool showEmptyFakes)
        {
            showFakeSlotsWhenEmpty = showEmptyFakes;
            identity = GetComponent<StationObjectIdentity>();
            slots = GetComponentsInChildren<StationUpgradeSlot>(true);
        }

        private void RestoreSlot(
            StationUpgradeSlot slot,
            StationSystemsController systems)
        {
            if (slot == null || identity == null)
                return;
            catalog ??= Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            string itemId = systems?.GetInstalledPartItemId(
                identity.SystemType,
                identity.ObjectId,
                slot.SlotId);
            ItemData item = catalog?.Find(itemId);
            if (item != null)
                slot.ShowPart(item);
            else
                slot.ShowEmpty(
                    showFakeSlotsWhenEmpty &&
                    upgradeModeActive &&
                    (!filterEmptySlots ||
                     availableEmptySlots.Contains(slot.SlotId)));
        }

        private void HandleSystemsChanged(StationSystemsController systems)
        {
            BindSystems(systems);
        }

        private void BindSystems(StationSystemsController systems)
        {
            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged -= Refresh;
            subscribedSystems = systems;
            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged += Refresh;
            Refresh();
        }

        private void ResolveReferences()
        {
            identity ??= GetComponent<StationObjectIdentity>();
            if (slots == null || slots.Length == 0)
                slots = GetComponentsInChildren<StationUpgradeSlot>(true);

            foreach (StationUpgradeSlot slot in slots)
            {
                slot?.SetRemoveRuntimeColliders(
                    removeRuntimePartColliders);
            }
        }

        private void OnDisable()
        {
            StationSystemsController.InstanceChanged -= HandleSystemsChanged;
            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged -= Refresh;
            subscribedSystems = null;
        }

        private void OnValidate()
        {
            ResolveReferences();
        }
    }
}
