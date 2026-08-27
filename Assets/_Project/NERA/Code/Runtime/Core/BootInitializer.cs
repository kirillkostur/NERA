using System;
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
        private bool sessionLoadingScreenRequested;
        private bool transitionLoadingScreenRequested;
        private string currentGameplaySceneName;

        private ParkourPlayerBridge player;
        private PlayerInteractionController interactionController;
        private Camera playerCamera;
        private AudioListener playerAudio;
        private Canvas gameplayHud;
        private EventSystem gameplayEventSystem;

        public static BootInitializer Instance => instance;
        public bool IsLoading => isLoading;
        public SceneTransitionResult LastTransitionResult { get; private set; }
        public string CurrentGameplaySceneName => currentGameplaySceneName;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            sessionLoadingScreenRequested =
                LoadingScreenController.BeginLoading();
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
                if (sessionLoadingScreenRequested)
                {
                    sessionLoadingScreenRequested = false;
                    yield return LoadingScreenController.EndLoadingAndWait();
                }
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

            LastTransitionResult = SceneTransitionResult.None;
            SceneTransitionResult transitionResult =
                SceneTransitionResult.Failure;
            yield return SwitchGameplayScene(
                startSceneName,
                startSpawnPointId,
                result => transitionResult = result);
            LastTransitionResult = transitionResult;
            autoSave?.SetSuspended(false);
            if (transitionResult == SceneTransitionResult.Success)
            {
                if (resumeCheckpoint)
                {
                    RestorePlayerCheckpointPose(saveController);
                }
                else
                {
                    checkpoints?.ActivateCheckpoint(
                        startSceneName,
                        startSpawnPointId);
                    checkpoints?.SuppressNextActivation(
                        startSceneName,
                        startSpawnPointId);
                }
            }

            if (sessionLoadingScreenRequested)
            {
                sessionLoadingScreenRequested = false;
                yield return LoadingScreenController.EndLoadingAndWait();
            }
            RestorePresentationAfterTransition();
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
            transitionLoadingScreenRequested =
                LoadingScreenController.BeginLoading();
            if (transitionLoadingScreenRequested)
                yield return null;
            LastTransitionResult = SceneTransitionResult.None;
            SceneTransitionResult transitionResult =
                SceneTransitionResult.Failure;
            yield return SwitchGameplayScene(
                sceneName,
                spawnPointId,
                result => transitionResult = result);
            LastTransitionResult = transitionResult;
            autoSave?.SetSuspended(false);
            if (transitionResult == SceneTransitionResult.Success)
            {
                checkpoints?.ActivateCheckpoint(sceneName, spawnPointId);
                checkpoints?.SuppressNextActivation(sceneName, spawnPointId);
            }

            if (transitionLoadingScreenRequested)
            {
                transitionLoadingScreenRequested = false;
                yield return LoadingScreenController.EndLoadingAndWait();
            }
            RestorePresentationAfterTransition();
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
            LastTransitionResult = SceneTransitionResult.None;
            SceneTransitionResult transitionResult =
                SceneTransitionResult.Failure;
            yield return SwitchGameplayScene(
                sceneName,
                transitionSpawnPointId,
                result => transitionResult = result,
                forceReload: true,
                reportSignals: false);
            LastTransitionResult = transitionResult;
            if (transitionResult == SceneTransitionResult.Success)
                RestorePlayerCheckpointPose(saveController);
        }

        public void CompleteCheckpointRestore()
        {
            RestorePresentationAfterTransition();
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
            Action<SceneTransitionResult> completed,
            bool forceReload = false,
            bool reportSignals = true)
        {
            SceneTransitionResult result = SceneTransitionResult.Failure;
            bool loadedByTransition = false;
            Scene targetScene = default;
            try
            {
                bool sameScene = string.Equals(
                    currentGameplaySceneName,
                    sceneName,
                    StringComparison.Ordinal);
                string previousSceneName = currentGameplaySceneName;
                SceneTransitionState.SetPendingSpawnPoint(spawnPointId);

                if (sameScene && !forceReload)
                {
                    targetScene = SceneManager.GetSceneByName(sceneName);
                    if (!TryApplyPendingSpawnPoint(
                            targetScene,
                            spawnPointId))
                    {
                        yield break;
                    }

                    result = SceneTransitionResult.Success;
                    yield break;
                }

                if (sameScene && forceReload)
                {
                    SetGameplayPresentationActive(false);
                    Scene currentScene =
                        SceneManager.GetSceneByName(currentGameplaySceneName);
                    if (currentScene.IsValid() && currentScene.isLoaded)
                    {
                        yield return SceneManager.UnloadSceneAsync(
                            currentScene);
                    }

                    currentGameplaySceneName = null;
                }

                targetScene = SceneManager.GetSceneByName(sceneName);
                if (!targetScene.IsValid() || !targetScene.isLoaded)
                {
                    AsyncOperation loadOperation = null;
                    try
                    {
                        loadOperation = SceneManager.LoadSceneAsync(
                            sceneName,
                            LoadSceneMode.Additive);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError(
                            $"BootInitializer: Could not start loading scene " +
                            $"'{sceneName}': {exception.Message}",
                            this);
                    }

                    if (loadOperation == null)
                    {
                        if (!Application.CanStreamedLevelBeLoaded(sceneName))
                        {
                            Debug.LogError(
                                $"BootInitializer: Could not start loading " +
                                $"scene '{sceneName}'.",
                                this);
                        }

                        yield break;
                    }

                    loadedByTransition = true;
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

                if (!TryApplyPendingSpawnPoint(
                        targetScene,
                        spawnPointId))
                {
                    if (loadedByTransition)
                        yield return SceneManager.UnloadSceneAsync(targetScene);
                    yield break;
                }

                if (reportSignals &&
                    !string.IsNullOrWhiteSpace(previousSceneName))
                {
                    ReportSceneSignal(
                        QuestSignalType.LocationExited,
                        previousSceneName);
                }

                SceneManager.SetActiveScene(targetScene);
                currentGameplaySceneName = sceneName;
                if (reportSignals)
                    ReportSceneEntered(sceneName);

                if (!string.IsNullOrWhiteSpace(previousSceneName) &&
                    !string.Equals(
                        previousSceneName,
                        sceneName,
                        StringComparison.Ordinal))
                {
                    Scene previousScene =
                        SceneManager.GetSceneByName(previousSceneName);
                    if (previousScene.IsValid() && previousScene.isLoaded)
                    {
                        yield return SceneManager.UnloadSceneAsync(
                            previousScene);
                    }
                }

                result = SceneTransitionResult.Success;
            }
            finally
            {
                SceneTransitionState.ClearPendingSpawnPoint();
                completed?.Invoke(result);
            }
        }

        private bool TryApplyPendingSpawnPoint(
            Scene targetScene,
            string spawnPointId)
        {
            if (string.IsNullOrWhiteSpace(spawnPointId))
                return true;

            SceneSpawnPoint match = null;
            AutoSaveCheckpoint checkpointMatch = null;
            foreach (GameObject root in targetScene.GetRootGameObjects())
            {
                foreach (SceneSpawnPoint spawnPoint in
                         root.GetComponentsInChildren<SceneSpawnPoint>(true))
                {
                    if (!string.Equals(
                            spawnPoint.SpawnPointId,
                            spawnPointId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (match != null)
                    {
                        Debug.LogError(
                            $"BootInitializer: Scene '{targetScene.name}' " +
                            $"has duplicate spawn point '{spawnPointId}'.",
                            this);
                        return false;
                    }

                    match = spawnPoint;
                }

                foreach (AutoSaveCheckpoint checkpoint in
                         root.GetComponentsInChildren<AutoSaveCheckpoint>(true))
                {
                    if (!string.Equals(
                            checkpoint.CheckpointId,
                            spawnPointId,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (match != null || checkpointMatch != null)
                    {
                        Debug.LogError(
                            $"BootInitializer: Scene '{targetScene.name}' " +
                            $"has duplicate spawn point '{spawnPointId}'.",
                            this);
                        return false;
                    }

                    checkpointMatch = checkpoint;
                }
            }

            if (match == null && checkpointMatch == null)
            {
                Debug.LogError(
                    $"BootInitializer: Scene '{targetScene.name}' has no " +
                    $"spawn point '{spawnPointId}'.",
                    this);
                return false;
            }

            if (player == null)
                player = FindFirstObjectByType<ParkourPlayerBridge>();
            if (player == null ||
                !SceneTransitionState.TryConsumeSpawnPoint(spawnPointId))
            {
                Debug.LogError(
                    $"BootInitializer: Could not apply spawn point " +
                    $"'{spawnPointId}' in scene '{targetScene.name}'.",
                    this);
                return false;
            }

            return match != null
                ? match.TryTeleport(player)
                : checkpointMatch.TryTeleport(player);
        }

        private void RestorePresentationAfterTransition()
        {
            Scene gameplayScene = SceneManager.GetSceneByName(
                currentGameplaySceneName);
            SetGameplayPresentationActive(
                gameplayScene.IsValid() && gameplayScene.isLoaded);
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
            StationUpgradeModeController upgradeMode =
                StationUpgradeModeController.Instance;
            if (upgradeMode != null &&
                !upgradeMode.PrepareForSessionEnd())
            {
                isLoading = false;
                isReturningToMenu = false;
                yield break;
            }
            AutoSaveService autoSave = GetComponent<AutoSaveService>();
            if (autoSave == null || !autoSave.Flush())
            {
                GetComponent<SaveGameController>()?.Save();
            }
            autoSave?.SetSuspended(true);
            GameSessionLaunchState.Clear();
            SetGameplayPresentationActive(false);
            transitionLoadingScreenRequested =
                LoadingScreenController.BeginLoading();
            if (transitionLoadingScreenRequested)
                yield return null;

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

            if (transitionLoadingScreenRequested)
            {
                transitionLoadingScreenRequested = false;
                yield return LoadingScreenController.EndLoadingAndWait();
            }

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
            if (sessionLoadingScreenRequested)
            {
                sessionLoadingScreenRequested = false;
                LoadingScreenController.EndLoading();
            }
            if (transitionLoadingScreenRequested)
            {
                transitionLoadingScreenRequested = false;
                LoadingScreenController.EndLoading();
            }
            if (instance == this)
                instance = null;
        }
    }
}
