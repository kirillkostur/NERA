using UnityEngine;

[RequireComponent(typeof(Collider))]
public abstract class InteractableBase : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private InteractionPreset preset;
    [SerializeField] private bool canInteract = true;

    public InteractionPreset Preset => preset;
    public bool CanInteract => canInteract && preset != null;

    public virtual void OnInteractionStarted(PlayerInteraction player)
    {
    }

    public abstract void OnInteractionCompleted(PlayerInteraction player);

    public virtual void OnInteractionCancelled(PlayerInteraction player)
    {
    }

    public void SetCanInteract(bool value)
    {
        canInteract = value;
    }
}
