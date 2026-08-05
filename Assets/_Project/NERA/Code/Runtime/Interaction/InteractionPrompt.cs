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
            ActionText = actionText;
            Mode = mode;
            HoldDuration = holdDuration;
            IsAvailable = isAvailable;
            UnavailableReason = unavailableReason;
            IsVisible = isVisible;
        }

        public string ActionText { get; }
        public InteractionMode Mode { get; }
        public float HoldDuration { get; }
        public bool IsAvailable { get; }
        public string UnavailableReason { get; }
        public bool IsVisible { get; }
    }
}
