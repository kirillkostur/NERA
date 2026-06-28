using UnityEngine;

public class SearchableObject : MonoBehaviour, IInteractionHandler
{
    [Header("Search")]
    [SerializeField] private string containerId = "container_id";
    [SerializeField] private bool searchOnlyOnce = true;

    private bool wasSearched;
    private Interactable interactable;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
    }

    public void OnInteractionStarted(PlayerInteraction player)
    {
        Debug.Log($"Search started: {containerId}");
    }

    public void OnInteractionCompleted(PlayerInteraction player)
    {
        if (searchOnlyOnce && wasSearched)
            return;

        wasSearched = true;

        Debug.Log($"Search completed: {containerId}");

        if (searchOnlyOnce && interactable != null)
            interactable.SetCanInteract(false);
    }

    public void OnInteractionCancelled(PlayerInteraction player)
    {
        Debug.Log($"Search cancelled: {containerId}");
    }
}
