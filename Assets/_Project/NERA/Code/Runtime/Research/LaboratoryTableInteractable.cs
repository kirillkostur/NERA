using NERA.Inventory;
using NERA.Interaction;
using NERA.Energy;
using NERA.Station;
using UnityEngine;

namespace NERA.Research
{
    [RequireComponent(typeof(StationObjectIdentity))]
    public sealed class LaboratoryTableInteractable : BaseInteractable
    {
        private const string LaboratoryObjectId = "station_laboratory";

        [Header("Required Action")]
        [Tooltip("How long E must be held to start a disabled laboratory.")]
        [SerializeField, Min(0.1f)]
        private float requiredActionHoldDuration = 1f;

        [SerializeField] private StationObjectIdentity identity;

        private void Awake()
        {
            ResolveIdentity();
            SetActionText("Use Laboratory");
        }

        public override InteractionPrompt GetPrompt()
        {
            InteractionPrompt configured = base.GetPrompt();
            StationSystemsController systems =
                StationSystemsController.Instance;
            string objectId = ResolveObjectId();
            if (systems != null &&
                !systems.IsRequestedActive(
                    StationSystemType.Laboratory,
                    objectId))
            {
                StationSystemDefinition definition = systems.GetDefinition(
                    StationSystemType.Laboratory,
                    objectId);
                string reason = "Station systems are unavailable";
                bool canStart = definition?.Controllable == true &&
                    systems.CanStart(
                        StationSystemType.Laboratory,
                        objectId,
                        out reason);

                return new InteractionPrompt(
                    "Start Laboratory",
                    InteractionMode.Hold,
                    RequiredActionHoldDuration,
                    canStart,
                    reason);
            }

            ResearchController research = ResearchController.Instance;
            bool hasPower = research != null
                ? research.HasOperationalPower &&
                  (systems == null || systems.IsRequestedActive(
                      StationSystemType.Laboratory,
                      objectId))
                : EnergySystemController.Instance != null &&
                  EnergySystemController.Instance.HasUsablePower &&
                  EnergySystemController.Instance.State != EnergyState.Emergency;

            return new InteractionPrompt(
                configured.ActionText,
                InteractionMode.Press,
                0f,
                configured.IsAvailable && hasPower,
                configured.IsAvailable
                    ? hasPower
                        ? string.Empty
                        : "Laboratory has no power"
                    : configured.UnavailableReason
            );
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            if (!GetPrompt().IsAvailable)
                return;

            StationSystemsController systems =
                StationSystemsController.Instance;
            string objectId = ResolveObjectId();
            if (systems != null &&
                !systems.IsRequestedActive(
                    StationSystemType.Laboratory,
                    objectId))
            {
                if (systems.SetRequestedActive(
                        StationSystemType.Laboratory,
                        true,
                        objectId))
                {
                    base.CompleteInteraction(interactor);
                }
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

        private float RequiredActionHoldDuration =>
            Mathf.Max(0.1f, requiredActionHoldDuration);

        private string ResolveObjectId()
        {
            ResolveIdentity();
            return identity != null &&
                !string.IsNullOrWhiteSpace(identity.ObjectId)
                    ? identity.ObjectId
                    : LaboratoryObjectId;
        }

        private void ResolveIdentity()
        {
            identity ??=
                GetComponentInParent<StationObjectIdentity>(true);
        }

        private void OnValidate()
        {
            requiredActionHoldDuration = RequiredActionHoldDuration;
            ResolveIdentity();
        }
    }
}
