using NERA.Core;
using NERA.Expeditions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NERA.Interaction
{
    public sealed class SceneTransitionInteractable : BaseInteractable
    {
        [Header("Scene Transition")]
        [SerializeField] private string targetSceneName;
        [SerializeField] private string targetSpawnPointId;
        [SerializeField] private bool disableAfterUse = true;

        private bool isLoading;

        public override void CompleteInteraction(GameObject interactor)
        {
            if (isLoading)
                return;

            base.CompleteInteraction(interactor);

            if (string.IsNullOrWhiteSpace(targetSceneName) ||
                !Application.CanStreamedLevelBeLoaded(targetSceneName))
            {
                Debug.LogError(
                    $"SceneTransitionInteractable: Scene '{targetSceneName}' is not available in Build Settings.",
                    this
                );
                return;
            }

            isLoading = true;
            if (string.Equals(targetSceneName, "Player_Station", System.StringComparison.Ordinal))
            {
                ExpeditionProgressController progress = ExpeditionProgressController.Instance;
                if (progress != null)
                    progress.MarkReturned();
            }
            SceneTransitionState.SetPendingSpawnPoint(targetSpawnPointId);

            if (disableAfterUse)
                SetAvailable(false, "Loading");

            SceneManager.LoadScene(targetSceneName);
        }
    }
}
