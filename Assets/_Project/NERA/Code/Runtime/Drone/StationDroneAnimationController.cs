using UnityEngine;

namespace NERA.Drone
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class StationDroneAnimationController : MonoBehaviour
    {
        private static readonly int TakeoffState =
            Animator.StringToHash("Base Layer.Dron_Start");
        private static readonly int LandingState =
            Animator.StringToHash("Base Layer.Dron_End");

        [SerializeField] private Animator animator;

        private DroneScanController scanController;

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            // The drone must remain parked until a scan is actually launched.
            animator.enabled = false;
        }

        private void OnEnable()
        {
            TryBindScanController();
        }

        private void Start()
        {
            TryBindScanController();
        }

        private void TryBindScanController()
        {
            DroneScanController candidate = DroneScanController.Instance;
            if (candidate == scanController)
                return;

            UnbindScanController();
            scanController = candidate;

            if (scanController == null)
                return;

            scanController.StateChanged += HandleDroneStateChanged;

            if (scanController.State == DroneState.Scanning)
                PlayState(TakeoffState, "Dron_Start");
        }

        private void HandleDroneStateChanged(DroneState state)
        {
            switch (state)
            {
                case DroneState.Scanning:
                    PlayState(TakeoffState, "Dron_Start");
                    break;

                case DroneState.ScanComplete:
                    PlayState(LandingState, "Dron_End");
                    break;
            }
        }

        private void PlayState(int stateHash, string stateName)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
                return;

            animator.enabled = true;

            if (!animator.HasState(0, stateHash))
            {
                Debug.LogWarningFormat(
                    this,
                    "StationDroneAnimationController: state '{0}' was not found.",
                    stateName
                );
                return;
            }

            animator.Play(stateHash, 0, 0f);
        }

        private void UnbindScanController()
        {
            if (scanController != null)
                scanController.StateChanged -= HandleDroneStateChanged;

            scanController = null;
        }

        private void OnDisable()
        {
            UnbindScanController();
        }
    }
}
