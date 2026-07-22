using System;
using System.Collections.Generic;
using NERA.Antenna;
using NERA.Drone;
using NERA.Energy;
using NERA.Inventory;
using NERA.Items;
using NERA.Research;
using NERA.Station;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Terminal
{
    public sealed class StationPanelController : MonoBehaviour
    {
        private static readonly Color MissingIconColor =
            new Color(0.18f, 0.28f, 0.31f, 1f);

        private sealed class SystemView
        {
            public StationSystemType Type;
            public GameObject Root;
            public Button Button;
            public TMP_Text Description;
        }

        private sealed class StorageView
        {
            public InventorySlotGroup Group;
            public int Index;
            public Button Button;
            public Image Icon;
            public LaboratoryInventoryItemDrag Drag;
            public LaboratoryItemDropSlot DropTarget;
        }

        private readonly Dictionary<StationSystemType, SystemView> systemViews =
            new Dictionary<StationSystemType, SystemView>();
        private readonly List<StorageView> storageViews = new List<StorageView>();

        private GameObject statusPanel;
        private GameObject systemsPanel;
        private GameObject devicesPanel;
        private Button statusTabButton;
        private Button systemsTabButton;
        private Button devicesTabButton;
        private Button upgradeButton;
        private Button powerButton;
        private TMP_Text statusText;
        private Canvas rootCanvas;
        private StationSystemType selectedSystem = StationSystemType.Drone;
        private float nextRefreshAt;

        private void Awake()
        {
            CacheReferences();
            BindButtons();
            ShowStatus();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshAt)
                return;

            nextRefreshAt = Time.unscaledTime + 0.2f;
            RefreshAll();
        }

        public void RefreshAll()
        {
            RefreshStatus();
            RefreshSystems();
            RefreshActions();
            RefreshStorage();
        }

        public void ShowDefaultSection()
        {
            ShowStatus();
        }

        private void CacheReferences()
        {
            rootCanvas = GetComponentInParent<Canvas>();
            statusPanel = FindChild("StatusPanel")?.gameObject;
            systemsPanel = FindChild("SystemsPanel")?.gameObject;
            devicesPanel = FindChild("DevicesPanel")?.gameObject;
            statusTabButton = FindButton("StatusTabButton");
            systemsTabButton = FindButton("SystemsTabButton");
            devicesTabButton = FindButton("DevicesTabButton");
            upgradeButton = FindButton("UpgradeTabButton");
            powerButton = FindButton("Stop\\StartTabButton");
            statusText = FindText(statusPanel != null ? statusPanel.transform : transform, "StatusText");

            RegisterSystemView(StationSystemType.SolarPanel, "Slot_SolarPanel");
            RegisterSystemView(StationSystemType.Battery, "Slot_Battery");
            RegisterSystemView(StationSystemType.Computer, "Slot_Сomputer");
            RegisterSystemView(StationSystemType.Drone, "Slot_Dron");
            RegisterSystemView(StationSystemType.Laboratory, "Slot_Laboratory");
            RegisterSystemView(StationSystemType.Charger, "Slot_Charger");
            RegisterSystemView(StationSystemType.Antenna, "Slot_Antenna");
            RegisterSystemView(StationSystemType.Turret, "Slot_Turret");

            BindStorageGroup("Background_Backpack", InventorySlotGroup.Backpack);
            BindStorageGroup("Background_QuickAccess", InventorySlotGroup.QuickAccess);
            BindStorageGroup("Background_Anomaly", InventorySlotGroup.Anomaly);
        }

        private void BindButtons()
        {
            statusTabButton?.onClick.AddListener(ShowStatus);
            systemsTabButton?.onClick.AddListener(ShowSystems);
            devicesTabButton?.onClick.AddListener(ShowDevices);
            upgradeButton?.onClick.AddListener(HandleUpgrade);
            powerButton?.onClick.AddListener(HandlePowerToggle);
        }

        private void RegisterSystemView(StationSystemType type, string objectName)
        {
            Transform root = FindChild(objectName, systemsPanel?.transform);
            if (root == null)
                return;

            Button button = root.GetComponent<Button>() ?? root.gameObject.AddComponent<Button>();
            TMP_Text description = FindText(root, "Description");
            SystemView view = new SystemView
            {
                Type = type,
                Root = root.gameObject,
                Button = button,
                Description = description
            };
            button.onClick.AddListener(() => SelectSystem(type));
            systemViews[type] = view;
        }

        private void ShowStatus()
        {
            InventoryLabHUDController.Instance?.CloseStationStorage();
            SetSection(statusPanel);
            RefreshAll();
        }

        private void ShowSystems()
        {
            InventoryLabHUDController.Instance?.CloseStationStorage();
            SetSection(systemsPanel);
            RefreshAll();
        }

        private void ShowDevices()
        {
            SetSection(devicesPanel);
            InventoryLabHUDController.Instance?.OpenStationStorage();
            RefreshAll();
        }

        private void SetSection(GameObject section)
        {
            if (statusPanel != null)
                statusPanel.SetActive(section == statusPanel);
            if (systemsPanel != null)
                systemsPanel.SetActive(section == systemsPanel);
            if (devicesPanel != null)
                devicesPanel.SetActive(section == devicesPanel);
        }

        private void SelectSystem(StationSystemType type)
        {
            selectedSystem = type;
            RefreshActions();
        }

        private void HandleUpgrade()
        {
            StationSystemsController systems = StationSystemsController.Instance;
            PlayerInventory inventory =
                InventoryLabHUDController.Instance?.BoundInventory ??
                FindFirstObjectByType<PlayerInventory>();
            if (systems != null && systems.TryUpgrade(
                    selectedSystem,
                    inventory,
                    StationStorageController.Instance))
            {
                AntennaController.Instance?.RefreshAvailability();
                DroneScanController.Instance?.RefreshAvailability();
                RefreshAll();
            }
        }

        private void HandlePowerToggle()
        {
            StationSystemsController systems = StationSystemsController.Instance;
            if (systems == null)
                return;

            bool active = systems.IsRequestedActive(selectedSystem);
            if (systems.SetRequestedActive(selectedSystem, !active))
            {
                AntennaController.Instance?.RefreshAvailability();
                DroneScanController.Instance?.RefreshAvailability();
                RefreshAll();
            }
        }

        private void BindStorageGroup(string backgroundName, InventorySlotGroup group)
        {
            Transform background = FindChild(backgroundName, devicesPanel?.transform);
            if (background == null)
                return;

            List<Button> buttons = new List<Button>();
            foreach (Button button in background.GetComponentsInChildren<Button>(true))
            {
                if (button.transform.parent == background &&
                    button.name.StartsWith("Slot_", StringComparison.Ordinal))
                {
                    buttons.Add(button);
                }
            }

            buttons.Sort((left, right) =>
                GetAuthoredSlotNumber(left.name).CompareTo(
                    GetAuthoredSlotNumber(right.name)));

            for (int index = 0; index < buttons.Count; index++)
            {
                Button button = buttons[index];
                StorageView view = new StorageView
                {
                    Group = group,
                    Index = index,
                    Button = button,
                    Icon = FindImage(button.transform, "Icon"),
                    Drag = button.GetComponent<LaboratoryInventoryItemDrag>() ??
                        button.gameObject.AddComponent<LaboratoryInventoryItemDrag>(),
                    DropTarget = button.GetComponent<LaboratoryItemDropSlot>() ??
                        button.gameObject.AddComponent<LaboratoryItemDropSlot>()
                };
                int slotIndex = index;
                view.DropTarget.ItemDropped += drag =>
                    HandleStorageDrop(group, slotIndex, drag);
                storageViews.Add(view);
            }
        }

        private void HandleStorageDrop(
            InventorySlotGroup destinationGroup,
            int destinationIndex,
            LaboratoryInventoryItemDrag drag)
        {
            if (drag == null || drag.Item == null)
                return;

            PlayerInventory inventory =
                InventoryLabHUDController.Instance?.BoundInventory ??
                FindFirstObjectByType<PlayerInventory>();
            StationStorageController storage = StationStorageController.Instance;
            if (storage == null)
                return;

            if (drag.IsStationStorageSource)
            {
                storage.MoveWithinStorage(
                    drag.SourceGroup,
                    drag.SourceIndex,
                    destinationGroup,
                    destinationIndex);
            }
            else if (!drag.IsLaboratorySource && !drag.IsChargingSource)
            {
                storage.MoveFromInventory(
                    inventory,
                    drag.SourceGroup,
                    drag.SourceIndex,
                    destinationGroup,
                    destinationIndex);
            }

            RefreshAll();
        }

        private void RefreshStatus()
        {
            if (statusText == null)
                return;

            EnergySystemController energy = EnergySystemController.Instance;
            StationStorageController storage = StationStorageController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            string power = energy != null && energy.GridEnabled ? "ONLINE" : "OFFLINE";
            string energyLine = energy != null
                ? $"{energy.CurrentEnergy:0} / {energy.TotalCapacity:0}  ({energy.Charge01 * 100f:0}%)"
                : "UNAVAILABLE";
            string flow = energy != null
                ? $"+{energy.CurrentGeneration:0.0}/s  -{energy.CurrentConsumption:0.0}/s"
                : "--";
            string storageLine = storage != null
                ? $"{storage.Count} / {storage.Capacity}"
                : "UNAVAILABLE";
            string defense = systems != null && systems.IsUnlocked(StationSystemType.Turret)
                ? systems.IsRequestedActive(StationSystemType.Turret)
                    ? "ARMED"
                    : "STOPPED"
                : "NOT INSTALLED";

            statusText.text =
                $"STATION STATUS\n\nPOWER GRID        {power}\n" +
                $"BATTERY          {energyLine}\n" +
                $"ENERGY FLOW       {flow}\n" +
                $"ACTIVE DEVICES    {(energy != null ? energy.ActiveConsumerCount : 0)}\n" +
                $"STORAGE           {storageLine}\n" +
                $"DEFENSE           {defense}";
        }

        private void RefreshSystems()
        {
            foreach (KeyValuePair<StationSystemType, SystemView> pair in systemViews)
            {
                if (pair.Value.Description != null)
                    pair.Value.Description.text = BuildSystemDescription(pair.Key);
            }
        }

        private string BuildSystemDescription(StationSystemType type)
        {
            StationSystemsController systems = StationSystemsController.Instance;
            StationSystemDefinition definition = systems?.GetDefinition(type);
            EnergySystemController energy = EnergySystemController.Instance;
            string title = definition?.DisplayName ?? type.ToString().ToUpperInvariant();
            string details = definition?.Description ?? string.Empty;
            string status;
            string metrics;

            switch (type)
            {
                case StationSystemType.SolarPanel:
                    float condition = systems?.GetCondition(type) ?? 1f;
                    status = condition > 0.01f ? "ACTIVE" : "CLEANING REQUIRED";
                    metrics = $"Efficiency {condition * 100f:0}% | Generation +{energy?.CurrentGeneration ?? 0f:0.0}/s";
                    break;
                case StationSystemType.Battery:
                    status = energy != null ? energy.State.ToString().ToUpperInvariant() : "OFFLINE";
                    metrics = energy != null
                        ? $"Charge {energy.CurrentEnergy:0}/{energy.TotalCapacity:0} | Consumers {energy.ActiveConsumerCount}"
                        : "Energy controller unavailable";
                    break;
                case StationSystemType.Computer:
                    status = "ONLINE";
                    metrics = $"Consumption {energy?.Config.TerminalConsumption ?? 0f:0.0}/s | Fixed system";
                    break;
                case StationSystemType.Drone:
                    DroneScanController drone = DroneScanController.Instance;
                    status = GetManagedStatus(type, systems);
                    metrics = $"{drone?.State.ToString().ToUpperInvariant() ?? "UNAVAILABLE"} | Charge load {energy?.Config.DroneChargingConsumption ?? 0f:0.0}/s | Range Lv.{systems?.GetUpgradeLevel(type) ?? 0}";
                    break;
                case StationSystemType.Laboratory:
                    status = GetManagedStatus(type, systems);
                    metrics = $"{ResearchController.Instance?.State.ToString().ToUpperInvariant() ?? "IDLE"} | Consumption {energy?.Config.LaboratoryConsumption ?? 0f:0.0}/s";
                    break;
                case StationSystemType.Charger:
                    status = GetManagedStatus(type, systems);
                    metrics = $"{ItemChargingController.Instance?.StatusMessage ?? "READY"} | Consumption {energy?.Config.ItemChargingConsumption ?? 0f:0.0}/s";
                    break;
                case StationSystemType.Antenna:
                    status = GetManagedStatus(type, systems);
                    metrics = $"{AntennaController.Instance?.State.ToString().ToUpperInvariant() ?? "LOCKED"} | Condition {(systems?.GetCondition(type) ?? 1f) * 100f:0}% | Consumption {energy?.Config.AntennaCalibrationConsumption ?? 0f:0.0}/s";
                    break;
                default:
                    StationTurretController turret = StationTurretController.Instance;
                    status = GetManagedStatus(type, systems);
                    metrics = $"Condition {(systems?.GetCondition(type) ?? 1f) * 100f:0}% | Idle {energy?.Config.TurretIdleConsumption ?? 0f:0.0}/s | Target {(turret != null && turret.HasTarget ? "LOCKED" : "NONE")}";
                    break;
            }

            string upgrade = BuildUpgradeLine(type, systems);
            return $"{title}\n{status} — {metrics}\n{details}{upgrade}";
        }

        private static string GetManagedStatus(
            StationSystemType type,
            StationSystemsController systems)
        {
            if (systems == null)
                return "UNAVAILABLE";
            if (!systems.IsUnlocked(type))
                return "UPGRADE REQUIRED";
            if (!systems.IsMaintenanceReady(type))
                return "SERVICE REQUIRED";
            return systems.IsRequestedActive(type) ? "ACTIVE" : "STOPPED";
        }

        private string BuildUpgradeLine(
            StationSystemType type,
            StationSystemsController systems)
        {
            StationSystemDefinition definition = systems?.GetDefinition(type);
            if (definition == null || !definition.Upgradeable ||
                systems.GetUpgradeLevel(type) >= definition.MaxLevel)
            {
                return string.Empty;
            }

            int available = CountAvailable(definition.RequiredItemId);
            string itemName = ResolveItemName(definition.RequiredItemId);
            return $"\nUpgrade: {itemName} {available}/{definition.RequiredItemCount}";
        }

        private void RefreshActions()
        {
            StationSystemsController systems = StationSystemsController.Instance;
            StationSystemDefinition definition = systems?.GetDefinition(selectedSystem);
            PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
            StationStorageController storage = StationStorageController.Instance;

            bool canUpgrade = systems != null &&
                systems.CanUpgrade(selectedSystem, inventory, storage);
            if (upgradeButton != null)
            {
                upgradeButton.gameObject.SetActive(canUpgrade);
                SetButtonLabel(upgradeButton, "UPGRADE");
            }

            if (powerButton == null)
                return;

            bool controllable = definition != null && definition.Controllable;
            bool droneIsScanning = selectedSystem == StationSystemType.Drone &&
                DroneScanController.Instance != null &&
                DroneScanController.Instance.State == DroneState.Scanning;
            bool showPowerButton = controllable && !droneIsScanning;
            powerButton.gameObject.SetActive(showPowerButton);
            if (!showPowerButton)
                return;

            bool active = systems.IsRequestedActive(selectedSystem);
            SetButtonLabel(powerButton, active ? "STOP" : "START");
            powerButton.interactable = active || systems.CanStart(selectedSystem, out _);
        }

        private void RefreshStorage()
        {
            StationStorageController storage = StationStorageController.Instance;
            foreach (StorageView view in storageViews)
            {
                IReadOnlyList<ItemInstance> slots = storage?.GetSlots(view.Group);
                ItemData item = slots != null && view.Index < slots.Count
                    ? slots[view.Index]?.ItemData
                    : null;
                if (view.Icon != null)
                {
                    view.Icon.sprite = item != null ? item.Icon : null;
                    view.Icon.preserveAspect = true;
                    view.Icon.color = item != null && item.Icon == null
                        ? MissingIconColor
                        : Color.white;
                    view.Icon.enabled = item != null;
                }

                if (view.Button != null)
                    view.Button.interactable = true;

                view.Drag?.Initialize(
                    item,
                    rootCanvas,
                    view.Group,
                    view.Index,
                    false,
                    false,
                    true);
            }
        }

        private static int GetAuthoredSlotNumber(string slotName)
        {
            if (!string.IsNullOrWhiteSpace(slotName) &&
                slotName.StartsWith("Slot_", StringComparison.Ordinal) &&
                int.TryParse(slotName.Substring(5), out int number))
            {
                return number;
            }

            return int.MaxValue;
        }

        private int CountAvailable(string itemId)
        {
            PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
            return (inventory?.CountItem(itemId) ?? 0) +
                (StationStorageController.Instance?.CountItem(itemId) ?? 0);
        }

        private static string ResolveItemName(string itemId)
        {
            ItemCatalogData catalog = Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData item = catalog != null ? catalog.Find(itemId) : null;
            return item != null ? item.DisplayName : itemId;
        }

        private void Subscribe()
        {
            if (StationStorageController.Instance != null)
                StationStorageController.Instance.StorageChanged += RefreshAll;
            if (StationSystemsController.Instance != null)
                StationSystemsController.Instance.SystemsChanged += RefreshAll;
        }

        private void Unsubscribe()
        {
            if (StationStorageController.Instance != null)
                StationStorageController.Instance.StorageChanged -= RefreshAll;
            if (StationSystemsController.Instance != null)
                StationSystemsController.Instance.SystemsChanged -= RefreshAll;
        }

        private Transform FindChild(string objectName, Transform root = null)
        {
            root ??= transform;
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name == objectName)
                    return child;
            }
            return null;
        }

        private Button FindButton(string objectName)
        {
            Transform child = FindChild(objectName);
            return child != null ? child.GetComponent<Button>() : null;
        }

        private static TMP_Text FindText(Transform root, string objectName)
        {
            if (root == null)
                return null;
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text.name == objectName)
                    return text;
            }
            return null;
        }

        private static Image FindImage(Transform root, string objectName)
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image.name == objectName)
                    return image;
            }
            return null;
        }

        private static void SetButtonLabel(Button button, string value)
        {
            TMP_Text label = button != null
                ? button.GetComponentInChildren<TMP_Text>(true)
                : null;
            if (label != null)
                label.text = value;
        }

        private void OnDisable()
        {
            InventoryLabHUDController.Instance?.CloseStationStorage();
            Unsubscribe();
        }
    }
}
