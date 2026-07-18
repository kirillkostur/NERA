using NERA.Inventory;
using NERA.Interaction;
using NERA.Energy;
using UnityEngine;

namespace NERA.Research
{
    public sealed class LaboratoryTableInteractable : BaseInteractable
    {
        private void Awake()
        {
            SetActionText("Use Laboratory");
        }

        public override InteractionPrompt GetPrompt()
        {
            InteractionPrompt prompt = base.GetPrompt();
            ResearchController research = ResearchController.Instance;
            bool hasPower = research != null
                ? research.HasOperationalPower
                : EnergySystemController.Instance != null &&
                  EnergySystemController.Instance.HasUsablePower &&
                  EnergySystemController.Instance.State != EnergyState.Emergency;

            return new InteractionPrompt(
                prompt.ActionText,
                prompt.Mode,
                prompt.HoldDuration,
                hasPower,
                "Laboratory has no power"
            );
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            if (!GetPrompt().IsAvailable)
                return;

            InventoryLabHUDController hud = InventoryLabHUDController.Instance;
            if (hud == null)
            {
                Debug.LogError(
                    "Laboratory HUD was not found. Start the game through the Boot scene.",
                    this
                );
                return;
            }

            hud.OpenLaboratory(interactor);
            base.CompleteInteraction(interactor);
        }
    }
}
