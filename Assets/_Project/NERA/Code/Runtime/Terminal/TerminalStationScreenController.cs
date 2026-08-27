using System;
using System.Text;
using NERA.Antenna;
using NERA.Drone;
using NERA.Energy;
using NERA.Localization;
using NERA.Maintenance;
using NERA.Station;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Terminal
{
    /// <summary>
    /// Read-only station status screen. Physical upgrades are installed on the
    /// world object and reflected here through its preview slots and stats.
    /// </summary>
    public sealed class TerminalStationScreenController : MonoBehaviour
    {
        private const float DataRefreshInterval = 0.1f;
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
        [SerializeField] private TMP_Text statusTabLabel;
        [SerializeField] private Button statusTabButton;
        [SerializeField] private GameObject statusPanel;
        [SerializeField] private TMP_Text statusText;

        private StationSystemType? selectedSystem;
        private string selectedObjectName;
        private string selectedObjectId;
        private bool initialized;
        private bool preserveAuthoredSwitchAnimation;
        private bool forcePowerHandleSync;
        private StationSystemType? renderedPowerSystem;
        private string renderedPowerObjectId;
        private bool? renderedPowerActive;
        private StationSystemsController subscribedSystems;
        private EnergySystemController subscribedEnergy;
        private DroneScanController subscribedDrone;
        private AntennaController subscribedAntenna;
        private bool dataRefreshPending;
        private float nextDataRefreshAt;

        public StationSystemType? SelectedSystem => selectedSystem;
        public string SelectedObjectId => selectedObjectId;

        private void Update()
        {
            if (!dataRefreshPending ||
                Time.unscaledTime < nextDataRefreshAt ||
                terminal?.IsOpen != true ||
                !gameObject.activeInHierarchy)
            {
                return;
            }

            RefreshAll();
        }

        public void SelectSystem(StationSystemType type)
        {
            StationSystemDefinition definition =
                StationSystemsController.Instance?.GetDefinition(type) ??
                StationSystemsConfig.LoadDefault()?.Find(type);
            selectedSystem = type;
            selectedObjectName = definition?.DisplayName ?? type.ToString();
            selectedObjectId = definition?.ObjectId ?? string.Empty;
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
                out selectedObjectId);
            RefreshAll();
            return selectedSystem.HasValue;
        }

        public void Initialize(TerminalUIScreen owner)
        {
            terminal = owner;
            if (initialized)
                return;
            initialized = true;
            NERALocalization.LocaleChanged += RefreshIfVisible;
            CacheHierarchy();
            BindButtons();
            ConfigurePreviewPicking();
            if (statusPanel != null)
                statusPanel.SetActive(false);
            ClearSelection();
            SetScreenActive(false);
        }

        public void SetScreenActive(bool active)
        {
            bool shouldRender = active && terminal != null && terminal.IsOpen;
            if (stationCamera != null)
                stationCamera.enabled = shouldRender;
            if (!shouldRender)
            {
                UnbindDataEvents();
                TerminalUIUtility.ReleaseCameraTarget(stationCamera);
                return;
            }
            BindDataEvents();
            if (statusPanel != null)
                statusPanel.SetActive(true);
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

            statusTabButton ??= TerminalUIUtility.FindComponent<Button>(
                transform, "StatusMapButton");
            statusTabLabel ??= statusTabButton != null
                ? statusTabButton.GetComponentInChildren<TMP_Text>(true)
                : null;
            statusPanel ??= TerminalUIUtility.Find(
                transform, "background_Status")?.gameObject;
            statusText ??= TerminalUIUtility.FindComponent<TMP_Text>(
                statusPanel != null ? statusPanel.transform : transform,
                "Text_description");
        }

        private void BindButtons()
        {
            statusTabButton?.onClick.AddListener(RefreshStatus);
            powerOnButton?.onClick.AddListener(
                () => HandlePowerSwitchChanged(false));
            powerOffButton?.onClick.AddListener(
                () => HandlePowerSwitchChanged(true));
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
            if (target != null)
                SelectPreviewObject(target);
        }

        private void ResolveStationObject(
            Transform hit,
            out string objectName,
            out StationSystemType? system,
            out string objectId)
        {
            objectName = hit != null ? hit.name : string.Empty;
            system = null;
            objectId = string.Empty;
            Transform current = hit;
            StationSystemsConfig config =
                StationSystemsController.Instance?.Config ??
                StationSystemsConfig.LoadDefault();
            while (current != null && current != transform)
            {
                StationObjectIdentity identity =
                    current.GetComponent<StationObjectIdentity>();
                StationSystemDefinition definition =
                    identity?.ResolveDefinition(config);
                if (definition != null)
                {
                    system = definition.SystemType;
                    objectName = definition.DisplayName;
                    objectId = definition.ObjectId;
                    return;
                }
                current = current.parent;
            }
        }

        private void ClearSelection()
        {
            selectedSystem = null;
            selectedObjectName = string.Empty;
            selectedObjectId = string.Empty;
            TerminalUIUtility.SetText(
                objectNameText,
                Localize("station.select_object", "SELECT STATION OBJECT"));
            TerminalUIUtility.SetText(
                objectInfoText,
                Localize(
                    "station.select_object_hint",
                    "Select an object in the 3D station preview."));
            if (powerSwitchRoot != null)
                powerSwitchRoot.SetActive(false);
            renderedPowerSystem = null;
            renderedPowerObjectId = string.Empty;
            renderedPowerActive = null;
            preserveAuthoredSwitchAnimation = false;
            forcePowerHandleSync = false;
            RefreshStatus();
        }

        private void RefreshAll()
        {
            dataRefreshPending = false;
            nextDataRefreshAt = Time.unscaledTime + DataRefreshInterval;
            TerminalUIUtility.SetText(
                statusTabLabel,
                Localize("station.tab.status", "STATUS"));
            RefreshObjectInfo();
            RefreshPowerSwitch();
            RefreshStatus();
        }

        private void RefreshObjectInfo()
        {
            if (!selectedSystem.HasValue)
                return;
            StationSystemDefinition definition =
                StationSystemsController.Instance?.GetDefinition(
                    selectedSystem.Value,
                    selectedObjectId) ??
                StationSystemsConfig.LoadDefault()?.Find(
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
            bool critical = type == StationSystemType.Battery ||
                type == StationSystemType.Terminal;
            bool controllable = critical ||
                systems?.GetDefinition(type, selectedObjectId)?.Controllable ==
                    true;
            bool requestedActive = IsSelectedSystemRequestedActive(type, systems);
            bool hasRequiredCharge = critical ||
                HasSelectedSystemRequiredCharge(type, systems);
            bool maintenanceReady = critical ||
                systems?.IsMaintenanceReady(type, selectedObjectId) != false;
            bool active = requestedActive &&
                hasRequiredCharge &&
                maintenanceReady;
            bool lowPower = !critical && requestedActive && !hasRequiredCharge;
            bool canChangeState = active || critical ||
                systems?.CanStart(type, selectedObjectId, out _) == true;
            bool interactable = controllable && canChangeState &&
                !(type == StationSystemType.Drone &&
                  DroneScanController.Instance?.IsAtStation == false);

            bool visualStateChanged = renderedPowerSystem != selectedSystem ||
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
                lowPower
                    ? Localize("station.power.low", "Low Power")
                    : active
                        ? Localize("station.power.active", "Active")
                        : Localize("station.power.inactive", "Inactive"));
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

        private bool IsSelectedSystemRequestedActive(
            StationSystemType type,
            StationSystemsController systems)
        {
            return type == StationSystemType.Battery
                ? EnergySystemController.Instance?.GridEnabled == true
                : systems == null ||
                    systems.IsRequestedActive(type, selectedObjectId);
        }

        private bool HasSelectedSystemRequiredCharge(
            StationSystemType type,
            StationSystemsController systems)
        {
            if (systems != null)
                return systems.HasRequiredCharge(type, selectedObjectId);
            EnergySystemController energy = EnergySystemController.Instance;
            return energy != null && energy.HasSufficientCharge(
                energy.Config.GetMinimumCharge01(type, selectedObjectId));
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
            else if (type == StationSystemType.Terminal)
            {
                changed = systems?.SetCriticalSystemActive(type, active) == true;
            }
            else
            {
                changed = systems?.SetRequestedActive(
                    type,
                    active,
                    selectedObjectId) == true;
                if (changed)
                {
                    AntennaController.Instance?.RefreshAvailability();
                    DroneScanController.Instance?.RefreshAvailability();
                }
            }

            preserveAuthoredSwitchAnimation = changed;
            forcePowerHandleSync = !changed;
            RefreshPowerSwitch();
            RefreshStatus();
            if (!active &&
                (type == StationSystemType.Battery ||
                 type == StationSystemType.Terminal))
            {
                terminal?.Close();
            }
        }

        private void RefreshStatus()
        {
            if (statusText == null)
                return;
            if (!selectedSystem.HasValue)
            {
                TerminalUIUtility.SetText(
                    statusText,
                    Localize("station.no_object_selected", "NO OBJECT SELECTED"));
                return;
            }

            StationSystemType type = selectedSystem.Value;
            StationSystemsController systems = StationSystemsController.Instance;
            StationSystemDefinition definition = systems?.GetDefinition(
                type,
                selectedObjectId) ?? StationSystemsConfig.LoadDefault()?.Find(
                type,
                selectedObjectId);
            var builder = new StringBuilder();
            if (StationSystemsController.UsesCondition(type))
            {
                builder.Append(Localize(
                    "station.status.condition",
                    "Condition"));
                builder.Append(" - ");
                builder.Append(
                    ((systems?.GetCondition(type, selectedObjectId) ?? 1f) *
                     100f).ToString("F0"));
                builder.AppendLine("%");
            }

            if (definition != null)
            {
                foreach (StationObjectStatDefinition stat in definition.BaseStats)
                {
                    if (stat == null)
                        continue;
                    float value = systems?.GetStat(
                        type,
                        selectedObjectId,
                        stat.Stat,
                        stat.BaseValue) ?? stat.BaseValue;
                    string statKey = NERALocalization.NormalizeKeyPart(
                        stat.Stat.ToString());
                    builder.Append(Localize(
                        $"station.stat.{statKey}",
                        stat.DisplayName));
                    builder.Append(" - ");
                    if (type == StationSystemType.Battery &&
                        (stat.Stat == StationObjectStat.Capacity ||
                         stat.Stat == StationObjectStat.BackupReserve))
                    {
                        EnergySystemController energy =
                            EnergySystemController.Instance;
                        bool isMainBattery =
                            stat.Stat == StationObjectStat.Capacity;
                        float currentCharge = isMainBattery
                            ? energy?.CurrentEnergy ?? value
                            : energy?.CurrentBackupReserve ?? value;
                        float maximumCharge = isMainBattery
                            ? energy?.TotalCapacity ?? value
                            : energy?.TotalBackupReserve ?? value;
                        string numberFormat = $"F{stat.Decimals}";
                        builder.Append(currentCharge.ToString(numberFormat));
                        builder.Append('/');
                        builder.Append(maximumCharge.ToString(numberFormat));
                        if (!string.IsNullOrEmpty(stat.Unit))
                        {
                            builder.Append(' ');
                            builder.Append(stat.Unit);
                        }
                        builder.AppendLine();
                        continue;
                    }
                    builder.AppendLine(stat.Format(value));
                }
                if (type == StationSystemType.Battery)
                {
                    EnergySystemController energy =
                        EnergySystemController.Instance;
                    builder.Append(Localize(
                        "station.stat.currentconsumption",
                        "Current Consumption"));
                    builder.Append(" - ");
                    builder.Append(
                        (energy?.CurrentConsumption ?? 0f).ToString("F1"));
                    builder.AppendLine(" kW");
                }
                builder.Append(Localize(
                    "station.status.installed_parts",
                    "Installed parts"));
                builder.Append(" - ");
                builder.Append(systems?.GetInstalledPartCount(
                    type,
                    selectedObjectId) ?? 0);
                builder.Append('/');
                builder.Append(definition.Slots.Count);
            }
            TerminalUIUtility.SetText(statusText, builder.ToString());
        }

        private void BindDataEvents()
        {
            MaintainableObject.AnyConditionChanged -=
                HandleMaintainableConditionChanged;
            MaintainableObject.AnyConditionChanged +=
                HandleMaintainableConditionChanged;

            StationSystemsController currentSystems =
                StationSystemsController.Instance;
            if (subscribedSystems != currentSystems)
            {
                if (subscribedSystems != null)
                    subscribedSystems.SystemsChanged -= HandleDataChanged;
                subscribedSystems = currentSystems;
                if (subscribedSystems != null)
                    subscribedSystems.SystemsChanged += HandleDataChanged;
            }

            EnergySystemController currentEnergy = EnergySystemController.Instance;
            if (subscribedEnergy != currentEnergy)
            {
                if (subscribedEnergy != null)
                    subscribedEnergy.EnergyChanged -= HandleDataChanged;
                subscribedEnergy = currentEnergy;
                if (subscribedEnergy != null)
                    subscribedEnergy.EnergyChanged += HandleDataChanged;
            }

            DroneScanController drone = DroneScanController.Instance;
            if (subscribedDrone != drone)
            {
                if (subscribedDrone != null)
                {
                    subscribedDrone.StateChanged -= HandleDroneStateChanged;
                    subscribedDrone.StationPresenceChanged -=
                        HandleDronePresenceChanged;
                }
                subscribedDrone = drone;
                if (subscribedDrone != null)
                {
                    subscribedDrone.StateChanged += HandleDroneStateChanged;
                    subscribedDrone.StationPresenceChanged +=
                        HandleDronePresenceChanged;
                }
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

        private void HandleDataChanged()
        {
            RefreshIfVisible();
        }

        private void HandleDroneStateChanged(DroneState _)
        {
            RefreshIfVisible();
        }

        private void HandleDronePresenceChanged(bool _)
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

        private void HandleMaintainableConditionChanged(string _, float __)
        {
            RefreshIfVisible();
        }

        private void RefreshIfVisible()
        {
            if (terminal?.IsOpen == true && gameObject.activeInHierarchy)
                dataRefreshPending = true;
        }

        private void UnbindDataEvents()
        {
            MaintainableObject.AnyConditionChanged -=
                HandleMaintainableConditionChanged;

            if (subscribedSystems != null)
            {
                subscribedSystems.SystemsChanged -= HandleDataChanged;
                subscribedSystems = null;
            }
            if (subscribedEnergy != null)
            {
                subscribedEnergy.EnergyChanged -= HandleDataChanged;
                subscribedEnergy = null;
            }
            if (subscribedDrone != null)
            {
                subscribedDrone.StateChanged -= HandleDroneStateChanged;
                subscribedDrone.StationPresenceChanged -=
                    HandleDronePresenceChanged;
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

        private static string FormatObjectName(string objectName)
        {
            return objectName
                .Replace("SM_", string.Empty)
                .Replace("_", " ")
                .ToUpperInvariant();
        }

        private static string Localize(
            string key,
            string fallback,
            params object[] arguments)
        {
            return NERALocalization.Get(
                NERALocalization.TerminalTable,
                key,
                fallback,
                arguments);
        }

        private void OnDestroy()
        {
            NERALocalization.LocaleChanged -= RefreshIfVisible;
            UnbindDataEvents();
        }
    }
}
