using NERA.Interaction;
using NERA.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NERA.Core
{
    public sealed class BootInitializer : MonoBehaviour
    {
        [Header("Initial Scene")]
        [SerializeField] private string initialSceneName = "Player_Station";
        [SerializeField] private string initialSpawnPointId = "Station_Start";

        private static BootInitializer instance;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            ConnectRuntimeReferences();
        }

        private void Start()
        {
            if (instance != this)
                return;

            if (string.IsNullOrWhiteSpace(initialSceneName) ||
                !Application.CanStreamedLevelBeLoaded(initialSceneName))
            {
                Debug.LogError(
                    $"BootInitializer: Scene '{initialSceneName}' is not available in Build Settings.",
                    this
                );
                return;
            }

            SceneTransitionState.SetPendingSpawnPoint(initialSpawnPointId);
            SceneManager.LoadScene(initialSceneName);
        }

        private void ConnectRuntimeReferences()
        {
            PlayerController player = GetComponentInChildren<PlayerController>(true);
            PlayerInteractionController interactionController =
                GetComponentInChildren<PlayerInteractionController>(true);
            Camera playerCamera = GetComponentInChildren<Camera>(true);
            InteractionPromptView promptView =
                GetComponentInChildren<InteractionPromptView>(true);

            if (player != null && playerCamera != null)
                player.SetCameraTransform(playerCamera.transform);

            if (promptView != null)
                promptView.SetInteractionController(interactionController);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }
    }
}
