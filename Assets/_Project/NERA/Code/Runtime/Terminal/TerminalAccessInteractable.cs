using NERA.Interaction;
using NERA.Player;
using NERA.Station;
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

        private StationPowerController subscribedPower;
        private StationSystemsController subscribedSystems;
        private PrioritySettings previousCameraPriority;
        private bool hasPreviousCameraPriority;
        private bool passiveCleanupPending;
        private int decorScreenIndex = StationScreenIndex;
        public float CameraBlendTimeout => cameraBlendTimeout;

        private void Awake()
        {
            ResolveReferences();
            ApplyPowerState(false);
        }

        private void OnEnable()
        {
            StationSystemsController.InstanceChanged +=
                HandleSystemsControllerChanged;
            BindPowerController(StationPowerController.Instance);
            BindSystemsController(StationSystemsController.Instance);
            RefreshPoweredDecoration();
        }

        private void Update()
        {
            if (subscribedPower != StationPowerController.Instance)
                BindPowerController(StationPowerController.Instance);
        }

        private void LateUpdate()
        {
            if (!passiveCleanupPending)
                return;

            passiveCleanupPending = false;
            DisableDecorationInteraction();
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
                !systems.IsRequestedActive(StationSystemType.Computer))
            {
                return new InteractionPrompt(
                    "Start Computer",
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
                !systems.IsRequestedActive(StationSystemType.Computer))
            {
                systems.SetCriticalSystemActive(
                    StationSystemType.Computer,
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
            if (subscribedPower == null || !subscribedPower.IsPowered)
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

            CinemachineBrain brain =
                player.GameplayCamera.GetComponent<CinemachineBrain>();
            return brain == null ||
                !brain.IsBlending && ReferenceEquals(
                    brain.ActiveVirtualCamera,
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

        private void BindSystemsController(StationSystemsController systems)
        {
            if (subscribedSystems == systems)
                return;

            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged -= HandleSystemsChanged;

            subscribedSystems = systems;
            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged += HandleSystemsChanged;
            passiveCleanupPending = true;
        }

        private void HandleSystemsControllerChanged(
            StationSystemsController systems)
        {
            BindSystemsController(systems);
        }

        private void HandleSystemsChanged()
        {
            // StationObjectVisual rebuilds installed part prefabs on this
            // event. Disable their colliders after every rebuild as well.
            passiveCleanupPending = true;
        }

        private void HandlePowerStateChanged(StationPowerState state)
        {
            bool powered = state == StationPowerState.Online;
            if (!powered)
                decorScreenIndex = StationScreenIndex;
            ApplyPowerState(powered);
        }

        private void RefreshPoweredDecoration()
        {
            bool powered = subscribedPower != null &&
                subscribedPower.IsPowered;
            if (!powered)
                decorScreenIndex = StationScreenIndex;
            ApplyPowerState(powered);
        }

        private void ApplyPowerState(bool powered)
        {
            ResolveReferences();
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
            DisableDecorationInteraction();
        }

        private void DisableDecorationInteraction()
        {
            if (visualRoot == null)
                return;

            foreach (Collider collider in
                     visualRoot.GetComponentsInChildren<Collider>(true))
            {
                if (collider != null)
                    collider.enabled = false;
            }
        }

        private void ResolveReferences()
        {
            terminalCamera ??= transform.Find("VirtualCam")?
                .GetComponent<CinemachineVirtualCameraBase>();
            visualRoot ??= transform.Find("Visual_3D");
            stationVisual ??= visualRoot?.Find("SM_Station_Mini_3D")?
                .gameObject;
            mapVisual ??= visualRoot?.Find("SM_Map_Mini_3D")?.gameObject;
        }

        private void OnDisable()
        {
            TerminalUIScreen.Instance?.HandleTerminalUnavailable(this);
            EndTerminalView();

            StationSystemsController.InstanceChanged -=
                HandleSystemsControllerChanged;
            if (subscribedSystems != null)
                subscribedSystems.SystemsChanged -= HandleSystemsChanged;
            if (subscribedPower != null)
                subscribedPower.StateChanged -= HandlePowerStateChanged;
            subscribedSystems = null;
            subscribedPower = null;
        }

        private void OnValidate()
        {
            ResolveReferences();
        }
    }
}
