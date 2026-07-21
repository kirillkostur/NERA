using NERA.Interaction;
using NERA.Inventory;
using UnityEngine;

namespace NERA.Energy
{
    public sealed class ItemChargingTableInteractable : BaseInteractable
    {
        private void Awake()
        {
            SetActionText("Use Charging Table");
        }

        public override InteractionPrompt GetPrompt()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null || !energy.HasUsablePower)
            {
                return new InteractionPrompt(
                    "Use Charging Table",
                    InteractionMode.Press,
                    0f,
                    false,
                    "Charging Table Offline - Restore Power First"
                );
            }

            return base.GetPrompt();
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null || !energy.HasUsablePower)
                return;

            InventoryLabHUDController hud = InventoryLabHUDController.Instance;
            if (hud == null)
            {
                Debug.LogError(
                    "Inventory HUD was not found. Start the game through the Boot scene.",
                    this
                );
                return;
            }

            hud.OpenChargingTable(interactor);
            base.CompleteInteraction(interactor);
        }
    }
}
