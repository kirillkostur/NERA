using UnityEngine;

namespace NERA.Expeditions
{
    public sealed class ExpeditionSceneBootstrap : MonoBehaviour
    {
        private void Start()
        {
            ExpeditionProgressController progress = ExpeditionProgressController.Instance;

            if (progress == null)
            {
                Debug.LogError(
                    "ExpeditionSceneBootstrap: ExpeditionProgressController is missing. Start through Boot.",
                    this
                );
                return;
            }

            progress.MarkVisited();
        }
    }
}
