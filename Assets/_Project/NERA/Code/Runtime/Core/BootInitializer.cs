using System.Collections;
using NERA.Antenna;
using NERA.Interaction;
using NERA.Expeditions;
using NERA.Inventory;
using NERA.Library;
using NERA.Player;
using NERA.Quests;
using NERA.Research;
using NERA.Save;
using NERA.Station;
using NERA.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace NERA.Core
{
    /// <summary>
    /// Keeps MainScene loaded additively as the explicit owner of Player, HUD,
    /// input and runtime services while content scenes are swapped around it.
    /// </summary>
    public sealed class BootInitializer : MonoBehaviour
    {
        [Header("Gameplay Start")]
        [SerializeField] private string initialSceneName = "Player_Station";
        [SerializeField] private string initialSpawnPointId = "Station_Start";
        [SerializeField] private string menuSceneName = "Boot";

        private static BootInitializer instance;
        private bool isReturningToMenu;
        private bool isLoading;
        private string currentGameplaySceneName;

        private ParkourPlayerBridge player;
        private PlayerInteractionController interactionController;
        private Camera playerCamera;
        private AudioListener playerAudio;
        private Canvas gameplayHud;
        private EventSystem gameplayEventSystem;

        public static BootInitializer Instance => instance;
        public bool IsLoading => isLoading;
        public string CurrentGameplaySceneName => currentGameplaySceneName;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            ConnectPersistentServices();
            ConnectRuntimeReferences();
            SetGameplayPresentationActive(false);
        }

        private void ConnectPersistentServices()
        {
            LibraryController library = GetComponent<LibraryController>();
            ResearchController research = GetComponent<ResearchController>();
            AntennaController antenna = GetComponent<AntennaController>();
            QuestController quests = GetComponent<QuestController>();

            if (library == null ||
                research == null ||
                antenna == null ||
                quests == null)
            {
                Debug.LogError(
                    "BootInitializer: RuntimeRoot is missing one or more persistent progression services.",
                    this
                );
                return;
            }

            research.SetPowerSource(GetComponent<StationPowerController>());
            research.SetLibrary(library);
        }

        private void Start()
        {
            if (instance != this)
                return;

            StartCoroutine(BeginSession());
        }

        private IEnumerator BeginSession()
        {
            isLoading = true;
            GameSessionLaunchRequest launchRequest =
                GameSessionLaunchState.ConsumeOrDefault();
            SaveGameController saveController =
                GetComponent<SaveGameController>();
            AutoSaveService autoSave = GetComponent<AutoSaveService>();
            CheckpointService checkpoints =
                GetComponent<CheckpointService>();
            saveController?.InitializeSession(launchRequest);
            autoSave?.InitializeSession();
            checkpoints?.InitializeSession();
            autoSave?.SetSuspended(true);

            string startSceneName = initialSceneName;
            string startSpawnPointId = initialSpawnPointId;
            bool resumeCheckpoint =
                launchRequest.Mode == GameLaunchMode.Continue &&
                saveController != null &&
                saveController.HasCheckpoint &&
                Application.CanStreamedLevelBeLoaded(
                    saveController.CheckpointSceneName);
            if (resumeCheckpoint)
            {
                startSceneName = saveController.CheckpointSceneName;
                startSpawnPointId = saveController.CheckpointUsesWorldPose
                    ? string.Empty
                    : saveController.CheckpointSpawnPointId;
                if (!saveController.CheckpointUsesWorldPose)
                {
                    checkpoints?.SuppressNextActivation(
                        startSceneName,
                        startSpawnPointId);
                }
            }

            if (string.IsNullOrWhiteSpace(startSceneName) ||
                !Application.CanStreamedLevelBeLoaded(startSceneName))
            {
                Debug.LogError(
                    $"BootInitializer: Scene '{startSceneName}' is not " +
                    "available in Build Settings.",
                    this
                );
                isLoading = false;
                autoSave?.SetSuspended(false);
                yield break;
            }

            Scene mainScene = gameObject.scene;
            if (mainScene.IsValid() && mainScene.isLoaded)
                SceneManager.SetActiveScene(mainScene);

            Scene menuScene = SceneManager.GetSceneByName(menuSceneName);
            if (menuScene.IsValid() &&
                menuScene.isLoaded &&
                menuScene != mainScene)
            {
                yield return SceneManager.UnloadSceneAsync(menuScene);
            }

            yield return SwitchGameplayScene(
                startSceneName,
                startSpawnPointId);
            if (resumeCheckpoint)
                RestorePlayerCheckpointPose(saveController);
            autoSave?.SetSuspended(false);
            if (!resumeCheckpoint)
            {
                checkpoints?.ActivateCheckpoint(
                    startSceneName,
                    startSpawnPointId);
                checkpoints?.SuppressNextActivation(
                    startSceneName,
                    startSpawnPointId);
            }
            isLoading = false;
        }

        public bool LoadGameplayScene(
            string sceneName,
            string spawnPointId)
        {
            if (isLoading ||
                string.IsNullOrWhiteSpace(sceneName) ||
                !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                return false;
            }

            isLoading = true;
            StartCoroutine(LoadGameplaySceneRoutine(sceneName, spawnPointId));
            return true;
        }

        private IEnumerator LoadGameplaySceneRoutine(
            string sceneName,
            string spawnPointId)
        {
            AutoSaveService autoSave = GetComponent<AutoSaveService>();
            CheckpointService checkpoints =
                GetComponent<CheckpointService>();
            autoSave?.Flush();
            autoSave?.SetSuspended(true);
            SetGameplayInputActive(false);
            yield return SwitchGameplayScene(sceneName, spawnPointId);
            autoSave?.SetSuspended(false);
            checkpoints?.ActivateCheckpoint(sceneName, spawnPointId);
            checkpoints?.SuppressNextActivation(sceneName, spawnPointId);
            SetGameplayInputActive(true);
            isLoading = false;
        }

        public IEnumerator ReloadGameplayFromCheckpoint(
            string sceneName,
            string spawnPointId)
        {
            isLoading = true;
            SetGameplayInputActive(false);
            SaveGameController saveController =
                GetComponent<SaveGameController>();
            string transitionSpawnPointId =
                saveController != null &&
                saveController.CheckpointUsesWorldPose
                    ? string.Empty
                    : spawnPointId;
            yield return SwitchGameplayScene(
                sceneName,
                transitionSpawnPointId,
                forceReload: true,
                reportSignals: false);
            RestorePlayerCheckpointPose(saveController);
            SetGameplayInputActive(true);
            isLoading = false;
        }

        private void RestorePlayerCheckpointPose(
            SaveGameController saveController)
        {
            if (saveController == null ||
                !saveController.CheckpointUsesWorldPose)
            {
                return;
            }

            if (player == null)
                player = FindFirstObjectByType<ParkourPlayerBridge>();
            player?.Teleport(
                saveController.CheckpointPosition,
                saveController.CheckpointRotation);
        }

        private IEnumerator SwitchGameplayScene(
            string sceneName,
            string spawnPointId,
            bool forceReload = false,
            bool reportSignals = true)
        {
            bool sameScene = string.Equals(
                    currentGameplaySceneName,
                    sceneName,
                    System.StringComparison.Ordinal);
            if (sameScene && !forceReload)
            {
                SetGameplayPresentationActive(true);
                yield break;
            }

            string previousSceneName = currentGameplaySceneName;
            if (sameScene && forceReload)
            {
                SetGameplayPresentationActive(false);
                Scene currentScene =
                    SceneManager.GetSceneByName(currentGameplaySceneName);
                if (currentScene.IsValid() && currentScene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(currentScene);
                currentGameplaySceneName = null;
            }

            SceneTransitionState.SetPendingSpawnPoint(spawnPointId);

            Scene targetScene = SceneManager.GetSceneByName(sceneName);
            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(
                    sceneName,
                    LoadSceneMode.Additive);
                if (loadOperation == null)
                {
                    Debug.LogError(
                        $"BootInitializer: Could not start loading scene " +
                        $"'{sceneName}'.",
                        this);
                    yield break;
                }

                yield return loadOperation;
                targetScene = SceneManager.GetSceneByName(sceneName);
            }

            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                Debug.LogError(
                    $"BootInitializer: Scene '{sceneName}' did not load.",
                    this);
                yield break;
            }

            if (reportSignals &&
                !string.IsNullOrWhiteSpace(previousSceneName))
                ReportSceneSignal(
                    QuestSignalType.LocationExited,
                    previousSceneName);

            SceneManager.SetActiveScene(targetScene);
            currentGameplaySceneName = sceneName;
            if (reportSignals)
                ReportSceneEntered(sceneName);

            if (!string.IsNullOrWhiteSpace(previousSceneName) &&
                !string.Equals(
                    previousSceneName,
                    sceneName,
                    System.StringComparison.Ordinal))
            {
                Scene previousScene =
                    SceneManager.GetSceneByName(previousSceneName);
                if (previousScene.IsValid() && previousScene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(previousScene);
            }

            SetGameplayPresentationActive(true);
        }

        private void ReportSceneEntered(string sceneName)
        {
            ReportSceneSignal(QuestSignalType.LocationEntered, sceneName);
        }

        private void ReportSceneSignal(
            QuestSignalType signalType,
            string sceneName)
        {
            string targetId = sceneName;
            string targetName = sceneName;
            ExpeditionDiscoveryController discovery =
                GetComponent<ExpeditionDiscoveryController>();
            if (discovery != null &&
                discovery.TryGetKnownLocationBySceneName(
                    sceneName,
                    out ExpeditionLocationData location))
            {
                targetId = location.LocationId;
                targetName = location.DisplayName;
            }

            QuestController.Instance?.Report(
                signalType,
                targetId,
                targetName);
        }

        public void ReturnToMainMenu()
        {
            if (isReturningToMenu)
                return;

            if (string.IsNullOrWhiteSpace(menuSceneName) ||
                !Application.CanStreamedLevelBeLoaded(menuSceneName))
            {
                Debug.LogError(
                    $"BootInitializer: Menu scene '{menuSceneName}' is not " +
                    "available in Build Settings.",
                    this);
                return;
            }

            isReturningToMenu = true;
            StartCoroutine(ReturnToMainMenuRoutine());
        }

        private IEnumerator ReturnToMainMenuRoutine()
        {
            isLoading = true;
            AutoSaveService autoSave = GetComponent<AutoSaveService>();
            if (autoSave == null || !autoSave.Flush())
            {
                GetComponent<SaveGameController>()?.Save();
            }
            autoSave?.SetSuspended(true);
            GameSessionLaunchState.Clear();
            SetGameplayPresentationActive(false);

            if (!string.IsNullOrWhiteSpace(currentGameplaySceneName))
            {
                Scene contentScene =
                    SceneManager.GetSceneByName(currentGameplaySceneName);
                if (contentScene.IsValid() && contentScene.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(contentScene);
                currentGameplaySceneName = null;
            }

            Scene menuScene = SceneManager.GetSceneByName(menuSceneName);
            if (!menuScene.IsValid() || !menuScene.isLoaded)
            {
                yield return SceneManager.LoadSceneAsync(
                    menuSceneName,
                    LoadSceneMode.Additive);
                menuScene = SceneManager.GetSceneByName(menuSceneName);
            }

            if (menuScene.IsValid() && menuScene.isLoaded)
                SceneManager.SetActiveScene(menuScene);

            Scene mainScene = gameObject.scene;
            if (mainScene.IsValid() && mainScene.isLoaded)
                SceneManager.UnloadSceneAsync(mainScene);
        }

        private void ConnectRuntimeReferences()
        {
            player = GetComponentInChildren<ParkourPlayerBridge>(true);
            interactionController =
                GetComponentInChildren<PlayerInteractionController>(true);
            playerCamera = player != null && player.GameplayCamera != null
                ? player.GameplayCamera
                : GetComponentInChildren<Camera>(true);
            playerAudio = playerCamera != null
                ? playerCamera.GetComponent<AudioListener>()
                : null;
            InventoryLabHUDController hud =
                GetComponentInChildren<InventoryLabHUDController>(true);
            gameplayHud = hud != null ? hud.GetComponent<Canvas>() : null;
            gameplayEventSystem =
                GetComponentInChildren<EventSystem>(true);
            InteractionPromptView promptView =
                GetComponentInChildren<InteractionPromptView>(true);

            if (promptView != null)
                promptView.SetInteractionController(interactionController);
        }

        private void SetGameplayPresentationActive(bool active)
        {
            if (playerCamera != null)
                playerCamera.enabled = active;
            if (playerAudio != null)
                playerAudio.enabled = active;
            if (gameplayHud != null)
                gameplayHud.enabled = active;
            if (gameplayEventSystem != null)
                gameplayEventSystem.gameObject.SetActive(active);

            SetGameplayInputActive(active);
            Cursor.lockState =
                active ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !active;
        }

        private void SetGameplayInputActive(bool active)
        {
            player?.SetInputEnabled(this, active);
            if (player == null && interactionController != null)
                interactionController.enabled = active;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
