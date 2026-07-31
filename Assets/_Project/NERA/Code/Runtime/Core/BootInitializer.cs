using System.Collections;
using NERA.Antenna;
using NERA.Interaction;
using NERA.Expeditions;
using NERA.Inventory;
using NERA.Library;
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

        private PlayerController player;
        private PlayerFollowCamera followCamera;
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
            GetComponent<SaveGameController>()?.InitializeSession(
                GameSessionLaunchState.ConsumeOrDefault());

            if (string.IsNullOrWhiteSpace(initialSceneName) ||
                !Application.CanStreamedLevelBeLoaded(initialSceneName))
            {
                Debug.LogError(
                    $"BootInitializer: Scene '{initialSceneName}' is not available in Build Settings.",
                    this
                );
                isLoading = false;
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
                initialSceneName,
                initialSpawnPointId);
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
            SetGameplayInputActive(false);
            yield return SwitchGameplayScene(sceneName, spawnPointId);
            SetGameplayInputActive(true);
            isLoading = false;
        }

        private IEnumerator SwitchGameplayScene(
            string sceneName,
            string spawnPointId)
        {
            if (string.Equals(
                    currentGameplaySceneName,
                    sceneName,
                    System.StringComparison.Ordinal))
            {
                SetGameplayPresentationActive(true);
                yield break;
            }

            string previousSceneName = currentGameplaySceneName;
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

            SceneManager.SetActiveScene(targetScene);
            currentGameplaySceneName = sceneName;
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
                QuestSignalType.LocationEntered,
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
            GetComponent<SaveGameController>()?.Save();
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
            player = GetComponentInChildren<PlayerController>(true);
            interactionController =
                GetComponentInChildren<PlayerInteractionController>(true);
            followCamera =
                GetComponentInChildren<PlayerFollowCamera>(true);
            playerCamera = followCamera != null
                ? followCamera.GetComponent<Camera>()
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

            if (player != null && playerCamera != null)
                player.SetCameraTransform(playerCamera.transform);

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
            player?.SetInputEnabled(active);
            followCamera?.SetInputEnabled(active);
            if (interactionController != null)
                interactionController.enabled = active;
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
