using NERA.Interaction;
using NERA.Inventory;
using NERA.Items;
using UnityEngine;

namespace NERA.Expeditions
{
    public sealed class ResearchObjectInteractable : BaseInteractable
    {
        [SerializeField, Min(0.1f)] private float inspectDuration = 2f;
        [SerializeField] private ItemData researchItem;

        private void Awake()
        {
            SetActionText("Inspect Memory Core");
        }

        public override InteractionPrompt GetPrompt()
        {
            return new InteractionPrompt(
                "Inspect Memory Core",
                InteractionMode.Hold,
                inspectDuration,
                true,
                string.Empty);
        }

        private void Start()
        {
            ExpeditionProgressController progress = ExpeditionProgressController.Instance;
            if (progress != null && progress.ResearchObject01Collected)
                gameObject.SetActive(false);
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            ExpeditionProgressController progress = ExpeditionProgressController.Instance;
            if (progress == null)
                return;

            progress.MarkResearchObjectCollected();

            PlayerInventory inventory = interactor != null
                ? interactor.GetComponent<PlayerInventory>()
                : null;

            if (inventory == null && interactor != null)
                inventory = interactor.GetComponentInParent<PlayerInventory>();

            if (inventory != null && researchItem != null)
                inventory.AddItem(researchItem);

            base.CompleteInteraction(interactor);
            SetAvailable(false, "Memory Core secured");
            gameObject.SetActive(false);
        }
    }
}
