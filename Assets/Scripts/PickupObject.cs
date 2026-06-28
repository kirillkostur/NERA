using UnityEngine;

public class PickupObject : MonoBehaviour, IInteractionHandler
{
    [Header("Pickup")]
    [SerializeField] private string itemId = "item_id";

    public void OnInteractionStarted(PlayerInteraction player)
    {
    }

    public void OnInteractionCompleted(PlayerInteraction player)
    {
        Debug.Log($"Picked up: {itemId}");
        gameObject.SetActive(false);
    }

    public void OnInteractionCancelled(PlayerInteraction player)
    {
    }
}
