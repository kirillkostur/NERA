using UnityEngine;

public class PickupInteractable : InteractableBase
{
    [Header("Pickup")]
    [SerializeField] private string itemId = "item_id";

    public override void OnInteractionCompleted(PlayerInteraction player)
    {
        Debug.Log($"Picked up item: {itemId}");

        gameObject.SetActive(false);
    }
}
