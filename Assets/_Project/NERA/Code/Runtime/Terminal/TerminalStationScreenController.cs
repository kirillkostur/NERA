using System;
using System.Text;
using NERA.Antenna;
using NERA.Drone;
using NERA.Energy;
using NERA.Inventory;
using NERA.Items;
using NERA.Station;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Terminal
{
    public sealed class TerminalStationScreenController : MonoBehaviour
    {
        private TerminalUIScreen terminal;
        [SerializeField] private RawImage stationImage;
        [SerializeField] private Camera stationCamera;
        [SerializeField] private TMP_Text objectNameText;
        [SerializeField] private TMP_Text objectInfoText;
        [SerializeField] private Image objectImage;
        [SerializeField] private GameObject powerSwitchRoot;
        [SerializeField] private Button powerOnButton;
        [SerializeField] private Button powerOffButton;
        [SerializeField] private RectTransform powerHandle;
        [SerializeField] private Animator powerHandleAnimator;
        [SerializeField] private TMP_Text powerStatusText;
        [SerializeField] private GameObject statusPanel;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private GameObject upgradePanel;
        [SerializeField] private TMP_Text upgradeTitle;
        [SerializeField] private TMP_Text upgradeInfo;
        [SerializeField] private TMP_Text upgradeRequired;
        [SerializeField] private Button upgradeButton;
        private readonly Button[] levelButtons = new Button[3];
        private readonly GameObject[] levelRoots = new GameObject[3];
        private readonly TMP_Text[] levelLabels = new TMP_Text[3];
        private readonly Image[] levelIcons = new Image[3];

        private StationSystemType? selectedSystem;
        private string selectedObjectName;
        private string selectedObjectId;
        private int selectedObjectInitialLevel;
        private bool selectedObjectInitiallyActive;
        private int selectedUpgradeLevel = 1;
        private bool initialized;
        private bool preserveAuthoredSwitchAnimation;
        private bool forcePowerHandleSync;
        private StationSystemType? renderedPowerSystem;
        private string renderedPowerObjectId;
        private bool? renderedPowerActive;
        private StationSystemsController subscribedSystems;
        private EnergySystemController subscribedEnergy;
        private StationStorageController subscribedStorage;
        private PlayerInventory subscribedInventory;
        private DroneScanController subscribedDrone;
        private AntennaController subscribedAntenna;

        public StationSystemType? SelectedSystem => selectedSystem;
        public string SelectedObjectId => selectedObjectId;

        public void SelectSystem(StationSystemType type)
        {
            StationSystemDefinition definition =
                StationSystemsController.Instance?.GetDefinition(type);
            selectedSystem = type;
            selectedObjectName = definition?.DisplayName ?? type.ToString();
            selectedObjectId = string.Empty;
            selectedObjectInitialLevel = definition?.InitialLevel ?? 0;
            selectedObjectInitiallyActive =
                definition?.InitiallyActive == true;
            selectedUpgradeLevel = Mathf.Max(
                1,
                (StationSystemsController.Instance?.GetUpgradeLevel(type) ?? 0) + 1);
            RefreshAll();
        }

        public bool SelectPreviewObject(Transform target)
        {
            if (target == null)
                return false;

            ResolveStationObject(
                target,
                out selectedObjectName,
                out selectedSystem,
                out selectedObjectId,
                out selectedObjectInitialLevel,
                out selectedObjectInitiallyActive);
            StationSystemsController systems = StationSystemsController.Instance;
            selectedUpgradeLevel = selectedSystem.HasValue && systems != null
                ? Mathf.Clamp(
                    GetSelectedUpgradeLevel(systems) + 1,
                    1,
                    Mathf.Max(
                        1,
                        systems.Config.GetMaxLevel(
                            selectedSystem.Value,
                            selectedObjectId)))
                : 1;
            RefreshAll();
            return selectedSystem.HasValue;
        }

        public void Initialize(TerminalUIScreen owner)
        {
            terminal = owner;
            if (initialized)
                return;

            initialized = true;
            CacheHierarchy();
            BindButtons();
            ConfigurePreviewPicking();
            if (statusPanel != null)
                statusPanel.SetActive(false);
            if (upgradePanel != null)
                upgradePanel.SetActive(false);
            ClearSelection();
            SetScreenActive(false);
        }

        public void SetScreenActive(bool active)
        {
            bool shouldRender =
                active &&
                terminal != null &&
                terminal.IsOpen;
            if (stationCamera != null)
                stationCamera.enabled = shouldRender;

            if (!shouldRender)
            {
                UnbindDataEvents();
                TerminalUIUtility.ReleaseCameraTarget(stationCamera);
                return;
            }

            BindDataEvents();
            ShowDetailTab(false);
            RefreshAll();
        }

        private void CacheHierarchy()
        {
            stationImage ??= TerminalUIUtility.FindComponent<RawImage>(
                transform, "Station_RawImage");
            stationCamera ??= TerminalUIUtility.FindComponent<Camera>(
                transform, "StationUICamera");
            objectNameText ??= TerminalUIUtility.FindComponent<TMP_Text>(
                transform, "Text_nameObj");
            objectInfoText ??= TerminalUIUtility.FindComponent<TMP_Text>(
                transform, "Text_info_obj");
            objectImage ??= TerminalUIUtility.FindComponent<Image>(
                transform, "Image_obj");

            Transform toggleRoot = powerSwitchRoot != null
                ? powerSwitchRoot.transform
                : TerminalUIUtility.Find(transform, "Toggle");
            if (toggleRoot != null)
            {
                powerSwitchRoot ??= toggleRoot.gameObject;
                powerOnButton ??= TerminalUIUtility.FindComponent<Button>(
                    toggleRoot, "OnButton");
                powerOffButton ??= TerminalUIUtility.FindComponent<Button>(
                    toggleRoot, "OffButton");
                powerHandle ??= TerminalUIUtility.FindComponent<RectTransform>(
                    toggleRoot, "Handle");
                powerHandleAnimator ??= powerHandle != null
                    ? powerHandle.GetComponent<Animator>()
                    : null;
                powerStatusText ??= TerminalUIUtility.FindComponent<TMP_Text>(
                    toggleRoot, "Text_Status");
            }

            statusPanel ??= TerminalUIUtility.Find(
                transform, "background_Status")?.gameObject;
            statusText ??= TerminalUIUtility.FindComponent<TMP_Text>(
                statusPanel != null ? statusPanel.transform : transform,
                "Text_description");
            upgradePanel ??= TerminalUIUtility.Find(
                transform, "background_Upgrade")?.gameObject;
            upgradeTitle ??= TerminalUIUtility.FindComponent<TMP_Text>(
                upgradePanel != null ? upgradePanel.transform : transform,
                "description_update");
            upgradeInfo ??= TerminalUIUtility.FindComponent<TMP_Text>(
                upgradePanel != null ? upgradePanel.transform : transform,
                "info_update");
            upgradeRequired ??= TerminalUIUtility.FindComponent<TMP_Text>(
                upgradePanel != null ? upgradePanel.transform : transform,
                "info_required");
            upgradeButton ??= TerminalUIUtility.FindComponent<Button>(
                upgradePanel != null ? upgradePanel.transform : transform,
                "UpgradeButton");

            for (int i = 0; i < levelButtons.Length; i++)
            {
                Transform levelRoot = TerminalUIUtility.Find(
                    upgradePanel != null ? upgradePanel.transform : transform,
                    $"Slot_LVL_{i + 1}");
                levelRoots[i] = levelRoot != null
                    ? levelRoot.gameObject
                    : null;
                levelButtons[i] = TerminalUIUtility.EnsureButton(levelRoot);
                levelLabels[i] = TerminalUIUtility.FindComponent<TMP_Text>(
                    levelRoot,
                    "Text_info_LVL");
                levelIcons[i] = EnsureUpgradeIcon(levelRoot);
            }
        }

        private static Image EnsureUpgradeIcon(Transform levelRoot)
        {
            if (levelRoot == null)
                return null;

            Transform existing = levelRoot.Find("Image_Icon");
            Image icon = existing != null
                ? existing.GetComponent<Image>()
                : null;
            if (icon == null)
            {
                GameObject iconObject = new GameObject(
                    "Image_Icon",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                RectTransform iconTransform =
                    iconObject.GetComponent<RectTransform>();
                iconTransform.SetParent(levelRoot, false);
                iconTransform.anchorMin = Vector2.zero;
                iconTransform.anchorMax = Vector2.one;
                iconTransform.offsetMin = new Vector2(8f, 8f);
                iconTransform.offsetMax = new Vector2(-8f, -8f);
                iconTransform.SetAsFirstSibling();
                icon = iconObject.GetComponent<Image>();
            }

            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.enabled = false;
            return icon;
        }

        private void BindButtons()
        {
            TerminalUIUtility.FindComponent<Button>(
                transform, "StatusMapButton")?.onClick.AddListener(
                () => ShowDetailTab(false));
            TerminalUIUtility.FindComponent<Button>(
                transform, "UpgradesMapButton")?.onClick.AddListener(
                () => ShowDetailTab(true));
            powerOnButton?.onClick.AddListener(
                () => HandlePowerSwitchChanged(false));
            powerOffButton?.onClick.AddListener(
                () => HandlePowerSwitchChanged(true));
            upgradeButton?.onClick.AddListener(PerformUpgrade);

            for (int i = 0; i < levelButtons.Length; i++)
            {
                int targetLevel = i + 1;
                levelButtons[i]?.onClick.AddListener(
                    () => SelectUpgradeLevel(targetLevel));
            }
        }

        private void ConfigurePreviewPicking()
        {
            if (stationImage == null)
                return;

            UIPreviewRaycaster picker =
                stationImage.GetComponent<UIPreviewRaycaster>() ??
                stationImage.gameObject.AddComponent<UIPreviewRaycaster>();
            picker.Initialize(stationImage, stationCamera, HandlePreviewHit);
        }

        private void HandlePreviewHit(RaycastHit hit)
        {
            Transform target = hit.collider != null
                ? hit.collider.transform
                : hit.transform;
            if (target == null)
                return;

            SelectPreviewObject(target);
        }

        private void ResolveStationObject(
            Transform hit,
            out string objectName,
            out StationSystemType? system,
            out string objectId,
            out int initialLevel,
            out bool initiallyActive)
        {
            objectName = hit != null ? hit.name : string.Empty;
            system = null;
            objectId = string.Empty;
            initialLevel = 0;
            initiallyActive = false;
            Transform fallback = null;
            Transform current = hit;
            while (current != null && current != transform)
            {
                StationSystemsConfig config =
                    StationSystemsController.Instance?.Config ??
                    StationSystemsConfig.LoadDefault();
                StationSystemDefinition configuredObject =
                    config.FindByPreviewName(current.name);
                if (configuredObject != null)
                {
                    system = configuredObject.SystemType;
                    objectName =
                        !string.IsNullOrWhiteSpace(
                            configuredObject.DisplayName)
                            ? configuredObject.DisplayName
                            : current.name;
                    objectId = configuredObject.ObjectId;
                    initialLevel = configuredObject.InitialLevel;
                    initiallyActive = configuredObject.InitiallyActive;
                    return;
                }

                if (fallback == null &&
                    current.name.StartsWith("SM_", StringComparison.Ordinal))
                {
                    fallback = current;
                }
                current = current.parent;
            }

            if (fallback != null)
                objectName = fallback.name;
        }

        private void ClearSelection()
        {
            selectedSystem = null;
            selectedObjectName = string.Empty;
            selectedObjectId = string.Empty;
            selectedObjectInitialLevel = 0;
            selectedObjectInitiallyActive = false;
            TerminalUIUtility.SetText(objectNameText, "SELECT STATION OBJECT");
            TerminalUIUtility.SetText(
                objectInfoText,
                "Select an object in the 3D station preview.");
            if (powerSwitchRoot != null)
                powerSwitchRoot.SetActive(false);
            renderedPowerSystem = null;
            renderedPowerObjectId = string.Empty;
            renderedPowerActive = null;
            preserveAuthoredSwitchAnimation = false;
            forcePowerHandleSync = false;
            RefreshUpgrade();
        }

        private void RefreshAll()
        {
            RefreshObjectInfo();
            RefreshPowerSwitch();
            RefreshStatus();
            RefreshUpgrade();
        }

        private void RefreshObjectInfo()
        {
            if (!selectedSystem.HasValue)
            {
                if (!string.IsNullOrWhiteSpace(selectedObjectName))
                {
                    TerminalUIUtility.SetText(
                        objectNameText,
                        FormatObjectName(selectedObjectName));
                    TerminalUIUtility.SetText(
                        objectInfoText,
                        "Station module. No remote power controls are available.");
                }
                return;
            }

            StationSystemDefinition definition =
                StationSystemsController.Instance?.GetDefinition(
                    selectedSystem.Value,
                    selectedObjectId);
            TerminalUIUtility.SetText(
                objectNameText,
                !string.IsNullOrWhiteSpace(selectedObjectName)
                    ? FormatObjectName(selectedObjectName)
                    : definition?.DisplayName ??
                      selectedSystem.Value.ToString());
            TerminalUIUtility.SetText(
                objectInfoText,
                definition?.Description ?? string.Empty);

            // The illustration is authored by the user. Keep an assigned sprite
            // intact and only control whether an actual sprite is present.
            if (objectImage != null)
                objectImage.enabled = objectImage.sprite != null;
        }

        private void RefreshPowerSwitch()
        {
            if (powerSwitchRoot == null)
                return;

            bool visible = selectedSystem.HasValue;
            powerSwitchRoot.SetActive(visible);
            if (!visible)
                return;

            StationSystemType type = selectedSystem.Value;
            StationSystemsController systems = StationSystemsController.Instance;
            bool critical =
                type == StationSystemType.Battery ||
                type == StationSystemType.Computer;
            bool controllable =
                critical ||
                systems?.GetDefinition(
                    type,
                    selectedObjectId)?.Controllable == true;
            bool active = IsSelectedSystemActive(type, systems);
            bool canChangeState = active ||
                critical ||
                systems?.CanStart(
                    type,
                    selectedObjectId,
                    selectedObjectInitialLevel,
                    out _) == true;
            bool interactable = controllable &&
                canChangeState &&
                !(type == StationSystemType.Drone &&
                  DroneScanController.Instance?.State == DroneState.Scanning);

            bool visualStateChanged =
                renderedPowerSystem != selectedSystem ||
                !string.Equals(
                    renderedPowerObjectId,
                    selectedObjectId,
                    StringComparison.OrdinalIgnoreCase) ||
                renderedPowerActive != active;
            if (powerOnButton != null)
            {
                powerOnButton.gameObject.SetActive(active);
                powerOnButton.interactable = interactable;
            }
            if (powerOffButton != null)
            {
                powerOffButton.gameObject.SetActive(!active);
                powerOffButton.interactable = interactable;
            }

            if (forcePowerHandleSync ||
                visualStateChanged && !preserveAuthoredSwitchAnimation)
            {
                SetPowerHandleState(active);
            }

            TerminalUIUtility.SetText(
                powerStatusText,
                active ? "Active" : "Inactive");
            if (powerStatusText != null)
            {
                powerStatusText.color = active
                    ? new Color(0.55f, 1f, 0.62f, 1f)
                    : new Color(1f, 0.48f, 0.42f, 1f);
            }

            renderedPowerSystem = selectedSystem;
            renderedPowerObjectId = selectedObjectId;
            renderedPowerActive = active;
            preserveAuthoredSwitchAnimation = false;
            forcePowerHandleSync = false;
        }

        private void SetPowerHandleState(bool active)
        {
            if (powerHandleAnimator != null &&
                powerHandleAnimator.runtimeAnimatorController != null)
            {
                powerHandleAnimator.Play(
                    active ? "ToggleOn_clip" : "ToggleOff_clip",
                    0,
                    1f);
                powerHandleAnimator.Update(0f);
            }

            if (powerHandle == null)
                return;

            Vector2 position = powerHandle.anchoredPosition;
            position.x = active ? 25f : -25f;
            powerHandle.anchoredPosition = position;
        }

        private bool IsSelectedSystemActive(
            StationSystemType type,
            StationSystemsController systems)
        {
            return type == StationSystemType.Battery
                ? EnergySystemController.Instance?.GridEnabled == true
                : systems == null || systems.IsRequestedActive(
                    type,
                    selectedObjectId,
                    selectedObjectInitialLevel,
                    selectedObjectInitiallyActive);
        }

        private void HandlePowerSwitchChanged(bool active)
        {
            if (!selectedSystem.HasValue)
                return;

            StationSystemType type = selectedSystem.Value;
            StationSystemsController systems = StationSystemsController.Instance;
            bool changed;
            if (type == StationSystemType.Battery)
            {
                changed = systems?.SetCriticalSystemActive(type, active) == true;
                if (changed)
                {
                    StationPowerController.Instance?.SetState(
                        active
                            ? StationPowerState.Online
                            : StationPowerState.Offline);
                }
            }
            else if (type == StationSystemType.Computer)
            {
                changed = systems?.SetCriticalSystemActive(type, active) == true;
            }
            else
            {
                changed = systems?.SetRequestedActive(
                    type,
                    active,
                    selectedObjectId,
                    selectedObjectInitialLevel,
                    selectedObjectInitiallyActive) == true;
                if (changed)
                {
                    AntennaController.Instance?.RefreshAvailability();
                    DroneScanController.Instance?.RefreshAvailability();
                }
            }

            preserveAuthoredSwitchAnimation = changed;
            forcePowerHandleSync = !changed;
            if (!changed || !active &&
                (type == StationSystemType.Battery ||
                 type == StationSystemType.Computer))
            {
                RefreshPowerSwitch();
            }
            else
            {
                RefreshPowerSwitch();
                RefreshStatus();
            }

            if (!active &&
                (type == StationSystemType.Battery ||
                 type == StationSystemType.Computer))
            {
                terminal?.Close();
            }
        }

        private void ShowDetailTab(bool showUpgrade)
        {
            if (statusPanel != null)
                statusPanel.SetActive(!showUpgrade);
            if (upgradePanel != null)
                upgradePanel.SetActive(showUpgrade);
            if (showUpgrade)
                RefreshUpgrade();
            else
                RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (statusText == null)
                return;

            EnergySystemController energy = EnergySystemController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            if (!selectedSystem.HasValue)
            {
                TerminalUIUtility.SetText(statusText, "NO OBJECT SELECTED");
                return;
            }

            StationSystemType type = selectedSystem.Value;
            string text;
            if (type == StationSystemType.Battery)
            {
                text =
                    $"Charge - {energy?.CurrentEnergy ?? 0f:0}/{energy?.TotalCapacity ?? 0f:0}\n" +
                    $"Consumption - {energy?.CurrentConsumption ?? 0f:0.0}\n" +
                    $"Connected objects - {energy?.ActiveConsumerCount ?? 0}";
            }
            else if (type == StationSystemType.SolarPanel)
            {
                text =
                    $"Status - {(energy?.GridEnabled == true ? "ACTIVE" : "OFFLINE")}\n" +
                    $"Generation - {energy?.CurrentGeneration ?? 0f:0.0}\n" +
                    $"Efficiency - {(systems?.GetCondition(type) ?? 1f) * 100f:0}%";
            }
            else
            {
                bool active = systems == null ||
                    systems.IsRequestedActive(
                        type,
                        selectedObjectId,
                        selectedObjectInitialLevel,
                        selectedObjectInitiallyActive);
                float consumption = GetConfiguredConsumption(type, energy);
                string consumptionLine = consumption > 0f
                    ? $"\nConsumption - {consumption:0.0}"
                    : string.Empty;
                text =
                    $"Status - {(active ? "ACTIVE" : "STOPPED")}" +
                    consumptionLine + "\n" +
                    $"Condition - {(systems?.GetCondition(type, selectedObjectId) ?? 1f) * 100f:0}%\n" +
                    $"Upgrade level - {GetSelectedUpgradeLevel(systems)}";
            }

            TerminalUIUtility.SetText(statusText, text);
        }

        private static float GetConfiguredConsumption(
            StationSystemType type,
            EnergySystemController energy)
        {
            EnergyBalanceConfig config = energy?.Config;
            if (config == null)
                return 0f;

            return type switch
            {
                StationSystemType.Computer => config.TerminalConsumption,
                StationSystemType.Drone => config.DroneChargingConsumption,
                StationSystemType.Laboratory => config.LaboratoryConsumption,
                StationSystemType.Charger => config.ItemChargingConsumption,
                StationSystemType.Antenna =>
                    config.AntennaCalibrationConsumption,
                StationSystemType.Turret => config.TurretIdleConsumption,
                _ => 0f
            };
        }

        private void BindDataEvents()
        {
            BindSystemEvents();

            EnergySystemController energy = EnergySystemController.Instance;
            if (subscribedEnergy != energy)
            {
                if (subscribedEnergy != null)
                    subscribedEnergy.EnergyChanged -= HandleDataChanged;
                subscribedEnergy = energy;
                if (subscribedEnergy != null)
                    subscribedEnergy.EnergyChanged += HandleDataChanged;
            }

            StationStorageController storage =
                StationStorageController.Instance;
            if (subscribedStorage != storage)
            {
                if (subscribedStorage != null)
                    subscribedStorage.StorageChanged -= HandleDataChanged;
                subscribedStorage = storage;
                if (subscribedStorage != null)
                    subscribedStorage.StorageChanged += HandleDataChanged;
            }

            PlayerInventory inventory =
                InventoryLabHUDController.Instance?.BoundInventory ??
                FindFirstObjectByType<PlayerInventory>();
            if (subscribedInventory != inventory)
            {
                if (subscribedInventory != null)
                    subscribedInventory.InventoryChanged -= HandleDataChanged;
                subscribedInventory = inventory;
                if (subscribedInventory != null)
                    subscribedInventory.InventoryChanged += HandleDataChanged;
            }

            DroneScanController drone = DroneScanController.Instance;
            if (subscribedDrone != drone)
            {
                if (subscribedDrone != null)
                    subscribedDrone.StateChanged -= HandleDroneStateChanged;
                subscribedDrone = drone;
                if (subscribedDrone != null)
                    subscribedDrone.StateChanged += HandleDroneStateChanged;
            }

            AntennaController antenna = AntennaController.Instance;
            if (subscribedAntenna != antenna)
            {
                if (subscribedAntenna != null)
                {
                    subscribedAntenna.StateChanged -= HandleAntennaStateChanged;
                    subscribedAntenna.ConditionChanged -=
                        HandleAntennaConditionChanged;
                }

                subscribedAntenna = antenna;
                if (subscribedAntenna != null)
                {
                    subscribedAntenna.StateChanged += HandleAntennaStateChanged;
                    subscribedAntenna.ConditionChanged +=
                        HandleAntennaConditionChanged;
                }
            }
        }

        private void BindSystemEvents()
        {
            StationSystemsController current = StationSystemsController.Instance;
            if (subscribedSystems == current)
                return;

            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged -= HandleSystemsChanged;
            subscribedSystems = current;
            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged += HandleSystemsChanged;
        }

        private void HandleSystemsChanged()
        {
            RefreshIfVisible();
        }

        private void HandleDataChanged()
        {
            RefreshIfVisible();
        }

        private void HandleDroneStateChanged(DroneState _)
        {
            RefreshIfVisible();
        }

        private void HandleAntennaStateChanged(AntennaState _)
        {
            RefreshIfVisible();
        }

        private void HandleAntennaConditionChanged(float _)
        {
            RefreshIfVisible();
        }

        private void RefreshIfVisible()
        {
            if (terminal?.IsOpen == true && gameObject.activeInHierarchy)
                RefreshAll();
        }

        private void UnbindDataEvents()
        {
            UnbindSystemEvents();

            if (subscribedEnergy != null)
            {
                subscribedEnergy.EnergyChanged -= HandleDataChanged;
                subscribedEnergy = null;
            }

            if (subscribedStorage != null)
            {
                subscribedStorage.StorageChanged -= HandleDataChanged;
                subscribedStorage = null;
            }

            if (subscribedInventory != null)
            {
                subscribedInventory.InventoryChanged -= HandleDataChanged;
                subscribedInventory = null;
            }

            if (subscribedDrone != null)
            {
                subscribedDrone.StateChanged -= HandleDroneStateChanged;
                subscribedDrone = null;
            }

            if (subscribedAntenna != null)
            {
                subscribedAntenna.StateChanged -= HandleAntennaStateChanged;
                subscribedAntenna.ConditionChanged -=
                    HandleAntennaConditionChanged;
                subscribedAntenna = null;
            }
        }

        private void UnbindSystemEvents()
        {
            if (subscribedSystems == null)
                return;

            subscribedSystems.SystemsChanged -= HandleSystemsChanged;
            subscribedSystems = null;
        }

        private void SelectUpgradeLevel(int targetLevel)
        {
            int maxLevel = selectedSystem.HasValue
                ? StationSystemsController.Instance?.Config.GetMaxLevel(
                    selectedSystem.Value,
                    selectedObjectId) ?? 1
                : 1;
            selectedUpgradeLevel = Mathf.Clamp(targetLevel, 1, maxLevel);
            RefreshUpgrade();
        }

        private void RefreshUpgrade()
        {
            StationSystemsController systems = StationSystemsController.Instance;
            PlayerInventory inventory =
                InventoryLabHUDController.Instance?.BoundInventory ??
                FindFirstObjectByType<PlayerInventory>();
            StationStorageController storage = StationStorageController.Instance;

            if (!selectedSystem.HasValue || systems == null)
            {
                SetUpgradeSlotsVisible(false);
                TerminalUIUtility.SetText(upgradeTitle, "NO OBJECT SELECTED");
                TerminalUIUtility.SetText(upgradeInfo, string.Empty);
                TerminalUIUtility.SetText(upgradeRequired, string.Empty);
                if (upgradeButton != null)
                    upgradeButton.interactable = false;
                return;
            }

            StationSystemType type = selectedSystem.Value;
            StationSystemDefinition definition =
                systems.GetDefinition(type, selectedObjectId);
            string upgradeSystemName =
                !string.IsNullOrWhiteSpace(selectedObjectId)
                    ? type.ToString().ToUpperInvariant()
                    : definition?.DisplayName ?? type.ToString();
            int currentLevel = GetSelectedUpgradeLevel(systems);
            int maxLevel = systems.Config.GetMaxLevel(
                type,
                selectedObjectId);
            selectedUpgradeLevel = Mathf.Clamp(
                selectedUpgradeLevel,
                1,
                Mathf.Max(1, maxLevel));

            for (int i = 0; i < levelButtons.Length; i++)
            {
                int level = i + 1;
                Button button = levelButtons[i];
                StationUpgradeLevelDefinition levelDefinition =
                    definition?.GetUpgradeDefinition(level);
                bool configured = definition?.Upgradeable == true &&
                    level <= maxLevel &&
                    levelDefinition != null;
                if (levelRoots[i] != null)
                    levelRoots[i].SetActive(configured);
                TerminalUIUtility.SetText(
                    levelLabels[i],
                    $"{upgradeSystemName} {level}");
                if (levelIcons[i] != null)
                {
                    levelIcons[i].sprite = levelDefinition?.UpgradeIcon;
                    levelIcons[i].enabled =
                        configured && levelIcons[i].sprite != null;
                }
                if (button == null || !configured)
                    continue;

                button.interactable = true;
                Image image = button.targetGraphic as Image;
                if (image != null)
                {
                    image.color = level <= currentLevel
                        ? new Color(0.1f, 0.72f, 0.58f, 1f)
                        : level == selectedUpgradeLevel
                            ? new Color(0.15f, 0.48f, 0.68f, 1f)
                            : Color.white;
                }
            }

            StationUpgradeLevelDefinition upgrade =
                systems.Config.GetUpgradeDefinition(
                    type,
                    selectedObjectId,
                    selectedUpgradeLevel);
            StringBuilder requirementsText = new StringBuilder();
            if (upgrade != null)
            {
                foreach (StationUpgradeItemRequirement requirement in
                         upgrade.RequiredItems)
                {
                    if (requirement == null ||
                        string.IsNullOrWhiteSpace(requirement.ItemId))
                    {
                        continue;
                    }

                    int available =
                        (inventory?.CountItem(requirement.ItemId) ?? 0) +
                        (storage?.CountItem(requirement.ItemId) ?? 0);
                    if (requirementsText.Length > 0)
                        requirementsText.Append('\n');
                    requirementsText.Append(
                        $"{requirement.DisplayName} - " +
                        $"{available}/{requirement.Count}");
                }

                if (requirementsText.Length > 0)
                    requirementsText.Append('\n');
                requirementsText.Append($"Energy - {upgrade.EnergyCost:0}");
            }

            TerminalUIUtility.SetText(
                upgradeTitle,
                !string.IsNullOrWhiteSpace(upgrade?.DisplayName)
                    ? upgrade.DisplayName
                    : $"{definition?.DisplayName ?? type.ToString()} " +
                      $"{selectedUpgradeLevel}");
            TerminalUIUtility.SetText(
                upgradeInfo,
                upgrade?.Description ??
                "Upgrade level is not configured.");
            TerminalUIUtility.SetText(
                upgradeRequired,
                requirementsText.ToString());

            bool canUpgrade = systems.CanUpgradeTo(
                type,
                selectedObjectId,
                selectedObjectInitialLevel,
                selectedUpgradeLevel,
                inventory,
                storage,
                out _);
            if (upgradeButton != null)
                upgradeButton.interactable = canUpgrade;
        }

        private void PerformUpgrade()
        {
            if (!selectedSystem.HasValue)
                return;

            PlayerInventory inventory =
                InventoryLabHUDController.Instance?.BoundInventory ??
                FindFirstObjectByType<PlayerInventory>();
            if (StationSystemsController.Instance?.TryUpgradeTo(
                    selectedSystem.Value,
                    selectedObjectId,
                    selectedObjectInitialLevel,
                    selectedUpgradeLevel,
                    inventory,
                    StationStorageController.Instance) == true)
            {
                selectedUpgradeLevel = Mathf.Clamp(
                    selectedUpgradeLevel + 1,
                    1,
                    StationSystemsController.Instance.GetDefinition(
                        selectedSystem.Value,
                        selectedObjectId) != null
                        ? StationSystemsController.Instance.Config.GetMaxLevel(
                            selectedSystem.Value,
                            selectedObjectId)
                        : 1);
                AntennaController.Instance?.RefreshAvailability();
                DroneScanController.Instance?.RefreshAvailability();
                RefreshAll();
            }
        }

        private int GetSelectedUpgradeLevel(
            StationSystemsController systems)
        {
            return selectedSystem.HasValue && systems != null
                ? systems.GetUpgradeLevel(
                    selectedSystem.Value,
                    selectedObjectId,
                    selectedObjectInitialLevel)
                : 0;
        }

        private void SetUpgradeSlotsVisible(bool visible)
        {
            foreach (GameObject levelRoot in levelRoots)
            {
                if (levelRoot != null)
                    levelRoot.SetActive(visible);
            }
        }

        private static string FormatObjectName(string objectName)
        {
            return objectName
                .Replace("SM_", string.Empty)
                .Replace("_", " ")
                .ToUpperInvariant();
        }

        private void OnDestroy()
        {
            UnbindDataEvents();
        }
    }
}
