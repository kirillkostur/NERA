using NERA.Core;
using UnityEngine;

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
            if (disableAfterUse)
                SetAvailable(false, "Loading");

            BootInitializer runtime = BootInitializer.Instance;
            if (runtime == null ||
                !runtime.LoadGameplayScene(
                    targetSceneName,
                    targetSpawnPointId))
            {
                isLoading = false;
                if (disableAfterUse)
                    SetAvailable(true);
                Debug.LogError(
                    "SceneTransitionInteractable: MainScene runtime loader " +
                    "is unavailable or busy.",
                    this);
                return;
            }
        }
    }
}
