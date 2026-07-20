using NERA.Interaction;
using NERA.Antenna;
using NERA.Core;
using NERA.Drone;
using NERA.Energy;
using NERA.Expeditions;
using NERA.Library;
using NERA.Items;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NERA.Terminal
{
    public sealed class TerminalUIScreen : MonoBehaviour
    {
        private const string TerminalConsumerId = "central_terminal";
        [Header("View")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button exitButton;
        [SerializeField] private Button statusTabButton;
        [SerializeField] private Button mapTabButton;
        [SerializeField] private Button libraryTabButton;
        [SerializeField] private Button travelButton;
        [SerializeField] private Button antennaCalibrationButton;
        [SerializeField] private GameObject statusPanel;
        [SerializeField] private GameObject mapPanel;
        [SerializeField] private GameObject libraryPanel;
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private TMP_Text mapText;
        [SerializeField] private Button[] mapSectorButtons = new Button[9];
        [SerializeField] private TMP_Text libraryText;
        [SerializeField] private Image libraryIllustration;
        [SerializeField] private Button stationLibraryTabButton;
        [SerializeField] private Button anomalyLibraryTabButton;
        [SerializeField] private Button recordsLibraryTabButton;
        [SerializeField] private Transform libraryIconContent;
        [SerializeField] private Button libraryIconButtonPrefab;

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
        private AntennaController subscribedAntenna;
        private readonly List<Button> spawnedLibraryButtons = new List<Button>();
        private readonly List<Button> spawnedSignalButtons = new List<Button>();
        private LibraryCategory selectedLibraryCategory = LibraryCategory.Station;
        private LibraryDisplayItem selectedLibraryItem;

        private sealed class LibraryDisplayItem
        {
            public string Id;
            public string Title;
            public string Description;
            public Sprite Icon;
            public LibraryCategory Category;
        }

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

            CacheTextReferences();

            if (exitButton != null)
                exitButton.onClick.AddListener(Close);

            if (statusTabButton != null)
                statusTabButton.onClick.AddListener(ShowStatusSection);

            if (mapTabButton != null)
                mapTabButton.onClick.AddListener(ShowMapSection);

            if (libraryTabButton != null)
                libraryTabButton.onClick.AddListener(ShowLibrarySection);

            if (stationLibraryTabButton != null)
                stationLibraryTabButton.onClick.AddListener(
                    () => SelectLibraryCategory(LibraryCategory.Station)
                );

            if (anomalyLibraryTabButton != null)
                anomalyLibraryTabButton.onClick.AddListener(
                    () => SelectLibraryCategory(LibraryCategory.Anomaly)
                );

            if (recordsLibraryTabButton != null)
                recordsLibraryTabButton.onClick.AddListener(
                    () => SelectLibraryCategory(LibraryCategory.Records)
                );

            if (travelButton != null)
                travelButton.onClick.AddListener(HandleMapAction);

            if (antennaCalibrationButton != null)
                antennaCalibrationButton.onClick.AddListener(HandleAntennaCalibration);

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
            SubscribeToAntenna();

            EnergySystemController energy = EnergySystemController.Instance;
            if (energy != null)
            {
                energy.RegisterConsumer(
                    TerminalConsumerId,
                    energy.Config.TerminalConsumption,
                    false
                );
                energy.SetConsumerActive(TerminalConsumerId, false);
            }
        }

        private void Update()
        {
            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                Close();

            if (IsOpen && mapPanel != null && mapPanel.activeSelf)
            {
                RefreshSelectedMapSector();
                RefreshAntennaCalibrationButton();
            }

            if (IsOpen && statusPanel != null && statusPanel.activeSelf)
                RefreshStatusSection();

            if (IsOpen &&
                EnergySystemController.Instance != null &&
                !EnergySystemController.Instance.IsConsumerPowered(TerminalConsumerId))
            {
                Close();
            }
        }

        public void Open()
        {
            if (IsOpen)
                return;

            CachePlayerControllers();

            EnergySystemController energy = EnergySystemController.Instance;
            if (energy != null)
            {
                energy.RegisterConsumer(
                    TerminalConsumerId,
                    energy.Config.TerminalConsumption,
                    false
                );
                energy.SetConsumerActive(TerminalConsumerId, true);
                if (!energy.IsConsumerPowered(TerminalConsumerId))
                {
                    energy.SetConsumerActive(TerminalConsumerId, false);
                    return;
                }
            }

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
            EnergySystemController.Instance?.SetConsumerActive(
                TerminalConsumerId,
                false
            );
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
            EnergySystemController energy = EnergySystemController.Instance;
            DroneScanController drone = DroneScanController.Instance;
            string droneState = drone != null ? drone.State.ToString().ToUpperInvariant() : "UNAVAILABLE";

            if (drone != null && drone.State == DroneState.Scanning)
                droneState += $" {Mathf.RoundToInt(drone.ScanProgress * 100f)}%";
            else if (drone != null && drone.IsCharging)
                droneState =
                    $"RECHARGING {Mathf.CeilToInt(drone.RechargeRemaining)}S";

            SetText(
                statusText,
                $"STATION STATUS\n\nPOWER GRID        {powerState}\n" +
                (energy != null
                    ? $"ENERGY            {energy.CurrentEnergy:0} / {energy.TotalCapacity:0}\n" +
                      $"GENERATION        +{energy.CurrentGeneration:0.0} / SEC\n" +
                      $"CONSUMPTION       -{energy.CurrentConsumption:0.0} / SEC\n" +
                      $"GRID MODE         {energy.State.ToString().ToUpperInvariant()}\n"
                    : string.Empty) +
                "TERMINAL          OPERATIONAL\n" +
                $"DRONE UPLINK      {droneState}\n" +
                "EXPEDITION LINK   STANDBY"
            );
        }

        private void ShowMapSection()
        {
            ShowOnly(mapPanel);
            SubscribeToAntenna();
            CacheMapReferences();
            RefreshMapSectors();
            RefreshSignalButtons();
            RefreshAntennaCalibrationButton();
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

            RefreshSignalButtons();
        }

        private void SelectMapSector(int sectorIndex)
        {
            selectedSectorIndex = sectorIndex;
            selectedLocation = null;
            RefreshSelectedMapSector();
        }

        private void RefreshSelectedMapSector()
        {
            AntennaController antenna = AntennaController.Instance;
            if (selectedLocation != null &&
                antenna != null &&
                antenna.ActiveSignal == selectedLocation)
            {
                SelectAntennaSignal(selectedLocation);
                return;
            }

            if (selectedSectorIndex == BaseSectorIndex)
            {
                SetText(
                    mapText,
                    "NERA STATION\n\nYour home base and expedition hub.\n" +
                    "Drone control, storage, research and preparation systems are located here."
                );
                SetMapAction(false, "TRAVEL", false);
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
                SetMapAction(false, "TRAVEL", false);
                return;
            }

            selectedLocation = location;
            bool discovered = discovery != null && discovery.IsDiscovered(location);

            if (discovered)
            {
                SetText(mapText, $"{location.DisplayName}\n\n{location.Description}");
                bool canTravel = Application.CanStreamedLevelBeLoaded(location.SceneName);
                SetMapAction(true, "TRAVEL", canTravel);
                return;
            }

            DroneScanController drone = DroneScanController.Instance;

            if (drone != null && drone.IsCharging)
            {
                int seconds = Mathf.CeilToInt(drone.RechargeRemaining);
                SetText(
                    mapText,
                    $"DRONE RECHARGING\n\nThe drone is preparing for its next survey.\nReady in approximately {seconds} seconds."
                );
                SetMapAction(true, $"RECHARGING {seconds}S", false);
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
                SetMapAction(false, "LAUNCH DRONE", false);
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
                if (location != null &&
                    location.LocationType == Locations.LocationType.Expedition &&
                    location.MapSectorIndex == sectorIndex)
                    return location;
            }

            return null;
        }

        private void RefreshSignalButtons()
        {
            foreach (Button button in spawnedSignalButtons)
            {
                if (button != null)
                    Destroy(button.gameObject);
            }

            spawnedSignalButtons.Clear();

            AntennaController antenna = AntennaController.Instance;
            ExpeditionLocationData signal = antenna != null
                ? antenna.ActiveSignal
                : null;
            int sectorIndex = antenna != null
                ? antenna.ActiveSignalSectorIndex
                : -1;

            if (signal == null ||
                sectorIndex < 0 ||
                sectorIndex >= mapSectorButtons.Length)
            {
                return;
            }

            Button sectorButton = mapSectorButtons[sectorIndex];
            if (sectorButton == null)
                return;

            Button signalButton = CreateSignalButton(sectorButton.transform, signal);
            spawnedSignalButtons.Add(signalButton);
        }

        private Button CreateSignalButton(
            Transform parent,
            ExpeditionLocationData signal
        )
        {
            GameObject buttonObject = new GameObject(
                "AntennaSignalButton",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            buttonObject.transform.SetParent(parent, false);
            buttonObject.transform.SetAsLastSibling();

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(8f, -8f);
            rectTransform.sizeDelta = new Vector2(34f, 34f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.03f, 0.08f, 0.09f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => SelectAntennaSignal(signal));

            GameObject labelObject = new GameObject(
                "Label",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = "?";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 26f;
            label.color = Color.white;
            label.raycastTarget = false;
            return button;
        }

        private void SelectAntennaSignal(ExpeditionLocationData signal)
        {
            selectedLocation = signal;
            AntennaController antenna = AntennaController.Instance;
            selectedSectorIndex = antenna != null &&
                antenna.ActiveSignal == signal
                    ? antenna.ActiveSignalSectorIndex
                    : selectedSectorIndex;

            if (signal == null)
                return;

            SetText(mapText, $"{signal.DisplayName}\n\n{signal.Description}");
            bool canTravel = Application.CanStreamedLevelBeLoaded(signal.SceneName);
            SetMapAction(true, "TRAVEL", canTravel);
        }

        private void RefreshAntennaCalibrationButton()
        {
            Button button = antennaCalibrationButton;
            if (button == null)
                return;

            AntennaController antenna = AntennaController.Instance;
            bool calibrating = antenna != null &&
                antenna.State == AntennaState.Calibrating;
            bool faulted = antenna != null &&
                antenna.State == AntennaState.Faulted;
            button.gameObject.SetActive(true);
            button.interactable = antenna != null && antenna.CanStartCalibration;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null)
                return;

            if (calibrating)
                label.text =
                    $"CALIBRATING {Mathf.RoundToInt(antenna.CalibrationProgress * 100f)}%";
            else if (faulted)
                label.text = "ANTENNA FAULT";
            else if (antenna != null && antenna.ActiveSignal != null)
                label.text = "SIGNAL FOUND";
            else
                label.text = "CALIBRATE ANTENNA";
        }

        private void ShowLibrarySection()
        {
            ShowOnly(libraryPanel);
            CacheLibraryReferences();
            SelectLibraryCategory(selectedLibraryCategory);
        }

        private void SelectLibraryCategory(LibraryCategory category)
        {
            selectedLibraryCategory = category;
            RefreshLibrarySection();
        }

        private void RefreshLibrarySection()
        {
            List<LibraryEntryData> availableEntries =
                new List<LibraryEntryData>(libraryEntries);
            availableEntries.AddRange(Resources.LoadAll<LibraryEntryData>("Library"));

            LibraryController library = LibraryController.Instance;
            library?.RegisterRange(availableEntries);

            if (library != null)
                availableEntries.AddRange(library.Entries);

            availableEntries.RemoveAll(entry => entry == null);

            HashSet<string> entryIds = new HashSet<string>();
            availableEntries.RemoveAll(entry =>
                string.IsNullOrWhiteSpace(entry.EntryId) ||
                !entryIds.Add(entry.EntryId));

            List<LibraryDisplayItem> visibleItems = BuildLibraryDisplayItems(
                library,
                availableEntries,
                selectedLibraryCategory
            );
            RebuildLibraryIconList(visibleItems);

            LibraryDisplayItem item = null;

            if (selectedLibraryItem != null)
            {
                item = visibleItems.Find(
                    candidate => candidate.Id == selectedLibraryItem.Id
                );
            }

            if (item == null && library != null)
                item = visibleItems.Find(candidate => candidate.Id == library.LastUnlockedEntryId);

            if (item == null && visibleItems.Count > 0)
                item = visibleItems[0];

            SelectLibraryItem(item);
        }

        private List<LibraryDisplayItem> BuildLibraryDisplayItems(
            LibraryController library,
            List<LibraryEntryData> availableEntries,
            LibraryCategory category
        )
        {
            List<LibraryDisplayItem> items = new List<LibraryDisplayItem>();

            if (category == LibraryCategory.Station && library != null)
            {
                foreach (ItemData knownItem in library.GetKnownItems())
                {
                    if (knownItem == null)
                        continue;

                    items.Add(new LibraryDisplayItem
                    {
                        Id = knownItem.ItemId,
                        Title = knownItem.DisplayName,
                        Description = knownItem.Description,
                        Icon = knownItem.Icon,
                        Category = LibraryCategory.Station
                    });
                }
            }

            foreach (LibraryEntryData entry in availableEntries)
            {
                if (entry == null || entry.Category != category)
                    continue;

                if (library != null && !library.IsUnlocked(entry))
                    continue;

                items.Add(new LibraryDisplayItem
                {
                    Id = entry.EntryId,
                    Title = entry.Title,
                    Description = entry.Description,
                    Icon = entry.Illustration,
                    Category = entry.Category
                });
            }

            HashSet<string> itemIds = new HashSet<string>();
            items.RemoveAll(item =>
                string.IsNullOrWhiteSpace(item.Id) || !itemIds.Add(item.Id));
            return items;
        }

        private void SelectLibraryItem(LibraryDisplayItem item)
        {
            selectedLibraryItem = item;

            if (item == null)
            {
                SetText(libraryText, GetEmptyLibraryMessage(selectedLibraryCategory));
                SetLibraryIllustration(null);
                return;
            }

            SetText(
                libraryText,
                $"{item.Title}\n\n{item.Description}"
            );
            SetLibraryIllustration(item.Icon);
        }

        private static string GetEmptyLibraryMessage(LibraryCategory category)
        {
            switch (category)
            {
                case LibraryCategory.Anomaly:
                    return "ANOMALY\n\nNo analyzed anomaly data.";
                case LibraryCategory.Records:
                    return "RECORDS\n\nNo recovered records.";
                default:
                    return "STATION\n\nNo station items catalogued.";
            }
        }

        private void RebuildLibraryIconList(List<LibraryDisplayItem> items)
        {
            if (libraryIconContent == null)
                return;

            ConfigureLibraryIconContent();

            foreach (Button button in spawnedLibraryButtons)
            {
                if (button != null)
                    Destroy(button.gameObject);
            }

            spawnedLibraryButtons.Clear();

            foreach (LibraryDisplayItem item in items)
            {
                Button button = CreateLibraryIconButton(item);

                if (button == null)
                    continue;

                spawnedLibraryButtons.Add(button);
            }

            RectTransform contentRect = libraryIconContent as RectTransform;
            if (contentRect != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        private Button CreateLibraryIconButton(LibraryDisplayItem item)
        {
            Button button = libraryIconButtonPrefab != null
                ? Instantiate(libraryIconButtonPrefab, libraryIconContent)
                : CreateRuntimeLibraryIconButton(libraryIconContent);

            if (button == null)
                return null;

            button.name = $"LibraryIcon_{item.Id}";
            button.gameObject.SetActive(true);
            ConfigureLibraryIconButton(button);

            Image icon = button.GetComponent<Image>();
            if (icon != null)
            {
                icon.sprite = item.Icon;
                icon.preserveAspect = true;
                icon.color = item.Icon != null
                    ? Color.white
                    : new Color(0.08f, 0.16f, 0.18f, 1f);
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
                label.text = item.Icon != null ? string.Empty : item.Title;

            LibraryDisplayItem capturedItem = item;
            button.onClick.AddListener(() => SelectLibraryItem(capturedItem));
            return button;
        }

        private void ConfigureLibraryIconContent()
        {
            RectTransform rectTransform = libraryIconContent as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.anchorMin = new Vector2(0f, 1f);
                rectTransform.anchorMax = new Vector2(1f, 1f);
                rectTransform.pivot = new Vector2(0f, 1f);
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.offsetMin = new Vector2(0f, rectTransform.offsetMin.y);
                rectTransform.offsetMax = new Vector2(0f, rectTransform.offsetMax.y);
            }

            VerticalLayoutGroup layout =
                libraryIconContent.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = libraryIconContent.gameObject.AddComponent<VerticalLayoutGroup>();

            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 16f;
            layout.padding = new RectOffset(0, 0, 16, 16);
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter =
                libraryIconContent.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = libraryIconContent.gameObject.AddComponent<ContentSizeFitter>();

            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void ConfigureLibraryIconButton(Button button)
        {
            RectTransform rectTransform = button.GetComponent<RectTransform>();
            if (rectTransform != null)
                rectTransform.sizeDelta = new Vector2(100f, 100f);

            LayoutElement layoutElement = button.GetComponent<LayoutElement>();
            if (layoutElement == null)
                layoutElement = button.gameObject.AddComponent<LayoutElement>();

            layoutElement.preferredWidth = 100f;
            layoutElement.preferredHeight = 100f;
            layoutElement.minWidth = 100f;
            layoutElement.minHeight = 100f;
            layoutElement.flexibleWidth = 0f;
            layoutElement.flexibleHeight = 0f;
        }

        private static Button CreateRuntimeLibraryIconButton(Transform parent)
        {
            GameObject buttonObject = new GameObject(
                "LibraryIcon",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button)
            );
            buttonObject.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(100f, 100f);

            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.16f, 0.18f, 1f);

            return buttonObject.GetComponent<Button>();
        }

        private void HandleMapAction()
        {
            ExpeditionDiscoveryController discovery = ExpeditionDiscoveryController.Instance;

            if (selectedLocation == null || discovery == null)
                return;

            if (CanTravelToSelectedLocation())
                TravelToSelectedLocation();
            else if (selectedLocation.DiscoverySource == Locations.DiscoverySource.Drone)
                LaunchDroneForSelectedLocation();
        }

        private void LaunchDroneForSelectedLocation()
        {
            DroneScanController drone = DroneScanController.Instance;

            if (drone != null && drone.LaunchScan(selectedLocation))
                RefreshSelectedMapSector();
        }

        private void HandleAntennaCalibration()
        {
            AntennaController antenna = AntennaController.Instance;

            if (antenna != null && antenna.StartCalibration())
            {
                SetText(mapText, "ANTENNA CALIBRATION\n\nSignal analysis started.");
                RefreshAntennaCalibrationButton();
                RefreshSelectedMapSector();
            }
        }

        private void HandleAntennaSignalFound(ExpeditionLocationData signal)
        {
            if (!IsOpen || mapPanel == null || !mapPanel.activeSelf)
                return;

            RefreshMapSectors();
            RefreshSignalButtons();
            RefreshAntennaCalibrationButton();

            SetText(
                mapText,
                signal != null
                    ? $"SIGNAL FOUND\n\nUnknown signal detected in mapped sector."
                    : "SIGNAL FOUND"
            );
        }

        private void HandleAntennaSignalNotFound()
        {
            if (!IsOpen || mapPanel == null || !mapPanel.activeSelf)
                return;

            RefreshAntennaCalibrationButton();
            SetText(
                mapText,
                "SIGNAL NOT FOUND\n\nAntenna calibration completed. No temporary signal was detected."
            );
        }

        private void TravelToSelectedLocation()
        {
            if (selectedLocation == null ||
                !CanTravelToSelectedLocation() ||
                !Application.CanStreamedLevelBeLoaded(selectedLocation.SceneName))
            {
                SetText(mapText, "Travel unavailable.");
                return;
            }

            SceneTransitionState.SetPendingSpawnPoint(selectedLocation.SpawnPointId);
            AntennaController.Instance?.ConsumeActiveSignal(selectedLocation);
            Close();
            SceneManager.LoadScene(selectedLocation.SceneName);
        }

        private bool CanTravelToSelectedLocation()
        {
            if (IsLocationDiscovered(selectedLocation))
                return true;

            AntennaController antenna = AntennaController.Instance;
            return antenna != null &&
                   antenna.ActiveSignal == selectedLocation;
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

        private static void SetText(TMP_Text target, string message)
        {
            if (target != null)
                target.text = message;
        }

        private void SetMapAction(bool visible, string label, bool interactable)
        {
            if (travelButton == null)
                return;

            label ??= string.Empty;

            if (travelButton.gameObject.activeSelf != visible)
                travelButton.gameObject.SetActive(visible);

            if (travelButton.interactable != interactable)
                travelButton.interactable = interactable;

            TMP_Text buttonLabel = travelButton.GetComponentInChildren<TMP_Text>(true);
            if (buttonLabel != null && buttonLabel.text != label)
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

        private void CacheTextReferences()
        {
            if (statusText == null && statusPanel != null)
                statusText = FindTextByName(statusPanel.transform, "StatusText") ??
                    statusPanel.GetComponentInChildren<TMP_Text>(true);

            if (mapText == null && mapPanel != null)
                mapText = FindTextByName(mapPanel.transform, "MapText") ??
                    mapPanel.GetComponentInChildren<TMP_Text>(true);

            if (libraryText == null && libraryPanel != null)
                libraryText = FindTextByName(libraryPanel.transform, "LibraryText") ??
                    libraryPanel.GetComponentInChildren<TMP_Text>(true);

            CacheLibraryReferences();
        }

        private void CacheLibraryReferences()
        {
            if (libraryPanel == null)
                return;

            Transform root = libraryPanel.transform;

            if (stationLibraryTabButton == null)
                stationLibraryTabButton = FindButtonByName(root, "StationTabButton");

            if (anomalyLibraryTabButton == null)
                anomalyLibraryTabButton = FindButtonByName(root, "AnomalyTabButton");

            if (recordsLibraryTabButton == null)
                recordsLibraryTabButton = FindButtonByName(root, "RecordsTabButton");

            if (libraryIconContent == null)
                libraryIconContent = FindTransformByName(root, "Content");

            TMP_Text descriptionText = FindTextByName(root, "Description");
            if (descriptionText != null)
                libraryText = descriptionText;
        }

        private void CacheMapReferences()
        {
            if (mapPanel == null)
                return;

            if (antennaCalibrationButton == null)
            {
                antennaCalibrationButton =
                    FindButtonByName(mapPanel.transform, "AntennaCalibrationButton") ??
                    FindButtonByName(mapPanel.transform, "CalibrateAntennaButton");

                if (antennaCalibrationButton != null)
                    antennaCalibrationButton.onClick.AddListener(HandleAntennaCalibration);
            }
        }

        private static TMP_Text FindTextByName(Transform root, string objectName)
        {
            TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text.name == objectName)
                    return text;
            }

            return null;
        }

        private static Button FindButtonByName(Transform root, string objectName)
        {
            Button[] buttons = root.GetComponentsInChildren<Button>(true);
            foreach (Button button in buttons)
            {
                if (button.name == objectName)
                    return button;
            }

            return null;
        }

        private static Transform FindTransformByName(Transform root, string objectName)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in transforms)
            {
                if (child.name == objectName)
                    return child;
            }

            return null;
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

        private void SubscribeToAntenna()
        {
            AntennaController antenna = AntennaController.Instance;

            if (antenna == null || antenna == subscribedAntenna)
                return;

            if (subscribedAntenna != null)
            {
                subscribedAntenna.SignalFound -= HandleAntennaSignalFound;
                subscribedAntenna.SignalNotFound -= HandleAntennaSignalNotFound;
            }

            subscribedAntenna = antenna;
            subscribedAntenna.SignalFound += HandleAntennaSignalFound;
            subscribedAntenna.SignalNotFound += HandleAntennaSignalNotFound;
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
            EnergySystemController.Instance?.SetConsumerActive(
                TerminalConsumerId,
                false
            );
            if (subscribedDiscovery != null)
                subscribedDiscovery.LocationDiscovered -= HandleLocationDiscovered;

            if (subscribedAntenna != null)
            {
                subscribedAntenna.SignalFound -= HandleAntennaSignalFound;
                subscribedAntenna.SignalNotFound -= HandleAntennaSignalNotFound;
            }

            if (exitButton != null)
                exitButton.onClick.RemoveListener(Close);

            if (statusTabButton != null)
                statusTabButton.onClick.RemoveListener(ShowStatusSection);

            if (mapTabButton != null)
                mapTabButton.onClick.RemoveListener(ShowMapSection);

            if (libraryTabButton != null)
                libraryTabButton.onClick.RemoveListener(ShowLibrarySection);

            if (stationLibraryTabButton != null)
                stationLibraryTabButton.onClick.RemoveAllListeners();

            if (anomalyLibraryTabButton != null)
                anomalyLibraryTabButton.onClick.RemoveAllListeners();

            if (recordsLibraryTabButton != null)
                recordsLibraryTabButton.onClick.RemoveAllListeners();

            if (travelButton != null)
                travelButton.onClick.RemoveListener(HandleMapAction);

            if (antennaCalibrationButton != null)
                antennaCalibrationButton.onClick.RemoveListener(HandleAntennaCalibration);

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
