using UnityEngine;

[CreateAssetMenu(
    fileName = "InteractionTextData_Default",
    menuName = "NERA/UI/Interaction Text Data"
)]
public class InteractionTextData : ScriptableObject
{
    [Header("Prompt Formats")]
    [SerializeField] private LocalizedText pressPromptFormat;
    [SerializeField] private LocalizedText holdPromptFormat;

    [Header("Fallback")]
    [SerializeField] private LocalizedText fallbackActionText;

    public string GetFallbackActionText()
    {
        return fallbackActionText.GetText("Interact");
    }

    public string GetPressPrompt(string keyLabel, string actionText)
    {
        string format = pressPromptFormat.GetText("[{0}] {1}");
        return string.Format(format, keyLabel, actionText);
    }

    public string GetHoldPrompt(string keyLabel, string actionText, float progress)
    {
        string format = holdPromptFormat.GetText("[Hold {0}] {1}");

        string prompt = string.Format(format, keyLabel, actionText);

        if (progress > 0f)
        {
            int progressPercent = Mathf.RoundToInt(progress * 100f);
            prompt += $" {progressPercent}%";
        }

        return prompt;
    }
}