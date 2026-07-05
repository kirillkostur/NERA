using UnityEngine;

public abstract class BaseInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private LocalizedText interactionText;
    [SerializeField] private string fallbackInteractionText = "Interact";
    [SerializeField] private bool canInteract = true;
    [SerializeField] private InteractionType interactionType = InteractionType.Press;

    [Header("Hold Interaction")]
    [SerializeField] private float holdDuration = 1.5f;
    [SerializeField] private bool resetHoldAfterComplete = true;

    private float holdTimer;
    private bool isHolding;
    private bool isCompleted;

    public string InteractionText
    {
        get
        {
            if (interactionText == null)
                return fallbackInteractionText;

            return interactionText.GetText(fallbackInteractionText);
        }
    }

    public bool CanInteract => canInteract && !isCompleted;
    public InteractionType InteractionType => interactionType;
    public float HoldDuration => holdDuration;

    public float HoldProgress
    {
        get
        {
            if (holdDuration <= 0f)
                return 1f;

            return Mathf.Clamp01(holdTimer / holdDuration);
        }
    }

    public void Interact()
    {
        if (!CanInteract)
        {
            Debug.Log($"{name}: Interaction blocked.");
            return;
        }

        if (interactionType != InteractionType.Press)
        {
            Debug.LogWarning($"{name}: Interact() called on Hold interaction. Use StartHold/UpdateHold instead.");
            return;
        }

        CompleteInteraction();
    }

    public void StartHold()
    {
        if (!CanInteract)
            return;

        if (interactionType != InteractionType.Hold)
            return;

        isHolding = true;
        OnHoldStarted();
    }

    public void UpdateHold(float deltaTime)
    {
        if (!CanInteract)
            return;

        if (interactionType != InteractionType.Hold)
            return;

        if (!isHolding)
            StartHold();

        holdTimer += deltaTime;

        OnHoldUpdated(HoldProgress);

        if (holdTimer >= holdDuration)
            CompleteInteraction();
    }

    public void CancelHold()
    {
        if (interactionType != InteractionType.Hold)
            return;

        if (!isHolding && holdTimer <= 0f)
            return;

        isHolding = false;
        holdTimer = 0f;

        OnHoldCancelled();
    }

    private void CompleteInteraction()
    {
        if (!CanInteract)
            return;

        isHolding = false;

        OnInteractCompleted();

        if (interactionType == InteractionType.Hold && resetHoldAfterComplete)
            holdTimer = 0f;

        if (!resetHoldAfterComplete)
            isCompleted = true;
    }

    protected abstract void OnInteractCompleted();

    protected virtual void OnHoldStarted()
    {
    }

    protected virtual void OnHoldUpdated(float progress)
    {
    }

    protected virtual void OnHoldCancelled()
    {
    }

    public void SetCanInteract(bool value)
    {
        canInteract = value;
    }

    public void SetInteractionText(string value)
    {
        fallbackInteractionText = value;
    }

    public void SetInteractionType(InteractionType value)
    {
        interactionType = value;
    }

    public void ResetCompletedState()
    {
        isCompleted = false;
        holdTimer = 0f;
        isHolding = false;
    }
}