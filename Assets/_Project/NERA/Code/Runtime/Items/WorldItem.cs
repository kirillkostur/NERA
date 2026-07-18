using NERA.Interaction;
using NERA.Inventory;
using UnityEngine;

namespace NERA.Items
{
    public sealed class WorldItem : BaseInteractable
    {
        [Header("Item")]
        [SerializeField] private ItemData itemData;
        [SerializeField] private bool destroyAfterPickup = true;

        public ItemData ItemData => itemData;

        public void Initialize(ItemData item)
        {
            itemData = item;
            destroyAfterPickup = true;
            SetActionText("Pick Up");
            SetAvailable(item != null, item == null ? "Item data missing" : string.Empty);
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

            if (!inventory.AddItem(itemData))
                return;

            base.CompleteInteraction(interactor);
            SetAvailable(false, "Picked Up");

            if (destroyAfterPickup)
                Destroy(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
