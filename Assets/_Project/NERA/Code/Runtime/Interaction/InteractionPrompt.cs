using NERA.Localization;

namespace NERA.Interaction
{
    public readonly struct InteractionPrompt
    {
        public InteractionPrompt(
            string actionText,
            InteractionMode mode,
            float holdDuration,
            bool isAvailable,
            string unavailableReason,
            bool isVisible = true)
        {
            ActionText = LocalizePromptValue("action", actionText);
            Mode = mode;
            HoldDuration = holdDuration;
            IsAvailable = isAvailable;
            UnavailableReason = LocalizePromptValue(
                "unavailable", unavailableReason);
            IsVisible = isVisible;
        }

        public string ActionText { get; }
        public InteractionMode Mode { get; }
        public float HoldDuration { get; }
        public bool IsAvailable { get; }
        public string UnavailableReason { get; }
        public bool IsVisible { get; }

        private static string LocalizePromptValue(string group, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return NERALocalization.Get(
                NERALocalization.HudTable,
                $"interaction.{group}." +
                NERALocalization.NormalizeKeyPart(value),
                value);
        }
    }
}
