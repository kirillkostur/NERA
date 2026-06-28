public interface IInteractionHandler
{
    void OnInteractionStarted(PlayerInteraction player);
    void OnInteractionCompleted(PlayerInteraction player);
    void OnInteractionCancelled(PlayerInteraction player);
}
