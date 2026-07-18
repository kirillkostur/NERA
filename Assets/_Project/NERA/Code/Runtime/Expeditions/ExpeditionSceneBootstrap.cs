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
                GameObject root = new GameObject("ExpeditionProgress");
                progress = root.AddComponent<ExpeditionProgressController>();
            }

            progress.MarkVisited();
        }
    }
}
