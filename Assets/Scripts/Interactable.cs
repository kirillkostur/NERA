using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private InteractionPreset preset;
    [SerializeField] private bool canInteract = true;

    private IInteractionHandler[] handlers;

    public InteractionPreset Preset => preset;
    public bool CanInteract => canInteract && preset != null;

    private void Awake()
    {
        handlers = GetComponents<IInteractionHandler>();
    }

    public void StartInteraction(PlayerInteraction player)
    {
        for (int i = 0; i < handlers.Length; i++)
            handlers[i].OnInteractionStarted(player);
    }

    public void CompleteInteraction(PlayerInteraction player)
    {
        for (int i = 0; i < handlers.Length; i++)
            handlers[i].OnInteractionCompleted(player);
    }

    public void CancelInteraction(PlayerInteraction player)
    {
        for (int i = 0; i < handlers.Length; i++)
            handlers[i].OnInteractionCancelled(player);
    }

    public void SetCanInteract(bool value)
    {
        canInteract = value;
    }

    public void ResetInteraction()
    {
        canInteract = true;
    }
}
