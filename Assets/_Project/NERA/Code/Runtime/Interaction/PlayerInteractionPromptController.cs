using UnityEngine;

[RequireComponent(typeof(PlayerInteractionDetector))]
public class PlayerInteractionPromptController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InteractionPromptUI promptUI;

    [Header("Input")]
    [SerializeField] private string keyLabel = "E";

    private PlayerInteractionDetector interactionDetector;

    private void Awake()
    {
        interactionDetector = GetComponent<PlayerInteractionDetector>();
    }

    private void Start()
    {
        if (promptUI == null)
            promptUI = FindFirstObjectByType<InteractionPromptUI>();

        if (promptUI != null)
            promptUI.HideInstant();

        SubscribeLocalization();
    }

    private void OnDestroy()
    {
        UnsubscribeLocalization();
    }

    private void Update()
    {
        if (promptUI == null)
            return;

        if (TerminalUI.Instance != null && TerminalUI.Instance.IsOpen)
        {
            promptUI.Hide();
            return;
        }

        IInteractable interactable = interactionDetector.GetCurrentInteractable();

        if (interactable == null)
        {
            promptUI.Hide();
            return;
        }

        if (!interactable.CanInteract)
        {
            promptUI.Hide();
            return;
        }

        string text = BuildPromptText(interactable);
        promptUI.Show(text);
    }

    private string BuildPromptText(IInteractable interactable)
    {
        InteractionTextData textData = GetInteractionTextData();

        string actionText = string.IsNullOrWhiteSpace(interactable.InteractionText)
            ? GetFallbackActionText(textData)
            : interactable.InteractionText;

        if (textData == null)
        {
            if (interactable.InteractionType == InteractionType.Hold)
            {
                int progressPercent = Mathf.RoundToInt(interactable.HoldProgress * 100f);

                if (progressPercent > 0)
                    return $"[Hold {keyLabel}] {actionText} {progressPercent}%";

                return $"[Hold {keyLabel}] {actionText}";
            }

            return $"[{keyLabel}] {actionText}";
        }

        if (interactable.InteractionType == InteractionType.Hold)
            return textData.GetHoldPrompt(keyLabel, actionText, interactable.HoldProgress);

        return textData.GetPressPrompt(keyLabel, actionText);
    }

    private string GetFallbackActionText(InteractionTextData textData)
    {
        if (textData == null)
            return "Interact";

        return textData.GetFallbackActionText();
    }

    private InteractionTextData GetInteractionTextData()
    {
        if (NeraContentProvider.Instance == null)
            return null;

        if (NeraContentProvider.Instance.ContentDatabase == null)
            return null;

        return NeraContentProvider.Instance.ContentDatabase.InteractionTextData;
    }

    private void SubscribeLocalization()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void UnsubscribeLocalization()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(GameLanguage language)
    {
        if (promptUI == null)
            return;

        IInteractable interactable = interactionDetector.GetCurrentInteractable();

        if (interactable == null || !interactable.CanInteract)
        {
            promptUI.Hide();
            return;
        }

        promptUI.Show(BuildPromptText(interactable));
    }
}