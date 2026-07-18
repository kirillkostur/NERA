using NERA.Interaction;
using NERA.Station;
using UnityEngine;

namespace NERA.Terminal
{
    public sealed class TerminalAccessInteractable : BaseInteractable
    {
        public override InteractionPrompt GetPrompt()
        {
            StationPowerController power = StationPowerController.Instance;

            if (power == null || !power.IsPowered)
            {
                return new InteractionPrompt(
                    "Use Terminal",
                    InteractionMode.Press,
                    0f,
                    false,
                    "Terminal Offline — Restore Power First"
                );
            }

            return base.GetPrompt();
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            StationPowerController power = StationPowerController.Instance;

            if (power == null || !power.IsPowered)
                return;

            TerminalUIScreen screen = TerminalUIScreen.Instance;

            if (screen == null)
            {
                Debug.LogError("TerminalAccessInteractable: TerminalUIScreen is missing.", this);
                return;
            }

            base.CompleteInteraction(interactor);
            screen.Open();
        }
    }
}
