using NERA.Interaction;
using NERA.Core;
using NERA.Drone;
using NERA.Expeditions;
using NERA.Library;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NERA.Terminal
{
    public sealed class TerminalUIScreen : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button statusTabButton;
        [SerializeField] private Button mapTabButton;
        [SerializeField] private Button libraryTabButton;
        [SerializeField] private Button travelButton;
        [SerializeField] private GameObject statusPanel;
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private GameObject libraryPanel;
        [SerializeField] private Text statusText;
        [SerializeField] private Text mapText;
        [SerializeField] private Text locationListText;
        [SerializeField] private Image mapPreview;
        [SerializeField] private Button[] mapSectorButtons = new Button[9];
        [SerializeField] private Text libraryText;
        [SerializeField] private Image libraryIllustration;

        [Header("First Playable Location")]
        [SerializeField] private ExpeditionLocationData droneDiscoveryLocation;

        [Header("Library Content")]
        [SerializeField] private List<LibraryEntryData> libraryEntries =
            new List<LibraryEntryData>();

        private ExpeditionLocationData selectedLocation;
        private int selectedSectorIndex = BaseSectorIndex;
        private const int BaseSectorIndex = 4;
        private static readonly Color UnknownSectorColor =
            new Color(0.18f, 0.22f, 0.24f, 1f);
        private static readonly Color DiscoveredSectorColor =
            new Color(0.32f, 0.8f, 0.86f, 1f);
        private static readonly Color BaseSectorColor =
            new Color(0.08f, 0.5f, 0.56f, 1f);

        public static TerminalUIScreen Instance { get; private set; }
        public bool IsOpen { get; private set; }

        private PlayerController playerController;
        private PlayerInteractionController interactionController;
        private PlayerFollowCamera followCamera;
        private ExpeditionDiscoveryController subscribedDiscovery;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (exitButton != null)
                exitButton.onClick.AddListener(Close);

            if (statusTabButton != null)
                statusTabButton.onClick.AddListener(ShowStatusSection);

            if (mapTabButton != null)
                mapTabButton.onClick.AddListener(ShowMapSection);

            if (libraryTabButton != null)
                libraryTabButton.onClick.AddListener(ShowLibrarySection);

            if (travelButton != null)
                travelButton.onClick.AddListener(HandleMapAction);

            for (int i = 0; i < mapSectorButtons.Length; i++)
            {
                int sectorIndex = i;

                if (mapSectorButtons[i] != null)
                    mapSectorButtons[i].onClick.AddListener(
                        () => SelectMapSector(sectorIndex)
                    );
            }

            SetVisible(false);
        }

        private void Start()
        {
            SubscribeToDiscovery();
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                Close();

            if (IsOpen && mapPanel != null && mapPanel.activeSelf)
                RefreshSelectedMapSector();

            if (IsOpen && statusPanel != null && statusPanel.activeSelf)
                RefreshStatusSection();
        }

        public void Open()
        {
            if (IsOpen)
                return;

            CachePlayerControllers();
            IsOpen = true;

            if (playerController != null)
                playerController.SetInputEnabled(false);

            if (interactionController != null)
                interactionController.enabled = false;

            if (followCamera != null)
                followCamera.SetInputEnabled(false);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetVisible(true);
            ShowStatusSection();
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            SetVisible(false);

            if (playerController != null)
                playerController.SetInputEnabled(true);

            if (interactionController != null)
                interactionController.enabled = true;

            if (followCamera != null)
                followCamera.SetInputEnabled(true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void ShowStatusSection()
        {
            ShowOnly(statusPanel);
            RefreshStatusSection();
        }

        private void RefreshStatusSection()
        {
            Station.StationPowerController power = Station.StationPowerController.Instance;
            string powerState = power != null && power.IsPowered ? "ONLINE" : "OFFLINE";
            DroneScanController drone = DroneScanController.Instance;
            string droneState = drone != null ? drone.State.ToString().ToUpperInvariant() : "UNAVAILABLE";

            if (drone != null && drone.State == DroneState.Scanning)
                droneState += $" {Mathf.RoundToInt(drone.ScanProgress * 100f)}%";

            SetText(
                statusText,
                $"STATION STATUS\n\nPOWER GRID        {powerState}\n" +
                "TERMINAL          OPERATIONAL\n" +
                $"DRONE UPLINK      {droneState}\n" +
                "EXPEDITION LINK   STANDBY"
            );
        }

        private void ShowMapSection()
        {
            ShowOnly(mapPanel);
            RefreshMapSectors();
            SelectMapSector(BaseSectorIndex);
        }

        private void RefreshMapSectors()
        {
            ExpeditionDiscoveryController discovery = ExpeditionDiscoveryController.Instance;
            List<ExpeditionLocationData> knownLocations = discovery != null
                ? discovery.KnownLocations
                : new List<ExpeditionLocationData>();

            for (int i = 0; i < mapSectorButtons.Length; i++)
            {
                Button button = mapSectorButtons[i];

                if (button == null)
                    continue;

                Image image = button.image;
                image.sprite = null;
                image.color = i == BaseSectorIndex
                    ? BaseSectorColor
                    : UnknownSectorColor;

                ExpeditionLocationData location = FindLocationAtSector(knownLocations, i);

                if (location != null && discovery.IsDiscovered(location))
                {
                    image.sprite = location.MapPreview;
                    image.preserveAspect = true;
                    image.color = location.MapPreview != null
                        ? Color.white
                        : DiscoveredSectorColor;
                }
            }
        }

        private void SelectMapSector(int sectorIndex)
        {
            selectedSectorIndex = sectorIndex;
            selectedLocation = null;
            RefreshSelectedMapSector();
        }

        private void RefreshSelectedMapSector()
        {
            SetMapAction(false, string.Empty, false);

            if (selectedSectorIndex == BaseSectorIndex)
            {
                SetText(
                    mapText,
                    "NERA STATION\n\nYour home base and expedition hub.\n" +
                    "Drone control, storage, research and preparation systems are located here."
                );
                return;
            }

            ExpeditionDiscoveryController discovery = ExpeditionDiscoveryController.Instance;
            List<ExpeditionLocationData> knownLocations = discovery != null
                ? discovery.KnownLocations
                : new List<ExpeditionLocationData>();
            ExpeditionLocationData location = FindLocationAtSector(
                knownLocations,
                selectedSectorIndex
            );

            if (location == null)
            {
                SetText(
                    mapText,
                    "UNEXPLORED SECTOR\n\nNo survey target is currently available in this area."
                );
                return;
            }

            selectedLocation = location;
            bool discovered = discovery != null && discovery.IsDiscovered(location);

            if (discovered)
            {
                SetText(mapText, $"{location.DisplayName}\n\n{location.Description}");
                bool canTravel = Application.CanStreamedLevelBeLoaded(location.SceneName);
                SetMapAction(
                    true,
                    canTravel ? "TRAVEL" : "TRAVEL UNAVAILABLE",
                    canTravel
                );
                return;
            }

            DroneScanController drone = DroneScanController.Instance;

            if (location.DiscoverySource != Locations.DiscoverySource.Drone)
            {
                SetText(
                    mapText,
                    "UNKNOWN SIGNAL\n\nThis area cannot be surveyed by drone.\nAntenna analysis is required."
                );
                return;
            }

            if (drone != null &&
                drone.State == DroneState.Scanning &&
                drone.ScanLocation == location)
            {
                int percent = Mathf.RoundToInt(drone.ScanProgress * 100f);
                SetText(
                    mapText,
                    $"DRONE SURVEY IN PROGRESS\n\nSector scan: {percent}%"
                );
                return;
            }

            SetText(
                mapText,
                "UNEXPLORED SECTOR\n\nNo survey data available.\nLaunch the drone to investigate this area."
            );
            SetMapAction(
                true,
                "LAUNCH DRONE",
                drone != null && drone.CanLaunchScan(location)
            );
        }

        private static ExpeditionLocationData FindLocationAtSector(
            List<ExpeditionLocationData> locations,
            int sectorIndex
        )
        {
            foreach (ExpeditionLocationData location in locations)
            {
                if (location != null && location.MapSectorIndex == sectorIndex)
                    return location;
            }

            return null;
        }

        private void ShowLibrarySection()
        {
            ShowOnly(libraryPanel);

            if (libraryEntries.Count == 0)
            {
                SetText(libraryText, "LIBRARY\n\nNo entries available.");
                SetLibraryIllustration(null);
                return;
            }

            LibraryEntryData entry = libraryEntries[0];
            SetText(libraryText, $"{entry.Title}\n\n{entry.Body}");
            SetLibraryIllustration(entry.Illustration);
        }

        private void HandleMapAction()
        {
            ExpeditionDiscoveryController discovery = ExpeditionDiscoveryController.Instance;

            if (selectedLocation == null || discovery == null)
                return;

            if (discovery.IsDiscovered(selectedLocation))
                TravelToSelectedLocation();
            else
                LaunchDroneForSelectedLocation();
        }

        private void LaunchDroneForSelectedLocation()
        {
            DroneScanController drone = DroneScanController.Instance;

            if (drone != null && drone.LaunchScan(selectedLocation))
                RefreshSelectedMapSector();
        }

        private void TravelToSelectedLocation()
        {
            if (selectedLocation == null ||
                !IsLocationDiscovered(selectedLocation) ||
                !Application.CanStreamedLevelBeLoaded(selectedLocation.SceneName))
            {
                SetText(mapText, "Travel unavailable.");
                return;
            }

            SceneTransitionState.SetPendingSpawnPoint(selectedLocation.SpawnPointId);
            Close();
            SceneManager.LoadScene(selectedLocation.SceneName);
        }

        private bool IsLocationDiscovered()
        {
            ExpeditionDiscoveryController discovery = ExpeditionDiscoveryController.Instance;
            return discovery != null && discovery.IsDiscovered(droneDiscoveryLocation);
        }

        private bool IsLocationDiscovered(ExpeditionLocationData location)
        {
            ExpeditionDiscoveryController discovery = ExpeditionDiscoveryController.Instance;
            return discovery != null && discovery.IsDiscovered(location);
        }

        private void ShowOnly(GameObject activePanel)
        {
            SetPanelActive(statusPanel, activePanel == statusPanel);
            SetPanelActive(mapPanel, activePanel == mapPanel);
            SetPanelActive(libraryPanel, activePanel == libraryPanel);
        }

        private static void SetPanelActive(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }

        private static void SetText(Text target, string message)
        {
            if (target != null)
                target.text = message;
        }

        private static void SetButtonInteractable(Button button, bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }

        private void SetMapAction(bool visible, string label, bool interactable)
        {
            if (travelButton == null)
                return;

            travelButton.gameObject.SetActive(visible);
            travelButton.interactable = interactable;

            Text buttonLabel = travelButton.GetComponentInChildren<Text>(true);

            if (buttonLabel != null)
                buttonLabel.text = label;
        }

        private void SetLibraryIllustration(Sprite sprite)
        {
            if (libraryIllustration == null)
                return;

            libraryIllustration.sprite = sprite;
            libraryIllustration.preserveAspect = true;
            libraryIllustration.gameObject.SetActive(sprite != null);
        }

        private void CachePlayerControllers()
        {
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();

            if (interactionController == null)
                interactionController = FindFirstObjectByType<PlayerInteractionController>();

            if (followCamera == null)
                followCamera = FindFirstObjectByType<PlayerFollowCamera>();
        }

        private void SubscribeToDiscovery()
        {
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;

            if (discovery == null || discovery == subscribedDiscovery)
                return;

            if (subscribedDiscovery != null)
                subscribedDiscovery.LocationDiscovered -= HandleLocationDiscovered;

            subscribedDiscovery = discovery;
            subscribedDiscovery.LocationDiscovered += HandleLocationDiscovered;
        }

        private void HandleLocationDiscovered(string _)
        {
            if (!IsOpen || mapPanel == null || !mapPanel.activeSelf)
                return;

            RefreshMapSectors();
            RefreshSelectedMapSector();
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private void OnDestroy()
        {
            if (subscribedDiscovery != null)
                subscribedDiscovery.LocationDiscovered -= HandleLocationDiscovered;

            if (exitButton != null)
                exitButton.onClick.RemoveListener(Close);

            if (statusTabButton != null)
                statusTabButton.onClick.RemoveListener(ShowStatusSection);

            if (mapTabButton != null)
                mapTabButton.onClick.RemoveListener(ShowMapSection);

            if (libraryTabButton != null)
                libraryTabButton.onClick.RemoveListener(ShowLibrarySection);

            if (travelButton != null)
                travelButton.onClick.RemoveListener(HandleMapAction);

            for (int i = 0; i < mapSectorButtons.Length; i++)
            {
                if (mapSectorButtons[i] != null)
                    mapSectorButtons[i].onClick.RemoveAllListeners();
            }

            if (Instance == this)
                Instance = null;
        }
    }
}
