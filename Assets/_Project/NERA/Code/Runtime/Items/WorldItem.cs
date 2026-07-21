using NERA.Interaction;
using NERA.Inventory;
using NERA.Expeditions;
using NERA.Library;
using UnityEngine;

namespace NERA.Items
{
    public sealed class WorldItem : BaseInteractable
    {
        [Header("Item")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private bool destroyAfterPickup = true;

        private ItemInstance itemInstance;

        public ItemData ItemData => itemData;
        public ItemInstance ItemInstance => itemInstance;

        public void Initialize(ItemData item)
        {
            Initialize(ItemInstance.Create(item));
        }

        public void Initialize(ItemInstance instance)
        {
            itemInstance = instance;
            itemData = instance?.ItemData;
            destroyAfterPickup = true;
            SetActionText("Pick Up");
            SetAvailable(itemData != null, itemData == null ? "Item data missing" : string.Empty);
        }

        private void Reset()
        {
            SetActionText("Pick Up");
        }

        public override InteractionPrompt GetPrompt()
        {
            if (itemData == null)
            {
                return new InteractionPrompt(
                    "Pick Up",
                    InteractionMode.Press,
                    0f,
                    false,
                    "Item data missing"
                );
            }

            return base.GetPrompt();
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            if (itemData == null || interactor == null)
                return;

            PlayerInventory inventory = interactor.GetComponentInParent<PlayerInventory>();

            if (inventory == null)
            {
                Debug.LogWarning(
                    $"{name}: PlayerInventory was not found on the interactor.",
                    this
                );
                return;
            }

            ItemInstance instance = itemInstance ?? ItemInstance.Create(itemData);
            if (!inventory.AddItem(instance))
                return;

            LibraryController.Instance?.RegisterKnownItem(itemData);
            if (itemData.ResearchDefinition != null)
            {
                ExpeditionProgressController.Instance?.MarkResearchSampleCollected(
                    itemData.DisplayName
                );
            }

            base.CompleteInteraction(interactor);
            SetAvailable(false, "Picked Up");

            if (destroyAfterPickup)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
