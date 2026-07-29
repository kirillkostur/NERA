using NERA.Core;
using NERA.Energy;
using NERA.Interaction;
using NERA.Inventory;
using NERA.Station;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Terminal
{
    /// <summary>
    /// Coordinates the authored terminal HUD. Individual screens own their
    /// content; this component only controls terminal lifetime and navigation.
    /// </summary>
    public sealed class TerminalUIScreen : MonoBehaviour
    {
        private const string TerminalConsumerId = "central_terminal";

        public static TerminalUIScreen Instance { get; private set; }
        public bool IsOpen { get; private set; }
        public int ActiveScreenIndex => activeScreenIndex;

        private CanvasGroup canvasGroup;
        private Button exitButton;
        private Button mapButton;
        private Button stationButton;
        private Button libraryButton;
        private Button storageButton;
        private Button nextButton;
        private Button backButton;

        private GameObject mapScreen;
        private GameObject stationScreen;
        private GameObject libraryScreen;
        private GameObject storageScreen;
        private GameObject[] screens;
        private int activeScreenIndex = 1;
        private int navigationInputUnlockFrame;

        private PlayerController playerController;
        private PlayerInteractionController interactionController;
        private PlayerFollowCamera followCamera;

        private TerminalMapScreenController mapController;
        private TerminalStationScreenController stationController;
        private TerminalLibraryScreenController libraryController;
        private TerminalStorageScreenController storageController;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            canvasGroup = GetComponent<CanvasGroup>();
            CacheHierarchy();
            BindButtons();
            AttachScreenControllers();
            RegisterTerminalConsumer();
            ShowScreen(activeScreenIndex);
            SetVisible(false);
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (Time.frameCount > navigationInputUnlockFrame)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                    ShowPreviousScreen();
                else if (Input.GetKeyDown(KeyCode.E))
                    ShowNextScreen();
            }

            EnergySystemController energy = EnergySystemController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            bool computerActive = systems == null ||
                systems.IsRequestedActive(StationSystemType.Computer);
            if (!computerActive ||
                (energy != null && !energy.IsConsumerPowered(TerminalConsumerId)))
            {
                Close();
            }
        }

        public void Open()
        {
            if (IsOpen)
                return;

            StationSystemsController systems = StationSystemsController.Instance;
            if (systems != null &&
                !systems.IsRequestedActive(StationSystemType.Computer))
            {
                return;
            }

            RegisterTerminalConsumer();
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy != null)
            {
                energy.SetConsumerActive(TerminalConsumerId, true);
                if (!energy.IsConsumerPowered(TerminalConsumerId))
                {
                    energy.SetConsumerActive(TerminalConsumerId, false);
                    return;
                }
            }

            CachePlayerControllers();
            IsOpen = true;
            navigationInputUnlockFrame = Time.frameCount + 1;
            SetPlayerControl(false);
            InventoryLabHUDController.Instance?.SetExternalUiLock(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetVisible(true);
            ShowScreen(activeScreenIndex);
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            EnergySystemController.Instance?.SetConsumerActive(
                TerminalConsumerId,
                false);
            storageController?.HandleTerminalClosed();
            InventoryLabHUDController.Instance?.SetExternalUiLock(false);
            mapController?.SetScreenActive(false);
            stationController?.SetScreenActive(false);
            SetVisible(false);
            SetPlayerControl(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public bool TravelTo(NERA.Expeditions.ExpeditionLocationData location)
        {
            if (location == null ||
                !Application.CanStreamedLevelBeLoaded(location.SceneName))
            {
                return false;
            }

            NERA.Expeditions.ExpeditionDiscoveryController discovery =
                NERA.Expeditions.ExpeditionDiscoveryController.Instance;
            NERA.Antenna.AntennaController antenna =
                NERA.Antenna.AntennaController.Instance;
            bool available =
                (discovery != null && discovery.IsDiscovered(location)) ||
                (antenna != null && antenna.ActiveSignal == location);
            if (!available)
                return false;

            BootInitializer runtime = BootInitializer.Instance;
            if (runtime == null ||
                !runtime.LoadGameplayScene(
                    location.SceneName,
                    location.SpawnPointId))
            {
                return false;
            }

            antenna?.ConsumeActiveSignal(location);
            Close();
            return true;
        }

        public void ShowMap() => ShowScreen(0);
        public void ShowStation() => ShowScreen(1);
        public void ShowLibrary() => ShowScreen(2);
        public void ShowStorage() => ShowScreen(3);
        public void ShowNextScreen() =>
            ShowScreen((activeScreenIndex + 1) % screens.Length);
        public void ShowPreviousScreen() =>
            ShowScreen(
                (activeScreenIndex - 1 + screens.Length) % screens.Length);

        private void CacheHierarchy()
        {
            exitButton = TerminalUIUtility.FindComponent<Button>(
                transform, "ExitButton");
            mapButton = TerminalUIUtility.FindComponent<Button>(
                transform, "MapButton");
            stationButton = TerminalUIUtility.FindComponent<Button>(
                transform, "StationButton");
            libraryButton = TerminalUIUtility.FindComponent<Button>(
                transform, "LibraryButton");
            storageButton = TerminalUIUtility.FindComponent<Button>(
                transform, "StorageButton");
            nextButton = TerminalUIUtility.FindComponent<Button>(
                transform, "NextButton");
            backButton = TerminalUIUtility.FindComponent<Button>(
                transform, "BackButton");

            mapScreen = TerminalUIUtility.Find(transform, "MapScreen")?.gameObject;
            stationScreen =
                TerminalUIUtility.Find(transform, "StationScreen")?.gameObject;
            libraryScreen =
                TerminalUIUtility.Find(transform, "LibraryScreen")?.gameObject;
            storageScreen =
                TerminalUIUtility.Find(transform, "StorageScreen")?.gameObject;
            screens = new[]
            {
                mapScreen,
                stationScreen,
                libraryScreen,
                storageScreen
            };
        }

        private void BindButtons()
        {
            exitButton?.onClick.AddListener(Close);
            mapButton?.onClick.AddListener(ShowMap);
            stationButton?.onClick.AddListener(ShowStation);
            libraryButton?.onClick.AddListener(ShowLibrary);
            storageButton?.onClick.AddListener(ShowStorage);
            nextButton?.onClick.AddListener(ShowNextScreen);
            backButton?.onClick.AddListener(ShowPreviousScreen);
        }

        private void AttachScreenControllers()
        {
            if (mapScreen != null)
            {
                mapController =
                    mapScreen.GetComponent<TerminalMapScreenController>() ??
                    mapScreen.AddComponent<TerminalMapScreenController>();
                mapController.Initialize(this);
            }

            if (stationScreen != null)
            {
                stationController =
                    stationScreen.GetComponent<TerminalStationScreenController>() ??
                    stationScreen.AddComponent<TerminalStationScreenController>();
                stationController.Initialize(this);
            }

            if (libraryScreen != null)
            {
                libraryController =
                    libraryScreen.GetComponent<TerminalLibraryScreenController>() ??
                    libraryScreen.AddComponent<TerminalLibraryScreenController>();
                libraryController.Initialize();
            }

            if (storageScreen != null)
            {
                storageController =
                    storageScreen.GetComponent<TerminalStorageScreenController>() ??
                    storageScreen.AddComponent<TerminalStorageScreenController>();
                storageController.Initialize();
            }
        }

        private void ShowScreen(int index)
        {
            if (screens == null || screens.Length == 0)
                return;

            activeScreenIndex = Mathf.Clamp(index, 0, screens.Length - 1);
            for (int i = 0; i < screens.Length; i++)
            {
                if (screens[i] != null)
                    screens[i].SetActive(i == activeScreenIndex);
            }

            mapController?.SetScreenActive(activeScreenIndex == 0);
            stationController?.SetScreenActive(activeScreenIndex == 1);
            libraryController?.SetScreenActive(activeScreenIndex == 2);
            storageController?.SetScreenActive(activeScreenIndex == 3);
        }

        private void RegisterTerminalConsumer()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return;

            energy.RegisterConsumer(
                TerminalConsumerId,
                energy.Config.TerminalConsumption,
                false);
            energy.SetConsumerActive(TerminalConsumerId, IsOpen);
        }

        private void CachePlayerControllers()
        {
            if (playerController == null)
                playerController = FindFirstObjectByType<PlayerController>();
            if (interactionController == null)
            {
                interactionController =
                    FindFirstObjectByType<PlayerInteractionController>();
            }
            if (followCamera == null)
                followCamera = FindFirstObjectByType<PlayerFollowCamera>();
        }

        private void SetPlayerControl(bool enabled)
        {
            if (playerController != null)
                playerController.SetInputEnabled(enabled);
            if (interactionController != null)
                interactionController.enabled = enabled;
            if (followCamera != null)
                followCamera.SetInputEnabled(enabled);
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
                false);
            if (Instance == this)
                Instance = null;
        }
    }
}
