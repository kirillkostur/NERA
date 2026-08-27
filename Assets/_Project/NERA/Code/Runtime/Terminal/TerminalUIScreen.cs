using System.Collections;
using NERA.Core;
using NERA.Energy;
using NERA.Interaction;
using NERA.Inventory;
using NERA.Player;
using NERA.Station;
using UnityEngine;
using UnityEngine.Rendering.Universal;
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
        private const int DefaultGameplayRendererIndex = -1;
        private const int PreviewRendererIndex = 1;

        public static TerminalUIScreen Instance { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsOpening { get; private set; }
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

        private ParkourPlayerBridge playerController;
        private TerminalAccessInteractable activeTerminal;
        private Coroutine openingRoutine;
        private Camera suspendedGameplayCamera;
        private bool suspendedGameplayCameraWasEnabled;
        private int suspendedGameplayCameraCullingMask;
        private CameraClearFlags suspendedGameplayCameraClearFlags;
        private Color suspendedGameplayCameraBackgroundColor;
        private bool suspendedGameplayCameraAllowHdr;
        private bool suspendedGameplayCameraAllowMsaa;
        private bool suspendedGameplayCameraUseOcclusionCulling;
        private UniversalAdditionalCameraData suspendedGameplayCameraData;
        private bool suspendedGameplayCameraRenderShadows;
        private CameraOverrideOption suspendedGameplayCameraDepthOption;
        private CameraOverrideOption suspendedGameplayCameraColorOption;
        private bool suspendedGameplayCameraPostProcessing;
        private AntialiasingMode suspendedGameplayCameraAntialiasing;
        private bool suspendedGameplayCameraAllowXr;
        private bool isGameplayCameraRenderingSuspended;

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
            if (!IsOpen && !IsOpening)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (IsOpen && Time.frameCount > navigationInputUnlockFrame)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                    ShowPreviousScreen();
                else if (Input.GetKeyDown(KeyCode.E))
                    ShowNextScreen();
            }

            EnergySystemController energy = EnergySystemController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            bool terminalActive = systems == null ||
                systems.IsRequestedActive(StationSystemType.Terminal);
            if (!terminalActive ||
                (energy != null && !energy.IsConsumerPowered(TerminalConsumerId)))
            {
                Close();
            }
        }

        public void Open()
        {
            if (!TryBeginSession())
                return;

            CompleteOpen();
        }

        public void Open(TerminalAccessInteractable terminal)
        {
            if (terminal == null)
            {
                Open();
                return;
            }

            if (!TryBeginSession())
                return;

            activeTerminal = terminal;
            activeTerminal.ShowDecorationForScreen(activeScreenIndex);
            if (!activeTerminal.BeginTerminalView(playerController))
            {
                CompleteOpen();
                return;
            }

            IsOpening = true;
            openingRoutine = StartCoroutine(OpenAfterCameraTransition());
        }

        public void Close()
        {
            if (!IsOpen && !IsOpening)
                return;

            RestoreGameplayCameraRendering();
            if (openingRoutine != null)
                StopCoroutine(openingRoutine);
            openingRoutine = null;
            IsOpening = false;
            IsOpen = false;
            EnergySystemController.Instance?.SetConsumerActive(
                TerminalConsumerId,
                false);
            storageController?.HandleTerminalClosed();
            InventoryLabHUDController.Instance?.SetExternalUiLock(false);
            mapController?.SetScreenActive(false);
            stationController?.SetScreenActive(false);
            SetVisible(false);
            activeTerminal?.ShowDecorationForScreen(activeScreenIndex);
            activeTerminal?.EndTerminalView();
            activeTerminal = null;
            SetPlayerControl(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void HandleTerminalUnavailable(
            TerminalAccessInteractable terminal)
        {
            if (activeTerminal == terminal)
                Close();
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
            activeTerminal?.ShowDecorationForScreen(activeScreenIndex);
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

            float consumption = StationSystemsConfig.GetEffectiveStat(
                StationSystemType.Terminal,
                string.Empty,
                StationObjectStat.IdleEnergyConsumption,
                2f);
            energy.RegisterConsumer(
                TerminalConsumerId,
                consumption,
                energy.Config.GetMinimumCharge01(
                    StationSystemType.Terminal),
                StationSystemType.Terminal);
            energy.SetConsumerActive(TerminalConsumerId, IsOpen);
        }

        private bool TryBeginSession()
        {
            if (IsOpen || IsOpening)
                return false;

            StationSystemsController systems = StationSystemsController.Instance;
            if (systems != null &&
                !systems.IsRequestedActive(StationSystemType.Terminal))
            {
                return false;
            }

            RegisterTerminalConsumer();
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy != null)
            {
                energy.SetConsumerActive(TerminalConsumerId, true);
                if (!energy.IsConsumerPowered(TerminalConsumerId))
                {
                    energy.SetConsumerActive(TerminalConsumerId, false);
                    return false;
                }
            }

            CachePlayerControllers();
            SetPlayerControl(false);
            InventoryLabHUDController.Instance?.SetExternalUiLock(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            SetVisible(false);
            return true;
        }

        private IEnumerator OpenAfterCameraTransition()
        {
            int startedFrame = Time.frameCount;
            float startedAt = Time.unscaledTime;
            yield return null;

            while (IsOpening && activeTerminal != null)
            {
                bool completed =
                    activeTerminal.IsTerminalCameraReady(playerController);
                bool timedOut = Time.unscaledTime - startedAt >=
                    Mathf.Max(0.1f, activeTerminal.CameraBlendTimeout);
                if (Time.frameCount > startedFrame &&
                    (completed || timedOut))
                {
                    break;
                }

                yield return null;
            }

            openingRoutine = null;
            if (IsOpening)
                CompleteOpen();
        }

        private void CompleteOpen()
        {
            IsOpening = false;
            IsOpen = true;
            navigationInputUnlockFrame = Time.frameCount + 1;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetVisible(true);
            ShowScreen(activeScreenIndex);
            SuspendGameplayCameraRendering();
        }

        private void CachePlayerControllers()
        {
            if (playerController == null)
                playerController =
                    FindFirstObjectByType<ParkourPlayerBridge>();
        }

        private void SetPlayerControl(bool enabled)
        {
            if (playerController != null)
                playerController.SetInputEnabled(this, enabled);
        }

        private void SuspendGameplayCameraRendering()
        {
            if (isGameplayCameraRenderingSuspended)
                return;

            suspendedGameplayCamera = playerController?.GameplayCamera;
            if (suspendedGameplayCamera == null)
                return;

            suspendedGameplayCameraWasEnabled =
                suspendedGameplayCamera.enabled;
            suspendedGameplayCameraCullingMask =
                suspendedGameplayCamera.cullingMask;
            suspendedGameplayCameraClearFlags =
                suspendedGameplayCamera.clearFlags;
            suspendedGameplayCameraBackgroundColor =
                suspendedGameplayCamera.backgroundColor;
            suspendedGameplayCameraAllowHdr =
                suspendedGameplayCamera.allowHDR;
            suspendedGameplayCameraAllowMsaa =
                suspendedGameplayCamera.allowMSAA;
            suspendedGameplayCameraUseOcclusionCulling =
                suspendedGameplayCamera.useOcclusionCulling;

            suspendedGameplayCameraData =
                suspendedGameplayCamera.GetComponent<
                    UniversalAdditionalCameraData>();
            if (suspendedGameplayCameraData != null)
            {
                suspendedGameplayCameraRenderShadows =
                    suspendedGameplayCameraData.renderShadows;
                suspendedGameplayCameraDepthOption =
                    suspendedGameplayCameraData.requiresDepthOption;
                suspendedGameplayCameraColorOption =
                    suspendedGameplayCameraData.requiresColorOption;
                suspendedGameplayCameraPostProcessing =
                    suspendedGameplayCameraData.renderPostProcessing;
                suspendedGameplayCameraAntialiasing =
                    suspendedGameplayCameraData.antialiasing;
                suspendedGameplayCameraAllowXr =
                    suspendedGameplayCameraData.allowXRRendering;

                suspendedGameplayCameraData.renderShadows = false;
                suspendedGameplayCameraData.requiresDepthOption =
                    CameraOverrideOption.Off;
                suspendedGameplayCameraData.requiresColorOption =
                    CameraOverrideOption.Off;
                suspendedGameplayCameraData.renderPostProcessing = false;
                suspendedGameplayCameraData.antialiasing =
                    AntialiasingMode.None;
                suspendedGameplayCameraData.allowXRRendering = false;
                suspendedGameplayCameraData.SetRenderer(PreviewRendererIndex);
            }

            // Display 1 needs an active camera even for a Screen Space Overlay
            // canvas. Keep this camera alive, but prevent world rendering.
            suspendedGameplayCamera.cullingMask = 0;
            suspendedGameplayCamera.clearFlags = CameraClearFlags.SolidColor;
            suspendedGameplayCamera.backgroundColor = Color.black;
            suspendedGameplayCamera.allowHDR = false;
            suspendedGameplayCamera.allowMSAA = false;
            suspendedGameplayCamera.useOcclusionCulling = false;
            suspendedGameplayCamera.enabled = true;
            isGameplayCameraRenderingSuspended = true;
        }

        private void RestoreGameplayCameraRendering()
        {
            if (!isGameplayCameraRenderingSuspended)
                return;

            if (suspendedGameplayCamera != null)
            {
                suspendedGameplayCamera.cullingMask =
                    suspendedGameplayCameraCullingMask;
                suspendedGameplayCamera.clearFlags =
                    suspendedGameplayCameraClearFlags;
                suspendedGameplayCamera.backgroundColor =
                    suspendedGameplayCameraBackgroundColor;
                suspendedGameplayCamera.allowHDR =
                    suspendedGameplayCameraAllowHdr;
                suspendedGameplayCamera.allowMSAA =
                    suspendedGameplayCameraAllowMsaa;
                suspendedGameplayCamera.useOcclusionCulling =
                    suspendedGameplayCameraUseOcclusionCulling;
                suspendedGameplayCamera.enabled =
                    suspendedGameplayCameraWasEnabled;
            }

            if (suspendedGameplayCameraData != null)
            {
                suspendedGameplayCameraData.renderShadows =
                    suspendedGameplayCameraRenderShadows;
                suspendedGameplayCameraData.requiresDepthOption =
                    suspendedGameplayCameraDepthOption;
                suspendedGameplayCameraData.requiresColorOption =
                    suspendedGameplayCameraColorOption;
                suspendedGameplayCameraData.renderPostProcessing =
                    suspendedGameplayCameraPostProcessing;
                suspendedGameplayCameraData.antialiasing =
                    suspendedGameplayCameraAntialiasing;
                suspendedGameplayCameraData.allowXRRendering =
                    suspendedGameplayCameraAllowXr;
                suspendedGameplayCameraData.SetRenderer(
                    DefaultGameplayRendererIndex);
            }

            suspendedGameplayCamera = null;
            suspendedGameplayCameraData = null;
            suspendedGameplayCameraWasEnabled = false;
            isGameplayCameraRenderingSuspended = false;
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
            if (openingRoutine != null)
                StopCoroutine(openingRoutine);
            openingRoutine = null;
            IsOpening = false;
            RestoreGameplayCameraRendering();
            activeTerminal?.EndTerminalView();
            activeTerminal = null;
            if (playerController != null)
                playerController.SetInputEnabled(this, true);
            InventoryLabHUDController.Instance?.SetExternalUiLock(false);
            EnergySystemController.Instance?.SetConsumerActive(
                TerminalConsumerId,
                false);
            if (Instance == this)
                Instance = null;
        }
    }
}
