using System;
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

            string localized = NERALocalization.Get(
                NERALocalization.HudTable,
                $"interaction.{group}." +
                NERALocalization.NormalizeKeyPart(value),
                value);
            if (!string.Equals(localized, value, StringComparison.Ordinal))
                return localized;

            if (string.Equals(group, "action", StringComparison.Ordinal))
            {
                if (TryLocalizeSuffix(
                        value,
                        "Start ",
                        "interaction.action.start_object",
                        "Start {0}",
                        out localized) ||
                    TryLocalizeSuffix(
                        value,
                        "Configure ",
                        "interaction.action.configure_object",
                        "Configure {0}",
                        out localized))
                {
                    return localized;
                }
            }

            const string chargePrefix = "Battery charge below ";
            const string chargeSuffix = "%.";
            if (string.Equals(group, "unavailable", StringComparison.Ordinal) &&
                value.StartsWith(chargePrefix, StringComparison.Ordinal) &&
                value.EndsWith(chargeSuffix, StringComparison.Ordinal))
            {
                string percentage = value.Substring(
                    chargePrefix.Length,
                    value.Length - chargePrefix.Length - chargeSuffix.Length);
                return NERALocalization.Get(
                    NERALocalization.HudTable,
                    "interaction.unavailable.battery_charge_below",
                    "Battery charge below {0}%.",
                    percentage);
            }

            return localized;
        }

        private static bool TryLocalizeSuffix(
            string value,
            string prefix,
            string key,
            string fallback,
            out string localized)
        {
            localized = value;
            if (!value.StartsWith(prefix, StringComparison.Ordinal) ||
                value.Length <= prefix.Length)
            {
                return false;
            }

            localized = NERALocalization.Get(
                NERALocalization.HudTable,
                key,
                fallback,
                value.Substring(prefix.Length));
            return true;
        }
    }
}
