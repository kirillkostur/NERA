using NERA.Graphics;
using NERA.Interaction;
using NERA.Player;
using NERA.Station;
using NERA.World;
using Unity.Cinemachine;
using UnityEngine;

namespace NERA.Terminal
{
    public sealed class TerminalAccessInteractable : BaseInteractable
    {
        private const int MapScreenIndex = 0;
        private const int StationScreenIndex = 1;

        [Header("Terminal View")]
        [SerializeField] private CinemachineVirtualCameraBase terminalCamera;
        [SerializeField, Min(1)] private int terminalCameraPriority = 200;
        [SerializeField, Min(0.1f)] private float cameraBlendTimeout = 5f;

        [Header("Powered Decoration")]
        [SerializeField] private Transform visualRoot;
        [SerializeField] private GameObject stationVisual;
        [SerializeField] private GameObject mapVisual;

        [Header("Weather Decoration")]
        [SerializeField] private ParticleEffectController sandstormEffect;

        private StationPowerController subscribedPower;
        private StationSystemsController subscribedSystems;
        private PrioritySettings previousCameraPriority;
        private bool hasPreviousCameraPriority;
        private int decorScreenIndex = StationScreenIndex;
        private Camera cachedBrainCamera;
        private CinemachineBrain cachedBrain;
        public float CameraBlendTimeout => cameraBlendTimeout;

        private void Awake()
        {
            ResolveReferences();
            RefreshSandstormVisual();
            ApplyPowerState(false);
        }

        private void OnEnable()
        {
            StationWeatherController.AnySandstormStarted -=
                HandleSandstormStarted;
            StationWeatherController.AnySandstormStarted +=
                HandleSandstormStarted;
            StationWeatherController.AnySandstormEnded -=
                HandleSandstormEnded;
            StationWeatherController.AnySandstormEnded +=
                HandleSandstormEnded;
            StationPowerController.InstanceChanged +=
                HandlePowerControllerInstanceChanged;
            StationSystemsController.InstanceChanged +=
                HandleSystemsControllerInstanceChanged;
            BindPowerController(StationPowerController.Instance);
            BindSystemsController(StationSystemsController.Instance);
            RefreshPoweredDecoration();
            RefreshSandstormVisual();
        }

        public override InteractionPrompt GetPrompt()
        {
            StationPowerController power = StationPowerController.Instance;

            if (power == null || !power.IsPowered)
            {
                return new InteractionPrompt(
                    "Use Terminal",
                    InteractionMode.Press,
                    0f,
                    false,
                    "Terminal Offline — Restore Power First"
                );
            }

            StationSystemsController systems = StationSystemsController.Instance;
            if (systems != null &&
                !systems.IsRequestedActive(StationSystemType.Terminal))
            {
                return new InteractionPrompt(
                    "Start Terminal",
                    InteractionMode.Press,
                    0f,
                    true,
                    string.Empty);
            }

            return base.GetPrompt();
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            StationPowerController power = StationPowerController.Instance;

            if (power == null || !power.IsPowered)
                return;

            StationSystemsController systems = StationSystemsController.Instance;
            if (systems != null &&
                !systems.IsRequestedActive(StationSystemType.Terminal))
            {
                systems.SetCriticalSystemActive(
                    StationSystemType.Terminal,
                    true);
            }

            TerminalUIScreen screen = TerminalUIScreen.Instance;

            if (screen == null)
            {
                Debug.LogError("TerminalAccessInteractable: TerminalUIScreen is missing.", this);
                return;
            }

            base.CompleteInteraction(interactor);
            screen.Open(this);
        }

        public void ShowDecorationForScreen(int screenIndex)
        {
            if (!IsTerminalOperational())
                return;

            decorScreenIndex = screenIndex == MapScreenIndex
                ? MapScreenIndex
                : StationScreenIndex;
            ApplyPowerState(true);
        }

        public bool BeginTerminalView(ParkourPlayerBridge player)
        {
            ResolveReferences();
            if (terminalCamera == null)
            {
                Debug.LogError(
                    "TerminalAccessInteractable: VirtualCam is missing.",
                    this);
                return false;
            }

            if (!hasPreviousCameraPriority)
            {
                previousCameraPriority = terminalCamera.Priority;
                hasPreviousCameraPriority = true;
            }

            terminalCamera.Priority = terminalCameraPriority;
            terminalCamera.PreviousStateIsValid = false;
            return player?.GameplayCamera != null;
        }

        public bool IsTerminalCameraReady(ParkourPlayerBridge player)
        {
            if (terminalCamera == null || player?.GameplayCamera == null)
                return true;

            if (cachedBrainCamera != player.GameplayCamera)
            {
                cachedBrainCamera = player.GameplayCamera;
                cachedBrain = cachedBrainCamera.GetComponent<CinemachineBrain>();
            }

            return cachedBrain == null ||
                !cachedBrain.IsBlending && ReferenceEquals(
                    cachedBrain.ActiveVirtualCamera,
                    terminalCamera);
        }

        public void EndTerminalView()
        {
            if (terminalCamera != null && hasPreviousCameraPriority)
            {
                terminalCamera.Priority = previousCameraPriority;
                terminalCamera.PreviousStateIsValid = false;
            }

            hasPreviousCameraPriority = false;
        }

        private void BindPowerController(StationPowerController power)
        {
            if (subscribedPower == power)
                return;

            if (subscribedPower != null)
                subscribedPower.StateChanged -= HandlePowerStateChanged;

            subscribedPower = power;
            if (subscribedPower != null)
                subscribedPower.StateChanged += HandlePowerStateChanged;
            RefreshPoweredDecoration();
        }

        private void HandlePowerStateChanged(StationPowerState state)
        {
            bool powered = state == StationPowerState.Online;
            if (!powered)
                decorScreenIndex = StationScreenIndex;
            RefreshPoweredDecoration();
        }

        private void HandlePowerControllerInstanceChanged(
            StationPowerController power)
        {
            BindPowerController(power);
        }

        private void BindSystemsController(StationSystemsController systems)
        {
            if (subscribedSystems == systems)
                return;

            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged -= HandleSystemsChanged;

            subscribedSystems = systems;
            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged += HandleSystemsChanged;
            RefreshPoweredDecoration();
        }

        private void HandleSystemsControllerInstanceChanged(
            StationSystemsController systems)
        {
            BindSystemsController(systems);
        }

        private void HandleSystemsChanged()
        {
            if (subscribedSystems != null &&
                !subscribedSystems.IsRequestedActive(
                    StationSystemType.Terminal))
            {
                decorScreenIndex = StationScreenIndex;
            }

            RefreshPoweredDecoration();
        }

        private void RefreshPoweredDecoration()
        {
            bool operational = IsTerminalOperational();
            if (!operational)
                decorScreenIndex = StationScreenIndex;
            ApplyPowerState(operational);
        }

        private void HandleSandstormStarted(float _)
        {
            SetSandstormEffectPlaying(true);
        }

        private void HandleSandstormEnded(bool _)
        {
            SetSandstormEffectPlaying(false);
        }

        private void RefreshSandstormVisual()
        {
            SetSandstormEffectPlaying(
                StationWeatherController.Instance?.IsSandstormActive == true);
        }

        private void SetSandstormEffectPlaying(bool playing)
        {
            ResolveReferences();
            sandstormEffect?.SetPlaying(playing);
        }

        private bool IsTerminalOperational()
        {
            bool powered = subscribedPower != null &&
                subscribedPower.IsPowered;
            bool terminalEnabled = subscribedSystems == null ||
                subscribedSystems.IsRequestedActive(
                    StationSystemType.Terminal);
            return powered && terminalEnabled;
        }

        private void ApplyPowerState(bool powered)
        {
            ResolveReferences();
            NormalizeDecorationLayers();
            if (!powered)
            {
                if (stationVisual != null)
                    stationVisual.SetActive(false);
                if (mapVisual != null)
                    mapVisual.SetActive(false);
                if (visualRoot != null)
                    visualRoot.gameObject.SetActive(false);
                return;
            }

            bool showMap = decorScreenIndex == MapScreenIndex;
            if (stationVisual != null)
                stationVisual.SetActive(!showMap);
            if (mapVisual != null)
                mapVisual.SetActive(showMap);
            if (visualRoot != null)
                visualRoot.gameObject.SetActive(true);
        }

        private void ResolveReferences()
        {
            terminalCamera ??= transform.Find("VirtualCam")?
                .GetComponent<CinemachineVirtualCameraBase>();
            visualRoot ??= transform.Find("Visual_3D");
            stationVisual ??= visualRoot?.Find("SM_Station_Mini_3D")?
                .gameObject;
            mapVisual ??= visualRoot?.Find("SM_Map_Mini_3D")?.gameObject;
            sandstormEffect ??= stationVisual?.transform
                .Find("VFX_Sandstorm_Mini")?
                .GetComponent<ParticleEffectController>();
        }

        private void NormalizeDecorationLayers()
        {
            SetLayerRecursively(stationVisual, 0);
            SetLayerRecursively(mapVisual, 0);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
                return;

            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }

        private void OnDisable()
        {
            sandstormEffect?.StopImmediate();
            StationWeatherController.AnySandstormStarted -=
                HandleSandstormStarted;
            StationWeatherController.AnySandstormEnded -=
                HandleSandstormEnded;
            StationPowerController.InstanceChanged -=
                HandlePowerControllerInstanceChanged;
            StationSystemsController.InstanceChanged -=
                HandleSystemsControllerInstanceChanged;
            TerminalUIScreen.Instance?.HandleTerminalUnavailable(this);
            EndTerminalView();

            if (subscribedPower != null)
                subscribedPower.StateChanged -= HandlePowerStateChanged;
            subscribedPower = null;

            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged -= HandleSystemsChanged;
            subscribedSystems = null;
        }

        private void OnValidate()
        {
            ResolveReferences();
        }
    }
}
