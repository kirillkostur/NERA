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
                    "ExpeditionSceneBootstrap: ExpeditionProgressController is missing. Start gameplay through MainScene.",
                    this
                );
                return;
            }

            progress.MarkVisited();
        }
    }
}
