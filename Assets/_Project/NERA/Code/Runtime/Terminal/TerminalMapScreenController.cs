using System;
using NERA.Antenna;
using NERA.Drone;
using NERA.Expeditions;
using NERA.Localization;
using NERA.Locations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Terminal
{
    public sealed class TerminalMapScreenController : MonoBehaviour
    {
        private TerminalUIScreen terminal;
        [SerializeField] private Button droneTabButton;
        [SerializeField] private Button antennaTabButton;
        [SerializeField] private Button launchButton;
        [SerializeField] private Button calibrationButton;
        [SerializeField] private Button moveYesButton;
        [SerializeField] private Button moveNoButton;
        [SerializeField] private GameObject droneScreen;
        [SerializeField] private GameObject antennaScreen;
        [SerializeField] private GameObject moveConfirmation;
        [SerializeField] private TMP_Text droneDescription;
        [SerializeField] private TMP_Text droneProgress;
        [SerializeField] private TMP_Text antennaDescription;
        [SerializeField] private TMP_Text antennaProgress;
        [SerializeField] private TMP_Text moveText;
        [SerializeField] private RawImage mapImage;
        [SerializeField] private Camera mapCamera;
        [SerializeField] private Transform mapModelRoot;
        [SerializeField] private MapLocationSlotRegistry mapSlotRegistry;
        private GameObject signalMarker;
        private ExpeditionLocationData selectedLocation;
        private DroneScanController subscribedDrone;
        private AntennaController subscribedAntenna;
        private ExpeditionDiscoveryController subscribedDiscovery;
        private TerminalPreviewRenderer previewRenderer;
        private bool initialized;

        public ExpeditionLocationData SelectedLocation => selectedLocation;

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
            ShowDroneScreen();
            if (moveConfirmation != null)
                moveConfirmation.SetActive(false);
            RefreshAll();
            SetScreenActive(false);
        }

        public void SetScreenActive(bool active)
        {
            bool shouldRender =
                active &&
                terminal != null &&
                terminal.IsOpen;
            if (!shouldRender)
            {
                previewRenderer?.SetPreviewActive(false);
                UnbindDataEvents();
                TerminalUIUtility.ReleaseCameraTarget(mapCamera);
                return;
            }

            BindDataEvents();
            RefreshAll();
            previewRenderer?.SetPreviewActive(true);
        }

        private void CacheHierarchy()
        {
            droneTabButton ??= TerminalUIUtility.FindComponent<Button>(
                transform, "DronMapButton");
            antennaTabButton ??= TerminalUIUtility.FindComponent<Button>(
                transform, "AntennaMapButton");
            launchButton ??= TerminalUIUtility.FindComponent<Button>(
                transform, "LauncheButton");
            calibrationButton ??= TerminalUIUtility.FindComponent<Button>(
                transform, "CalibrationButton");
            moveYesButton ??= TerminalUIUtility.FindComponent<Button>(
                transform, "MoveYesButton");
            moveNoButton ??= TerminalUIUtility.FindComponent<Button>(
                transform, "MoveNoButton");

            droneScreen ??= TerminalUIUtility.Find(
                transform, "DronScreen")?.gameObject;
            antennaScreen ??= TerminalUIUtility.Find(
                transform, "AntennaScreen")?.gameObject;
            moveConfirmation ??= TerminalUIUtility.Find(
                transform, "background_info_Move")?.gameObject;
            moveText ??= TerminalUIUtility.FindComponent<TMP_Text>(
                moveConfirmation != null
                    ? moveConfirmation.transform
                    : transform,
                "Text_Move");

            if (droneScreen != null)
            {
                droneDescription ??= TerminalUIUtility.FindComponent<TMP_Text>(
                    droneScreen.transform, "description_update");
                droneProgress ??= TerminalUIUtility.FindComponent<TMP_Text>(
                    droneScreen.transform, "info_update");
            }

            if (antennaScreen != null)
            {
                antennaDescription ??= TerminalUIUtility.FindComponent<TMP_Text>(
                    antennaScreen.transform, "description_update");
                antennaProgress ??= TerminalUIUtility.FindComponent<TMP_Text>(
                    antennaScreen.transform, "info_progresa");
            }

            mapImage ??= TerminalUIUtility.FindComponent<RawImage>(
                transform, "Map_RawImage");
            mapCamera ??= TerminalUIUtility.FindComponent<Camera>(
                transform, "MapUICamera");
            if (mapCamera != null)
            {
                previewRenderer =
                    mapCamera.GetComponent<TerminalPreviewRenderer>() ??
                    mapCamera.gameObject.AddComponent<TerminalPreviewRenderer>();
                previewRenderer.Initialize(mapCamera);
            }
            mapModelRoot ??= TerminalUIUtility.Find(
                transform, "SM_UI_3D");
            if (mapSlotRegistry == null && mapModelRoot != null)
            {
                mapSlotRegistry =
                    mapModelRoot.GetComponent<MapLocationSlotRegistry>();
            }

            mapSlotRegistry?.Rebuild();
        }

        private void BindButtons()
        {
            droneTabButton?.onClick.AddListener(ShowDroneScreen);
            antennaTabButton?.onClick.AddListener(ShowAntennaScreen);
            launchButton?.onClick.AddListener(LaunchSelectedDroneScan);
            calibrationButton?.onClick.AddListener(StartAntennaCalibration);
            moveYesButton?.onClick.AddListener(ConfirmTravel);
            moveNoButton?.onClick.AddListener(HideTravelConfirmation);
        }

        private void ConfigurePreviewPicking()
        {
            if (mapImage == null)
                return;

            UIPreviewRaycaster picker =
                mapImage.GetComponent<UIPreviewRaycaster>() ??
                mapImage.gameObject.AddComponent<UIPreviewRaycaster>();
            picker.Initialize(mapImage, mapCamera, HandlePreviewHit);
        }

        private void ShowDroneScreen()
        {
            if (droneScreen != null)
                droneScreen.SetActive(true);
            if (antennaScreen != null)
                antennaScreen.SetActive(false);
            RefreshAll();
        }

        private void ShowAntennaScreen()
        {
            if (droneScreen != null)
                droneScreen.SetActive(false);
            if (antennaScreen != null)
                antennaScreen.SetActive(true);
            RefreshAll();
        }

        private void HandlePreviewHit(RaycastHit hit)
        {
            Transform target = hit.collider != null
                ? hit.collider.transform
                : hit.transform;
            if (target == null)
                return;

            AntennaController antenna = AntennaController.Instance;
            if (signalMarker != null &&
                (target == signalMarker.transform ||
                 target.IsChildOf(signalMarker.transform)))
            {
                selectedLocation = antenna?.ActiveSignal;
            }
            else
            {
                selectedLocation = ResolveLocationForMarker(target);
            }

            if (selectedLocation == null)
            {
                HideTravelConfirmation();
                RefreshAll();
                return;
            }

            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            bool canTravel =
                (discovery != null && discovery.IsDiscovered(selectedLocation)) ||
                (antenna != null && antenna.ActiveSignal == selectedLocation);
            if (canTravel)
                ShowTravelConfirmation(selectedLocation);
            else
                HideTravelConfirmation();

            RefreshAll();
        }

        private ExpeditionLocationData ResolveLocationForMarker(Transform target)
        {
            if (mapSlotRegistry == null ||
                !mapSlotRegistry.TryGetSlot(
                    target,
                    out MapLocationSlot authoredSlot))
                return null;

            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            if (discovery == null)
                return null;

            foreach (ExpeditionLocationData location in discovery.KnownLocations)
            {
                if (location != null &&
                    location.MapSlot == authoredSlot.Slot &&
                    location.DiscoverySource != DiscoverySource.Antenna)
                {
                    return location;
                }
            }

            return null;
        }

        private void LaunchSelectedDroneScan()
        {
            DroneScanController drone = DroneScanController.Instance;
            if (drone != null &&
                selectedLocation != null &&
                drone.LaunchScan(selectedLocation))
            {
                RefreshAll();
            }
        }

        private void StartAntennaCalibration()
        {
            if (AntennaController.Instance?.StartCalibration() == true)
                RefreshAll();
        }

        private void ShowTravelConfirmation(ExpeditionLocationData location)
        {
            if (moveConfirmation == null)
                return;

            moveConfirmation.SetActive(true);
            TerminalUIUtility.SetText(
                moveText,
                NERALocalization.Get(
                    NERALocalization.TerminalTable,
                    "map.travel_confirmation",
                    "Travel to {0}?",
                    location.DisplayName));
        }

        private void HideTravelConfirmation()
        {
            if (moveConfirmation != null)
                moveConfirmation.SetActive(false);
        }

        private void ConfirmTravel()
        {
            if (terminal != null && terminal.TravelTo(selectedLocation))
                HideTravelConfirmation();
        }

        private void RefreshAll()
        {
            RefreshDrone();
            RefreshAntenna();
            RefreshSignalMarker();
            previewRenderer?.RequestRender();
        }

        private void BindDataEvents()
        {
            DroneScanController drone = DroneScanController.Instance;
            if (subscribedDrone != drone)
            {
                UnbindDroneEvents();
                subscribedDrone = drone;
                if (subscribedDrone != null)
                {
                    subscribedDrone.StateChanged += HandleDroneStateChanged;
                    subscribedDrone.ScanProgressChanged += HandleDroneProgressChanged;
                    subscribedDrone.RechargeProgressChanged += HandleDroneProgressChanged;
                    subscribedDrone.BatteryChargeChanged += HandleDroneProgressChanged;
                    subscribedDrone.ScanCompleted += HandleDroneScanCompleted;
                }
            }

            AntennaController antenna = AntennaController.Instance;
            if (subscribedAntenna != antenna)
            {
                UnbindAntennaEvents();
                subscribedAntenna = antenna;
                if (subscribedAntenna != null)
                {
                    subscribedAntenna.StateChanged += HandleAntennaStateChanged;
                    subscribedAntenna.CalibrationProgressChanged +=
                        HandleAntennaProgressChanged;
                    subscribedAntenna.ConditionChanged += HandleAntennaProgressChanged;
                    subscribedAntenna.ActiveSignalChanged +=
                        HandleActiveSignalChanged;
                }
            }

            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            if (subscribedDiscovery != discovery)
            {
                if (subscribedDiscovery != null)
                {
                    subscribedDiscovery.LocationDiscovered -=
                        HandleLocationDiscovered;
                }

                subscribedDiscovery = discovery;
                if (subscribedDiscovery != null)
                {
                    subscribedDiscovery.LocationDiscovered +=
                        HandleLocationDiscovered;
                }
            }
        }

        private void UnbindDataEvents()
        {
            UnbindDroneEvents();
            UnbindAntennaEvents();
            if (subscribedDiscovery != null)
            {
                subscribedDiscovery.LocationDiscovered -=
                    HandleLocationDiscovered;
                subscribedDiscovery = null;
            }
        }

        private void UnbindDroneEvents()
        {
            if (subscribedDrone == null)
                return;

            subscribedDrone.StateChanged -= HandleDroneStateChanged;
            subscribedDrone.ScanProgressChanged -= HandleDroneProgressChanged;
            subscribedDrone.RechargeProgressChanged -= HandleDroneProgressChanged;
            subscribedDrone.BatteryChargeChanged -= HandleDroneProgressChanged;
            subscribedDrone.ScanCompleted -= HandleDroneScanCompleted;
            subscribedDrone = null;
        }

        private void UnbindAntennaEvents()
        {
            if (subscribedAntenna == null)
                return;

            subscribedAntenna.StateChanged -= HandleAntennaStateChanged;
            subscribedAntenna.CalibrationProgressChanged -=
                HandleAntennaProgressChanged;
            subscribedAntenna.ConditionChanged -= HandleAntennaProgressChanged;
            subscribedAntenna.ActiveSignalChanged -= HandleActiveSignalChanged;
            subscribedAntenna = null;
        }

        private void HandleDroneStateChanged(DroneState _) => RefreshIfVisible();

        private void HandleDroneProgressChanged(float _) => RefreshIfVisible();

        private void HandleDroneScanCompleted(DroneScanResult _) =>
            RefreshIfVisible();

        private void HandleAntennaStateChanged(AntennaState _) =>
            RefreshIfVisible();

        private void HandleAntennaProgressChanged(float _) => RefreshIfVisible();

        private void HandleActiveSignalChanged(ExpeditionLocationData _) =>
            RefreshIfVisible();

        private void HandleLocationDiscovered(string _) => RefreshIfVisible();

        private void RefreshIfVisible()
        {
            if (terminal?.IsOpen == true && gameObject.activeInHierarchy)
                RefreshAll();
        }

        private void RefreshDrone()
        {
            DroneScanController drone = DroneScanController.Instance;
            bool hasSelection = selectedLocation != null;
            bool canLaunch = drone != null &&
                hasSelection &&
                drone.CanLaunchScan(selectedLocation);
            if (launchButton != null)
                launchButton.interactable = canLaunch;

            string selected = hasSelection
                ? selectedLocation.DisplayName
                : NERALocalization.Get(
                    NERALocalization.TerminalTable,
                    "map.select_sector",
                    "Select a sector on the 3D map.");
            TerminalUIUtility.SetText(
                droneDescription,
                NERALocalization.Get(
                    NERALocalization.TerminalTable,
                    "map.drone_target",
                    "DRONE TARGET\n{0}",
                    selected));

            string progress;
            if (drone == null)
                progress = NERALocalization.Get(
                    NERALocalization.TerminalTable,
                    "map.drone_unavailable",
                    "DRONE UNAVAILABLE");
            else if (drone.State == DroneState.Scanning)
            {
                progress = NERALocalization.Get(
                    NERALocalization.TerminalTable,
                    "map.scanning",
                    "SCANNING {0}%",
                    Mathf.RoundToInt(drone.ScanProgress * 100f));
            }
            else if (drone.IsCharging)
            {
                progress = NERALocalization.Get(
                    NERALocalization.TerminalTable,
                    "map.recharging",
                    "RECHARGING {0}s",
                    Mathf.CeilToInt(drone.RechargeRemaining));
            }
            else
                progress = LocalizeState(drone.State.ToString());

            TerminalUIUtility.SetText(droneProgress, progress);
        }

        private void RefreshAntenna()
        {
            AntennaController antenna = AntennaController.Instance;
            if (calibrationButton != null)
            {
                calibrationButton.interactable =
                    antenna != null && antenna.CanStartCalibration;
            }

            string description = antenna?.ActiveSignal != null
                ? NERALocalization.Get(
                    NERALocalization.TerminalTable,
                    "map.signal_found",
                    "SIGNAL FOUND\n{0}",
                    antenna.ActiveSignal.DisplayName)
                : NERALocalization.Get(
                    NERALocalization.TerminalTable,
                    "map.antenna_hint",
                    "ANTENNA\nCalibrate to reveal a hidden signal on an opened sector.");
            TerminalUIUtility.SetText(antennaDescription, description);

            string progress;
            if (antenna == null)
                progress = NERALocalization.Get(
                    NERALocalization.TerminalTable,
                    "map.antenna_unavailable",
                    "ANTENNA UNAVAILABLE");
            else if (antenna.State == AntennaState.Calibrating)
            {
                progress = NERALocalization.Get(
                    NERALocalization.TerminalTable,
                    "map.calibrating",
                    "CALIBRATING {0}%",
                    Mathf.RoundToInt(antenna.CalibrationProgress * 100f));
            }
            else
                progress = LocalizeState(antenna.State.ToString());
            TerminalUIUtility.SetText(antennaProgress, progress);
        }

        private static string LocalizeState(string state)
        {
            return NERALocalization.Get(
                NERALocalization.TerminalTable,
                "map.state." + NERALocalization.NormalizeKeyPart(state),
                state?.ToUpperInvariant() ?? string.Empty);
        }

        private void RefreshSignalMarker()
        {
            AntennaController antenna = AntennaController.Instance;
            ExpeditionLocationData signal = antenna?.ActiveSignal;
            if (signal == null || mapModelRoot == null)
            {
                if (signalMarker != null)
                    signalMarker.SetActive(false);
                return;
            }

            if (mapSlotRegistry == null ||
                !mapSlotRegistry.TryGetSlot(
                    antenna.ActiveSignalMapSlot,
                    out MapLocationSlot authoredSlot))
            {
                if (signalMarker != null)
                    signalMarker.SetActive(false);
                return;
            }

            Transform parent = authoredSlot.SignalAnchor;

            if (signalMarker == null)
            {
                signalMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                signalMarker.name = "Runtime_AntennaSignal";
                signalMarker.layer = parent.gameObject.layer;
                MeshRenderer renderer = signalMarker.GetComponent<MeshRenderer>();
                MeshRenderer parentRenderer = parent.GetComponent<MeshRenderer>();
                if (renderer != null && parentRenderer != null)
                    renderer.sharedMaterial = parentRenderer.sharedMaterial;
            }

            signalMarker.transform.SetParent(parent, false);
            signalMarker.transform.localPosition = Vector3.up * 0.65f;
            signalMarker.transform.localRotation = Quaternion.identity;
            signalMarker.transform.localScale = Vector3.one * 0.35f;
            signalMarker.SetActive(true);
        }

        private void OnDestroy()
        {
            NERALocalization.LocaleChanged -= RefreshIfVisible;
            previewRenderer?.SetPreviewActive(false);
            UnbindDataEvents();
        }
    }
}
