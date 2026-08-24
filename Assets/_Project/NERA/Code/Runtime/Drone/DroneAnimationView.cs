using UnityEngine;
using UnityEngine.SceneManagement;

namespace NERA.Drone
{
    /// <summary>
    /// Mirrors the station drone flight state on every loaded drone Animator.
    /// Views are installed automatically for the authored full-size and mini
    /// drone controllers, including inactive terminal UI hierarchies.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class DroneAnimationView : MonoBehaviour
    {
        public const string MainControllerName = "Station_Drone";
        public const string MiniControllerName = "Station_Mini_Drone";
        public const string MainLaunchStateName = "Dron_Start";
        public const string MainReturnStateName = "Dron_End";
        public const string MiniLaunchStateName = "Dron_Start_Mini";
        public const string MiniReturnStateName = "Dron_End_Mini";

        private const string BaseLayerPrefix = "Base Layer.";

        private static DroneAnimationView mainView;
        private static float nextMainViewLookupAt;

        private Animator animator;
        private DroneScanController controller;
        private bool isMainController;
        private string launchClipName;
        private string returnClipName;
        private int launchStateHash;
        private int returnStateHash;
        private DroneState? renderedState;
        private bool returnPlaying;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            mainView = null;
            nextMainViewLookupAt = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallInitialViews()
        {
            InstallLoadedViews();
        }

        public static bool SupportsController(
            RuntimeAnimatorController runtimeController)
        {
            if (runtimeController == null)
                return false;

            return runtimeController.name == MainControllerName ||
                runtimeController.name == MiniControllerName;
        }

        private static void HandleSceneLoaded(Scene _, LoadSceneMode __)
        {
            InstallLoadedViews();
        }

        private static void InstallLoadedViews()
        {
            Animator[] animators = FindObjectsByType<Animator>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (Animator candidate in animators)
            {
                if (!SupportsController(candidate.runtimeAnimatorController))
                    continue;

                DroneAnimationView view =
                    candidate.GetComponent<DroneAnimationView>() ??
                    candidate.gameObject.AddComponent<DroneAnimationView>();
                if (candidate.runtimeAnimatorController.name ==
                    MainControllerName)
                {
                    mainView = view;
                }
            }
        }

        private void Awake()
        {
            animator = GetComponent<Animator>();
            ConfigureStateNames();
            RegisterMainView();
        }

        private void OnEnable()
        {
            animator ??= GetComponent<Animator>();
            ConfigureStateNames();
            RegisterMainView();
            DroneScanController.InstanceChanged += HandleInstanceChanged;
            Bind(DroneScanController.Instance);
        }

        private void Update()
        {
            if (!returnPlaying || animator == null)
                return;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.fullPathHash != returnStateHash ||
                state.normalizedTime < 1f)
            {
                return;
            }

            returnPlaying = false;
            animator.speed = 0f;
        }

        private void LateUpdate()
        {
            if (!isMainController)
                SynchronizeWithMain(false);
        }

        private void OnDisable()
        {
            DroneScanController.InstanceChanged -= HandleInstanceChanged;
            Bind(null);
        }

        private void HandleInstanceChanged(DroneScanController value)
        {
            Bind(value);
        }

        private void Bind(DroneScanController value)
        {
            if (controller == value)
            {
                if (controller != null && !renderedState.HasValue)
                    ApplyBoundState(controller.State);
                return;
            }

            if (controller != null)
            {
                controller.StateChanged -= HandleStateChanged;
                if (isMainController)
                    controller.UnregisterAnimationDriver(this);
            }

            controller = value;
            renderedState = null;
            returnPlaying = false;

            if (controller == null)
                return;

            if (isMainController)
                controller.RegisterAnimationDriver(this);
            controller.StateChanged += HandleStateChanged;
            ApplyBoundState(controller.State);
        }

        // Animation Event receiver for Dron_Start.
        public void Start_Scan()
        {
            if (isMainController)
                controller?.NotifyLaunchAnimationEvent();
        }

        // Animation Event receiver for Dron_End.
        public void End_Scan()
        {
            if (isMainController)
                controller?.NotifyReturnAnimationEvent();
        }

        private void HandleStateChanged(DroneState state)
        {
            ApplyState(state);
        }

        private void ApplyBoundState(DroneState state)
        {
            if (!isMainController && SynchronizeWithMain())
            {
                renderedState = state;
                return;
            }

            ApplyState(state);
        }

        private void ApplyState(DroneState state)
        {
            if (renderedState == state || animator == null)
                return;

            switch (state)
            {
                case DroneState.Scanning:
                    returnPlaying = false;
                    PlayOneShot(launchStateHash);
                    break;

                case DroneState.ScanComplete:
                    // ScanComplete is also used for ordinary charging, for
                    // example after a battery-capacity upgrade. Only an
                    // expedition return is allowed to play the flight clip.
                    if (controller?.IsExpeditionInProgress == true)
                    {
                        PlayOneShot(returnStateHash);
                        returnPlaying = true;
                    }
                    else if (!returnPlaying)
                    {
                        SnapToHomePose();
                    }
                    break;

                default:
                    if (!returnPlaying)
                        SnapToHomePose();
                    break;
            }

            renderedState = state;
        }

        private void PlayOneShot(int stateHash)
        {
            if (!animator.HasState(0, stateHash))
                return;

            animator.speed = 1f;
            animator.Play(stateHash, 0, 0f);
            animator.Update(0f);
        }

        private void SnapToHomePose()
        {
            animator.speed = 0f;
            if (!animator.HasState(0, returnStateHash))
                return;

            animator.Play(returnStateHash, 0, 1f);
            animator.Update(0f);
        }

        private bool SynchronizeWithMain(bool force = true)
        {
            DroneAnimationView source = ResolveMainView();
            if (source == null || source == this || source.animator == null ||
                animator == null)
            {
                return false;
            }

            AnimatorStateInfo sourceState =
                source.animator.GetCurrentAnimatorStateInfo(0);
            int targetStateHash;
            if (sourceState.fullPathHash == source.launchStateHash)
            {
                targetStateHash = launchStateHash;
                returnPlaying = false;
            }
            else if (sourceState.fullPathHash == source.returnStateHash)
            {
                targetStateHash = returnStateHash;
                returnPlaying = source.returnPlaying;
            }
            else
            {
                return false;
            }

            if (!animator.HasState(0, targetStateHash))
                return false;

            float normalizedTime = Mathf.Clamp01(sourceState.normalizedTime);
            float playbackSpeed = source.animator.speed <= 0f ||
                sourceState.normalizedTime >= 1f
                    ? 0f
                    : source.animator.speed;
            AnimatorStateInfo targetState =
                animator.GetCurrentAnimatorStateInfo(0);
            if (!force &&
                targetState.fullPathHash == targetStateHash &&
                Mathf.Abs(targetState.normalizedTime - normalizedTime) <=
                    0.005f &&
                Mathf.Approximately(animator.speed, playbackSpeed))
            {
                return true;
            }

            animator.speed = 1f;
            animator.Play(targetStateHash, 0, normalizedTime);
            animator.Update(0f);
            animator.speed = playbackSpeed;
            return true;
        }

        private static DroneAnimationView ResolveMainView()
        {
            if (mainView != null && mainView.animator != null)
                return mainView;

            if (Time.unscaledTime < nextMainViewLookupAt)
                return null;

            nextMainViewLookupAt = Time.unscaledTime + 0.5f;

            DroneAnimationView[] views = FindObjectsByType<DroneAnimationView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (DroneAnimationView candidate in views)
            {
                Animator candidateAnimator =
                    candidate != null
                        ? candidate.GetComponent<Animator>()
                        : null;
                if (candidateAnimator?.runtimeAnimatorController == null ||
                    candidateAnimator.runtimeAnimatorController.name !=
                    MainControllerName)
                {
                    continue;
                }

                candidate.animator = candidateAnimator;
                candidate.ConfigureStateNames();
                mainView = candidate;
                return mainView;
            }

            return null;
        }

        private void RegisterMainView()
        {
            if (isMainController)
                mainView = this;
        }

        private void OnDestroy()
        {
            if (mainView == this)
                mainView = null;
        }

        private void ConfigureStateNames()
        {
            isMainController = animator != null &&
                animator.runtimeAnimatorController != null &&
                animator.runtimeAnimatorController.name == MainControllerName;
            bool mini = animator != null &&
                animator.runtimeAnimatorController != null &&
                animator.runtimeAnimatorController.name == MiniControllerName;
            launchClipName = mini
                ? MiniLaunchStateName
                : MainLaunchStateName;
            returnClipName = mini
                ? MiniReturnStateName
                : MainReturnStateName;
            launchStateHash = Animator.StringToHash(
                BaseLayerPrefix + launchClipName);
            returnStateHash = Animator.StringToHash(
                BaseLayerPrefix + returnClipName);
        }

    }
}
