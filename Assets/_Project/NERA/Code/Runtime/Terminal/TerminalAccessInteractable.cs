using NERA.Interaction;
using NERA.Station;
using Unity.Cinemachine;
using UnityEngine;

namespace NERA.Terminal
{
    public sealed class TerminalAccessInteractable : BaseInteractable
    {
        [Header("Terminal View")]
        [SerializeField] private CinemachineVirtualCameraBase terminalCamera;
        [SerializeField] private Transform stationVisualRoot;
        [SerializeField] private Transform mapVisualRoot;
        [SerializeField, Min(1)] private int terminalCameraPriority = 200;

        private PrioritySettings previousCameraPriority;
        private bool hasPreviousCameraPriority;
        private bool terminalViewActive;

        public Transform StationVisualRoot
        {
            get
            {
                ResolveTerminalView();
                return stationVisualRoot;
            }
        }

        public Transform MapVisualRoot
        {
            get
            {
                ResolveTerminalView();
                return mapVisualRoot;
            }
        }

        private void Awake()
        {
            ResolveTerminalView();
            SetVisualsVisible(false, false);
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

        public void BeginTerminalView(int screenIndex)
        {
            ResolveTerminalView();
            terminalViewActive = true;

            if (terminalCamera != null)
            {
                if (!hasPreviousCameraPriority)
                {
                    previousCameraPriority = terminalCamera.Priority;
                    hasPreviousCameraPriority = true;
                }

                terminalCamera.Priority = terminalCameraPriority;
                terminalCamera.PreviousStateIsValid = false;
            }

            SetTerminalScreen(screenIndex);
        }

        public void SetTerminalScreen(int screenIndex)
        {
            bool showMap = terminalViewActive && screenIndex == 0;
            bool showStation = terminalViewActive && screenIndex == 1;
            SetVisualsVisible(showStation, showMap);
        }

        public void EndTerminalView()
        {
            terminalViewActive = false;
            SetVisualsVisible(false, false);

            if (terminalCamera != null && hasPreviousCameraPriority)
                terminalCamera.Priority = previousCameraPriority;

            hasPreviousCameraPriority = false;
        }

        private void ResolveTerminalView()
        {
            if (terminalCamera == null)
            {
                Transform cameraTransform = transform.Find("VirtualCamOrbit");
                terminalCamera = cameraTransform != null
                    ? cameraTransform.GetComponent<CinemachineVirtualCameraBase>()
                    : null;
            }

            Transform visualRoot = FindDescendant(transform, "Visual_3D");
            if (visualRoot == null)
                return;

            if (stationVisualRoot == null)
            {
                stationVisualRoot =
                    FindDescendant(visualRoot, "SM_Station_Mini_3D");
            }

            if (mapVisualRoot == null)
            {
                mapVisualRoot =
                    FindDescendant(visualRoot, "SM_Map_Mini_3D");
            }
        }

        private void SetVisualsVisible(bool stationVisible, bool mapVisible)
        {
            if (stationVisualRoot != null)
                stationVisualRoot.gameObject.SetActive(stationVisible);
            if (mapVisualRoot != null)
                mapVisualRoot.gameObject.SetActive(mapVisible);
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            if (root == null)
                return null;

            foreach (Transform child in root)
            {
                if (child.name == name)
                    return child;

                Transform nested = FindDescendant(child, name);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private void OnDisable()
        {
            if (terminalViewActive || hasPreviousCameraPriority)
                EndTerminalView();
        }
    }
}
