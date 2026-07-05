public interface IInteractable
{
    string InteractionText { get; }
    bool CanInteract { get; }
    InteractionType InteractionType { get; }
    float HoldDuration { get; }
    float HoldProgress { get; }

    void Interact();
    void StartHold();
    void UpdateHold(float deltaTime);
    void CancelHold();
}