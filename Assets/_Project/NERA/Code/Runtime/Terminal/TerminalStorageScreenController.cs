using System;
using System.Collections.Generic;
using NERA.Inventory;
using NERA.Items;
using NERA.Research;
using NERA.Station;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Terminal
{
    public sealed class TerminalStorageScreenController : MonoBehaviour
    {
        private sealed class SlotView
        {
            public InventorySlotGroup Group;
            public int Index;
            public bool IsStorage;
            public Button Button;
            public Image Icon;
            public LaboratoryInventoryItemDrag Drag;
            public LaboratoryItemDropSlot Drop;
            public ItemData Item;
        }

        private readonly List<SlotView> storageSlots = new List<SlotView>();
        private readonly List<SlotView> inventorySlots = new List<SlotView>();
        private readonly Dictionary<InventorySlotGroup, GameObject> storageRoots =
            new Dictionary<InventorySlotGroup, GameObject>();

        private PlayerInventory inventory;
        private Canvas rootCanvas;
        private TMP_Text nameText;
        private TMP_Text descriptionText;
        private Image infoImage;
        private SlotView selectedSlot;
        private InventorySlotGroup activeGroup = InventorySlotGroup.Backpack;
        private bool initialized;
        private PlayerInventory subscribedInventory;
        private StationStorageController subscribedStorage;

        public void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            rootCanvas = GetComponentInParent<Canvas>();
            inventory = InventoryLabHUDController.Instance?.BoundInventory ??
                FindFirstObjectByType<PlayerInventory>();
            CacheHierarchy();
            BindTabs();
            StationStorageController.Instance?.ConfigureCapacities(16, 16, 16);
            BuildAllSlots();
            ShowStorageGroup(InventorySlotGroup.Backpack);
            RefreshAll();
        }

        public void SetScreenActive(bool active)
        {
            if (!active)
            {
                UnbindSources();
                return;
            }

            inventory = InventoryLabHUDController.Instance?.BoundInventory ??
                inventory ??
                FindFirstObjectByType<PlayerInventory>();
            BindSources();
            RefreshAll();
        }

        public void HandleTerminalClosed()
        {
            UnbindSources();
            selectedSlot = null;
            ClearInfo();
        }

        private void CacheHierarchy()
        {
            Transform infoRoot = TerminalUIUtility.Find(
                transform, "background_Screen_Storage_Info");
            nameText = TerminalUIUtility.FindComponent<TMP_Text>(
                infoRoot, "Text_Name");
            descriptionText = TerminalUIUtility.FindComponent<TMP_Text>(
                infoRoot, "Text_Description");
            infoImage = TerminalUIUtility.FindComponent<Image>(
                infoRoot, "Image_info");

            storageRoots[InventorySlotGroup.Backpack] =
                TerminalUIUtility.Find(
                    transform,
                    "background_Screen_Storage_Slot")?.gameObject;
            storageRoots[InventorySlotGroup.Anomaly] =
                TerminalUIUtility.Find(
                    transform,
                    "background_Screen_Storage_Slot_Anomaly")?.gameObject;
            storageRoots[InventorySlotGroup.QuickAccess] =
                TerminalUIUtility.Find(
                    transform,
                    "background_Screen_Storage_Slot_Equipment")?.gameObject;
        }

        private void BindTabs()
        {
            TerminalUIUtility.FindComponent<Button>(
                transform, "StoragMapButton")?.onClick.AddListener(
                () => ShowStorageGroup(InventorySlotGroup.Backpack));
            TerminalUIUtility.FindComponent<Button>(
                transform, "AnomalyMapButton")?.onClick.AddListener(
                () => ShowStorageGroup(InventorySlotGroup.Anomaly));
            TerminalUIUtility.FindComponent<Button>(
                transform, "EquipmentMapButton")?.onClick.AddListener(
                () => ShowStorageGroup(InventorySlotGroup.QuickAccess));
        }

        private void BuildAllSlots()
        {
            BuildGroup(
                storageRoots[InventorySlotGroup.Backpack]?.transform,
                InventorySlotGroup.Backpack,
                true,
                storageSlots);
            BuildGroup(
                storageRoots[InventorySlotGroup.Anomaly]?.transform,
                InventorySlotGroup.Anomaly,
                true,
                storageSlots);
            BuildGroup(
                storageRoots[InventorySlotGroup.QuickAccess]?.transform,
                InventorySlotGroup.QuickAccess,
                true,
                storageSlots);

            BuildGroup(
                TerminalUIUtility.Find(
                    transform,
                    "background_Screen_Storage_Slot_Invent"),
                InventorySlotGroup.Backpack,
                false,
                inventorySlots);
            BuildGroup(
                TerminalUIUtility.Find(
                    transform,
                    "background_Screen_Storage_Slot_Invent_Anomaly"),
                InventorySlotGroup.Anomaly,
                false,
                inventorySlots);
            BuildGroup(
                TerminalUIUtility.Find(
                    transform,
                    "background_Screen_Storage_Slot_Invent_Equipment"),
                InventorySlotGroup.QuickAccess,
                false,
                inventorySlots);
        }

        private void BuildGroup(
            Transform root,
            InventorySlotGroup group,
            bool isStorage,
            List<SlotView> destination)
        {
            if (root == null)
                return;

            InventoryConfig inventoryConfig =
                InventoryConfig.Resolve(inventory?.Config);
            GameObject slotPrefab = inventoryConfig?.SlotPrefab;
            if (slotPrefab == null)
            {
                Debug.LogError(
                    "TerminalStorageScreenController: P_InventorySlot is missing in InventoryConfig.",
                    this);
                return;
            }

            List<Transform> authored = new List<Transform>();
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name.StartsWith("Slot_", StringComparison.Ordinal))
                    authored.Add(child);
            }
            authored.Sort((left, right) =>
                GetSlotNumber(left.name).CompareTo(GetSlotNumber(right.name)));

            for (int index = 0; index < authored.Count; index++)
            {
                InventorySlotView inventorySlot =
                    InventorySlotSpawnUtility.GetOrCreate(
                        authored[index],
                        slotPrefab);
                if (inventorySlot == null)
                    continue;

                inventorySlot.Initialize(index, false, rootCanvas);
                Transform slotRoot = inventorySlot.transform;
                SlotView view = new SlotView
                {
                    Group = group,
                    Index = index,
                    IsStorage = isStorage,
                    Button = inventorySlot.Button ??
                        TerminalUIUtility.EnsureButton(slotRoot),
                    Icon = inventorySlot.Icon ??
                        TerminalUIUtility.EnsureSlotIcon(slotRoot),
                    Drag = inventorySlot.LaboratoryDrag ??
                        slotRoot.gameObject.AddComponent<LaboratoryInventoryItemDrag>(),
                    Drop = slotRoot.GetComponent<LaboratoryItemDropSlot>() ??
                        slotRoot.gameObject.AddComponent<LaboratoryItemDropSlot>()
                };
                view.Button?.onClick.AddListener(() => SelectSlot(view));
                view.Drag.InteractionStarted += _ => SelectSlot(view);
                view.Drop.ItemDropped += drag => HandleDrop(view, drag);
                destination.Add(view);
            }
        }

        private void ShowStorageGroup(InventorySlotGroup group)
        {
            activeGroup = group;
            foreach (KeyValuePair<InventorySlotGroup, GameObject> pair in
                     storageRoots)
            {
                if (pair.Value != null)
                    pair.Value.SetActive(pair.Key == group);
            }
            RefreshAll();
        }

        private void HandleDrop(
            SlotView destination,
            LaboratoryInventoryItemDrag drag)
        {
            if (destination == null || drag?.Item == null)
                return;

            StationStorageController storage = StationStorageController.Instance;
            inventory = InventoryLabHUDController.Instance?.BoundInventory ??
                inventory;
            if (storage == null || inventory == null)
                return;

            if (destination.IsStorage)
            {
                if (drag.IsStationStorageSource)
                {
                    storage.MoveWithinStorage(
                        drag.SourceGroup,
                        drag.SourceIndex,
                        destination.Group,
                        destination.Index);
                }
                else if (!drag.IsLaboratorySource &&
                         !drag.IsChargingSource &&
                         !drag.IsUpgradeSource)
                {
                    storage.MoveFromInventory(
                        inventory,
                        drag.SourceGroup,
                        drag.SourceIndex,
                        destination.Group,
                        destination.Index);
                }
            }
            else if (drag.IsStationStorageSource)
            {
                storage.MoveToInventory(
                    drag.SourceGroup,
                    drag.SourceIndex,
                    inventory,
                    destination.Group,
                    destination.Index);
            }
            else if (!drag.IsLaboratorySource &&
                     !drag.IsChargingSource &&
                     !drag.IsUpgradeSource)
            {
                inventory.TryMoveItem(
                    drag.SourceGroup,
                    drag.SourceIndex,
                    destination.Group,
                    destination.Index);
            }

            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshViews(storageSlots);
            RefreshViews(inventorySlots);
            if (selectedSlot != null)
                ShowInfo(selectedSlot.Item);
        }

        private void RefreshViews(List<SlotView> views)
        {
            StationStorageController storage = StationStorageController.Instance;
            foreach (SlotView view in views)
            {
                ItemInstance instance;
                if (view.IsStorage)
                {
                    IReadOnlyList<ItemInstance> slots =
                        storage?.GetSlots(view.Group);
                    instance = slots != null && view.Index < slots.Count
                        ? slots[view.Index]
                        : null;
                }
                else
                {
                    instance = inventory?.GetItemInstance(
                        view.Group,
                        view.Index);
                }

                view.Item = instance?.ItemData;
                TerminalUIUtility.SetItemIcon(view.Icon, view.Item);
                view.Drag?.Initialize(
                    view.Item,
                    rootCanvas,
                    view.Group,
                    view.Index,
                    false,
                    false,
                    view.IsStorage);
                if (view.Button != null)
                    view.Button.interactable = true;
            }
        }

        private void SelectSlot(SlotView view)
        {
            selectedSlot = view;
            ShowInfo(view?.Item);
        }

        private void ShowInfo(ItemData item)
        {
            TerminalUIUtility.SetText(nameText, item?.DisplayName);
            TerminalUIUtility.SetText(descriptionText, item?.Description);
            if (infoImage != null)
            {
                infoImage.sprite = item?.Icon;
                infoImage.enabled = item?.Icon != null;
            }
        }

        private void ClearInfo()
        {
            ShowInfo(null);
        }

        private void BindSources()
        {
            PlayerInventory resolvedInventory =
                InventoryLabHUDController.Instance?.BoundInventory ??
                inventory ??
                FindFirstObjectByType<PlayerInventory>();
            if (subscribedInventory != resolvedInventory)
            {
                if (subscribedInventory != null)
                    subscribedInventory.InventoryChanged -= RefreshAll;
                subscribedInventory = resolvedInventory;
                inventory = resolvedInventory;
                if (subscribedInventory != null)
                    subscribedInventory.InventoryChanged += RefreshAll;
            }

            StationStorageController resolvedStorage =
                StationStorageController.Instance;
            if (subscribedStorage != resolvedStorage)
            {
                if (subscribedStorage != null)
                    subscribedStorage.StorageChanged -= RefreshAll;
                subscribedStorage = resolvedStorage;
                if (subscribedStorage != null)
                    subscribedStorage.StorageChanged += RefreshAll;
            }
        }

        private void UnbindSources()
        {
            if (subscribedInventory != null)
                subscribedInventory.InventoryChanged -= RefreshAll;
            if (subscribedStorage != null)
                subscribedStorage.StorageChanged -= RefreshAll;
            subscribedInventory = null;
            subscribedStorage = null;
        }

        private void OnDestroy()
        {
            UnbindSources();
        }

        private static int GetSlotNumber(string name)
        {
            return name.StartsWith("Slot_", StringComparison.Ordinal) &&
                int.TryParse(name.Substring(5), out int number)
                ? number
                : int.MaxValue;
        }
    }
}
