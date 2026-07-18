using NERA.Interaction;
using UnityEngine;

namespace NERA.Expeditions
{
    public sealed class AncientRecordInteractable : BaseInteractable
    {
        private void Awake()
        {
            SetActionText("Read Ancient Record");
        }

        private void Start()
        {
            ExpeditionProgressController progress = ExpeditionProgressController.Instance;
            if (progress != null && progress.AncientRecord01Found)
                gameObject.SetActive(false);
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            ExpeditionProgressController progress = ExpeditionProgressController.Instance;
            if (progress == null)
                return;

            progress.MarkAncientRecordFound();
            base.CompleteInteraction(interactor);
            SetAvailable(false, "Record already recovered");
            gameObject.SetActive(false);
        }
    }
}
