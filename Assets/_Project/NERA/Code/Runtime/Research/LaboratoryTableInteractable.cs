using NERA.Inventory;
using NERA.Interaction;
using NERA.Energy;
using NERA.Station;
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
            StationSystemsController systems =
                StationSystemsController.Instance;
            if (systems != null &&
                !systems.IsRequestedActive(StationSystemType.Laboratory))
            {
                bool canStart = systems.CanStart(
                    StationSystemType.Laboratory,
                    out string reason);
                return new InteractionPrompt(
                    "Start Laboratory",
                    prompt.Mode,
                    prompt.HoldDuration,
                    canStart,
                    reason);
            }

            ResearchController research = ResearchController.Instance;
            bool hasPower = research != null
                ? research.HasOperationalPower &&
                  (systems == null || systems.IsRequestedActive(
                      StationSystemType.Laboratory))
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

            StationSystemsController systems =
                StationSystemsController.Instance;
            if (systems != null &&
                !systems.IsRequestedActive(StationSystemType.Laboratory) &&
                !systems.SetRequestedActive(
                    StationSystemType.Laboratory,
                    true))
            {
                return;
            }

            InventoryLabHUDController hud = InventoryLabHUDController.Instance;
            if (hud == null)
            {
                Debug.LogError(
                    "Laboratory HUD was not found. Start gameplay through MainScene.",
                    this
                );
                return;
            }

            hud.OpenLaboratory(interactor);
            base.CompleteInteraction(interactor);
        }
    }
}
