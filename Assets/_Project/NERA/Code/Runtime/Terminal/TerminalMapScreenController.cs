using System;
using NERA.Antenna;
using NERA.Drone;
using NERA.Expeditions;
using NERA.Locations;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Terminal
{
    public sealed class TerminalMapScreenController : MonoBehaviour
    {
        private TerminalUIScreen terminal;
        private Button droneTabButton;
        private Button antennaTabButton;
        private Button launchButton;
        private Button calibrationButton;
        private Button moveYesButton;
        private Button moveNoButton;
        private GameObject droneScreen;
        private GameObject antennaScreen;
        private GameObject moveConfirmation;
        private TMP_Text droneDescription;
        private TMP_Text droneProgress;
        private TMP_Text antennaDescription;
        private TMP_Text antennaProgress;
        private TMP_Text moveText;
        private RawImage mapImage;
        private Camera mapCamera;
        private Transform mapModelRoot;
        private GameObject signalMarker;
        private ExpeditionLocationData selectedLocation;
        private float nextRefreshAt;
        private bool initialized;

        public ExpeditionLocationData SelectedLocation => selectedLocation;

        public void Initialize(TerminalUIScreen owner)
        {
            terminal = owner;
            if (initialized)
                return;

            initialized = true;
            CacheHierarchy();
            BindButtons();
            ConfigurePreviewPicking();
            ShowDroneScreen();
            if (moveConfirmation != null)
                moveConfirmation.SetActive(false);
            RefreshAll();
        }

        public void SetScreenActive(bool active)
        {
            if (!active)
                return;

            RefreshAll();
        }

        private void Update()
        {
            if (!gameObject.activeInHierarchy ||
                Time.unscaledTime < nextRefreshAt)
            {
                return;
            }

            nextRefreshAt = Time.unscaledTime + 0.15f;
            RefreshAll();
        }

        private void CacheHierarchy()
        {
            droneTabButton = TerminalUIUtility.FindComponent<Button>(
                transform, "DronMapButton");
            antennaTabButton = TerminalUIUtility.FindComponent<Button>(
                transform, "AntennaMapButton");
            launchButton = TerminalUIUtility.FindComponent<Button>(
                transform, "LauncheButton");
            calibrationButton = TerminalUIUtility.FindComponent<Button>(
                transform, "CalibrationButton");
            moveYesButton = TerminalUIUtility.FindComponent<Button>(
                transform, "MoveYesButton");
            moveNoButton = TerminalUIUtility.FindComponent<Button>(
                transform, "MoveNoButton");

            droneScreen = TerminalUIUtility.Find(
                transform, "DronScreen")?.gameObject;
            antennaScreen = TerminalUIUtility.Find(
                transform, "AntennaScreen")?.gameObject;
            moveConfirmation = TerminalUIUtility.Find(
                transform, "background_info_Move")?.gameObject;
            moveText = TerminalUIUtility.FindComponent<TMP_Text>(
                moveConfirmation != null
                    ? moveConfirmation.transform
                    : transform,
                "Text_Move");

            if (droneScreen != null)
            {
                droneDescription = TerminalUIUtility.FindComponent<TMP_Text>(
                    droneScreen.transform, "description_update");
                droneProgress = TerminalUIUtility.FindComponent<TMP_Text>(
                    droneScreen.transform, "info_update");
            }

            if (antennaScreen != null)
            {
                antennaDescription = TerminalUIUtility.FindComponent<TMP_Text>(
                    antennaScreen.transform, "description_update");
                antennaProgress = TerminalUIUtility.FindComponent<TMP_Text>(
                    antennaScreen.transform, "info_progresa");
            }

            mapImage = TerminalUIUtility.FindComponent<RawImage>(
                transform, "Map_RawImage");
            mapCamera = TerminalUIUtility.FindComponent<Camera>(
                transform, "MapUICamera");
            mapModelRoot = TerminalUIUtility.Find(
                transform, "SM_UI_3D");
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
                selectedLocation = ResolveLocationForMarker(target.name);
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

        private ExpeditionLocationData ResolveLocationForMarker(string markerName)
        {
            if (string.IsNullOrWhiteSpace(markerName) ||
                markerName == "SN_Station")
            {
                return null;
            }

            const string prefix = "SM_Expedition_";
            if (!markerName.StartsWith(prefix, StringComparison.Ordinal) ||
                !int.TryParse(markerName.Substring(prefix.Length), out int authored))
            {
                return null;
            }

            int sectorIndex = authored - 1;
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            if (discovery == null)
                return null;

            foreach (ExpeditionLocationData location in discovery.KnownLocations)
            {
                if (location != null &&
                    location.MapSectorIndex == sectorIndex &&
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
                $"Travel to {location.DisplayName}?");
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
                : "Select a sector on the 3D map.";
            TerminalUIUtility.SetText(
                droneDescription,
                $"DRONE TARGET\n{selected}");

            string progress;
            if (drone == null)
                progress = "DRONE UNAVAILABLE";
            else if (drone.State == DroneState.Scanning)
            {
                progress =
                    $"SCANNING {Mathf.RoundToInt(drone.ScanProgress * 100f)}%";
            }
            else if (drone.IsCharging)
            {
                progress =
                    $"RECHARGING {Mathf.CeilToInt(drone.RechargeRemaining)}s";
            }
            else
                progress = drone.State.ToString().ToUpperInvariant();

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
                ? $"SIGNAL FOUND\n{antenna.ActiveSignal.DisplayName}"
                : "ANTENNA\nCalibrate to reveal a hidden signal on an opened sector.";
            TerminalUIUtility.SetText(antennaDescription, description);

            string progress;
            if (antenna == null)
                progress = "ANTENNA UNAVAILABLE";
            else if (antenna.State == AntennaState.Calibrating)
            {
                progress =
                    $"CALIBRATING {Mathf.RoundToInt(antenna.CalibrationProgress * 100f)}%";
            }
            else
                progress = antenna.State.ToString().ToUpperInvariant();
            TerminalUIUtility.SetText(antennaProgress, progress);
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

            int sector = antenna.ActiveSignalSectorIndex;
            Transform parent = TerminalUIUtility.Find(
                mapModelRoot,
                $"SM_Expedition_{sector + 1:00}");
            if (parent == null)
                return;

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
    }
}
