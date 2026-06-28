using UnityEngine;

public class SearchableInteractable : InteractableBase
{
    [Header("Search")]
    [SerializeField] private string containerId = "container_id";
    [SerializeField] private bool searchOnlyOnce = true;

    private bool wasSearched;

    public override void OnInteractionCompleted(PlayerInteraction player)
    {
        if (searchOnlyOnce && wasSearched)
            return;

        wasSearched = true;

        Debug.Log($"Searched container: {containerId}");

        SetCanInteract(!searchOnlyOnce);
    }
}
