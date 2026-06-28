using System;
using UnityEngine;

public class RepairableObject : MonoBehaviour, IInteractionHandler
{
    [Header("Repair")]
    [SerializeField] private string objectId = "repairable_object";

    [Header("State")]
    public bool isRepaired = false;

    [Header("Visual")]
    [SerializeField] private GameObject brokenIcon;
    [SerializeField] private GameObject highlightEffect;

    public event Action<RepairableObject> OnRepairStarted;
    public event Action<RepairableObject> OnRepaired;
    public event Action<RepairableObject> OnRepairCancelled;
    public event Action<RepairableObject> OnBroken;

    public bool IsRepaired => isRepaired;

    private Interactable interactable;

    private void Awake()
    {
        interactable = GetComponent<Interactable>();
        UpdateVisualState();

        if (isRepaired)
        {
            HideHighlight();

            if (interactable != null)
                interactable.SetCanInteract(false);
        }
        else
        {
            ShowHighlight();

            if (interactable != null)
                interactable.SetCanInteract(true);
        }
    }

    public void OnInteractionStarted(PlayerInteraction player)
    {
        if (isRepaired)
            return;

        OnRepairStarted?.Invoke(this);
        Debug.Log($"Repair started: {objectId}");
    }

    public void OnInteractionCompleted(PlayerInteraction player)
    {
        CompleteRepair();
    }

    public void OnInteractionCancelled(PlayerInteraction player)
    {
        if (isRepaired)
            return;

        OnRepairCancelled?.Invoke(this);
        Debug.Log($"Repair cancelled: {objectId}");
    }

    public void CompleteRepair()
    {
        if (isRepaired)
            return;

        isRepaired = true;

        UpdateVisualState();
        HideHighlight();

        if (interactable != null)
            interactable.SetCanInteract(false);

        OnRepaired?.Invoke(this);
        Debug.Log($"Repair completed: {objectId}");
    }

    public void BreakObject()
    {
        isRepaired = false;

        UpdateVisualState();
        ShowHighlight();

        if (interactable != null)
        {
            interactable.SetCanInteract(true);
            interactable.ResetInteraction();
        }

        OnBroken?.Invoke(this);
        Debug.Log($"Object broken: {objectId}");
    }

    public void ShowHighlight()
    {
        if (highlightEffect != null)
            highlightEffect.SetActive(true);
    }

    public void HideHighlight()
    {
        if (highlightEffect != null)
            highlightEffect.SetActive(false);
    }

    public void SetHighlight(bool value)
    {
        if (value)
            ShowHighlight();
        else
            HideHighlight();
    }

    private void UpdateVisualState()
    {
        if (brokenIcon != null)
            brokenIcon.SetActive(!isRepaired);
    }
}
