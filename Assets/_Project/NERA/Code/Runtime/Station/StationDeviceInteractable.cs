using NERA.Interaction;
using NERA.Maintenance;
using UnityEngine;

namespace NERA.Station
{
    /// <summary>
    /// Single physical interaction point for a station device. Maintenance
    /// state stays in MaintainableObject; requested on/off state stays in
    /// StationSystemsController, shared with the station terminal.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MaintainableObject))]
    public sealed class StationDeviceInteractable : BaseInteractable
    {
        [SerializeField] private MaintainableObject maintenance;

        private StationObjectIdentity identity;

        public StationSystemType SystemType => ResolveSystemType();
        public string ObjectId => ResolveObjectId();
        public MaintainableObject Maintenance => maintenance;

        private void Awake()
        {
            CacheIdentity();
            CacheMaintenance();
        }

        public override InteractionPrompt GetPrompt()
        {
            InteractionPrompt configured = base.GetPrompt();
            CacheMaintenance();

            if (maintenance != null && maintenance.IsCleaning)
                return Hidden(configured);

            if (maintenance != null && maintenance.NeedsService)
            {
                return new InteractionPrompt(
                    maintenance.ServiceActionText,
                    configured.Mode,
                    configured.HoldDuration,
                    configured.IsAvailable,
                    configured.UnavailableReason);
            }

            StationSystemsController systems =
                StationSystemsController.Instance;
            string resolvedObjectId = ResolveObjectId();
            StationSystemDefinition definition = GetDefinition(
                systems,
                resolvedObjectId);
            if (definition == null || !definition.Controllable)
                return Hidden(configured);

            bool requestedActive = systems != null
                ? systems.IsRequestedActive(
                    ResolveSystemType(),
                    resolvedObjectId)
                : definition.InitiallyActive;
            bool canRun = systems != null && systems.CanStart(
                ResolveSystemType(),
                resolvedObjectId,
                out _);

            // Requested devices resume automatically after power returns.
            // While they cannot run, interaction remains visible so the
            // player may attempt a manual start without changing the toggle.
            if (requestedActive && canRun)
                return Hidden(configured);

            return configured;
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            CacheMaintenance();
            if (maintenance != null && maintenance.IsCleaning)
                return;

            if (maintenance != null && maintenance.NeedsService)
            {
                if (maintenance.Service())
                    base.CompleteInteraction(interactor);
                return;
            }

            StationSystemsController systems =
                StationSystemsController.Instance;
            if (systems == null)
                return;

            string resolvedObjectId = ResolveObjectId();
            StationSystemDefinition definition = GetDefinition(
                systems,
                resolvedObjectId);
            if (definition == null || !definition.Controllable)
                return;

            if (!systems.SetRequestedActive(
                    ResolveSystemType(),
                    true,
                    resolvedObjectId))
            {
                return;
            }

            base.CompleteInteraction(interactor);
        }

        private void CacheMaintenance()
        {
            if (maintenance == null)
                maintenance = GetComponent<MaintainableObject>();
        }

        private string ResolveObjectId()
        {
            CacheIdentity();
            return identity != null ? identity.ObjectId : string.Empty;
        }

        private StationSystemType ResolveSystemType()
        {
            CacheIdentity();
            return identity != null ? identity.SystemType : default;
        }

        private StationSystemDefinition GetDefinition(
            StationSystemsController systems,
            string resolvedObjectId)
        {
            CacheIdentity();
            if (identity == null)
                return null;

            StationSystemsConfig config = systems != null
                ? systems.Config
                : StationSystemsConfig.LoadDefault();
            return config.Find(ResolveSystemType(), resolvedObjectId);
        }

        private static InteractionPrompt Hidden(InteractionPrompt configured)
        {
            return new InteractionPrompt(
                configured.ActionText,
                configured.Mode,
                configured.HoldDuration,
                false,
                string.Empty,
                false);
        }

        private void OnValidate()
        {
            CacheIdentity();
            CacheMaintenance();
        }

        private void CacheIdentity()
        {
            if (identity == null)
            {
                identity = GetComponentInParent<StationObjectIdentity>(true);
            }
        }
    }
}
