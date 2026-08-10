using System.Collections.Generic;
using NERA.Interaction;
using NERA.Items;
using NERA.Maintenance;
using Unity.Cinemachine;
using UnityEngine;

namespace NERA.Station
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StationObjectIdentity))]
    [RequireComponent(typeof(StationObjectVisual))]
    public sealed class StationUpgradeableObject : BaseInteractable
    {
        private enum RequiredInteraction
        {
            Upgrade,
            Service,
            Start,
            RestorePower
        }

        [SerializeField] private StationObjectIdentity identity;
        [SerializeField] private StationObjectVisual visual;
        [SerializeField] private CinemachineVirtualCameraBase upgradeCamera;
        [SerializeField] private MaintainableObject maintenance;

        [Header("Required Action")]
        [Tooltip("How long E must be held to service, start, or restore power. Upgrade mode always opens with a short press.")]
        [SerializeField, Min(0.1f)] private float requiredActionHoldDuration = 1f;

        public StationObjectIdentity Identity => identity;
        public CinemachineVirtualCameraBase UpgradeCamera => upgradeCamera;
        public IReadOnlyList<StationUpgradeSlot> Slots => visual?.Slots;
        public StationSystemType SystemType => identity != null
            ? identity.SystemType
            : default;
        public string ObjectId => identity != null
            ? identity.ObjectId
            : string.Empty;

        private void Awake()
        {
            ResolveReferences();
            SetActionText("Configure Upgrades");
        }

        public override InteractionPrompt GetPrompt()
        {
            ResolveReferences();
            InteractionPrompt configured = base.GetPrompt();
            RequiredInteraction required = ResolveRequiredInteraction(
                out bool actionAvailable,
                out string unavailableReason);

            if (required == RequiredInteraction.Service)
            {
                return new InteractionPrompt(
                    maintenance.ServiceActionText,
                    InteractionMode.Hold,
                    RequiredActionHoldDuration,
                    configured.IsAvailable,
                    configured.UnavailableReason);
            }

            if (required == RequiredInteraction.RestorePower)
            {
                return new InteractionPrompt(
                    "Restore Power",
                    InteractionMode.Hold,
                    RequiredActionHoldDuration,
                    actionAvailable,
                    unavailableReason);
            }

            if (required == RequiredInteraction.Start)
            {
                return new InteractionPrompt(
                    $"Start {identity?.DisplayName ?? name}",
                    InteractionMode.Hold,
                    RequiredActionHoldDuration,
                    actionAvailable,
                    unavailableReason);
            }

            bool available = StationUpgradeModeController.Instance == null ||
                !StationUpgradeModeController.Instance.IsOpen;
            return new InteractionPrompt(
                $"Configure {identity?.DisplayName ?? name}",
                InteractionMode.Press,
                0f,
                available,
                available ? string.Empty : "Upgrade mode is already open");
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            ResolveReferences();
            RequiredInteraction required = ResolveRequiredInteraction(
                out bool actionAvailable,
                out _);
            if (!actionAvailable)
                return;

            if (required == RequiredInteraction.Service)
            {
                if (maintenance != null && maintenance.Service())
                    base.CompleteInteraction(interactor);
                return;
            }

            StationSystemsController systems =
                StationSystemsController.Instance;
            if (required == RequiredInteraction.RestorePower)
            {
                StationPowerController power = StationPowerController.Instance;
                if (power == null ||
                    !power.IsPowered && !power.RestorePower())
                {
                    return;
                }

                if (systems?.SetCriticalSystemActive(
                        StationSystemType.Battery,
                        true,
                        true) == true)
                {
                    base.CompleteInteraction(interactor);
                }
                return;
            }

            if (required == RequiredInteraction.Start)
            {
                if (systems?.SetRequestedActive(
                        SystemType,
                        true,
                        ObjectId) == true)
                {
                    base.CompleteInteraction(interactor);
                }
                return;
            }

            StationUpgradeModeController controller =
                StationUpgradeModeController.GetOrCreate();
            if (controller == null || !controller.Open(this, interactor))
                return;
            base.CompleteInteraction(interactor);
        }

        public StationUpgradeSlot FindSlot(string slotId)
        {
            return visual?.FindSlot(slotId);
        }

        public void RefreshVisuals()
        {
            visual?.Refresh();
        }

        public void SetUpgradeVisualsVisible(bool visible)
        {
            visual?.SetUpgradeModeActive(visible);
        }

        public void ShowStaged(StationUpgradeSlot slot, ItemData item)
        {
            visual?.ShowPart(slot, item);
        }

        public void RestoreSlot(StationUpgradeSlot slot)
        {
            visual?.RestoreSlot(slot);
        }

        private void ResolveReferences()
        {
            identity ??= GetComponent<StationObjectIdentity>();
            visual ??= GetComponent<StationObjectVisual>();
            maintenance ??= GetComponent<MaintainableObject>();
            upgradeCamera ??=
                GetComponentInChildren<CinemachineVirtualCameraBase>(true);
        }

        private float RequiredActionHoldDuration =>
            Mathf.Max(0.1f, requiredActionHoldDuration);

        private RequiredInteraction ResolveRequiredInteraction(
            out bool available,
            out string unavailableReason)
        {
            if (maintenance != null && maintenance.NeedsService)
            {
                available = true;
                unavailableReason = string.Empty;
                return RequiredInteraction.Service;
            }

            StationSystemsController systems =
                StationSystemsController.Instance;
            StationSystemDefinition definition = identity?.ResolveDefinition(
                systems?.Config);
            if (SystemType == StationSystemType.Battery)
            {
                StationPowerController power = StationPowerController.Instance;
                bool batteryRequested = systems != null
                    ? systems.IsRequestedActive(SystemType, ObjectId)
                    : definition?.InitiallyActive ?? false;
                if (power == null || !power.IsPowered || !batteryRequested)
                {
                    available = power != null;
                    unavailableReason = available
                        ? string.Empty
                        : "Station power controller is unavailable";
                    return RequiredInteraction.RestorePower;
                }
            }

            if (definition?.Controllable == true)
            {
                bool requestedActive = systems != null
                    ? systems.IsRequestedActive(SystemType, ObjectId)
                    : definition.InitiallyActive;
                string startReason = "Station systems are unavailable";
                bool canRun = systems != null && systems.CanStart(
                    SystemType,
                    ObjectId,
                    out startReason);
                if (!requestedActive || !canRun)
                {
                    available = canRun;
                    unavailableReason = canRun
                        ? string.Empty
                        : systems != null
                            ? startReason
                            : "Station systems are unavailable";
                    return RequiredInteraction.Start;
                }
            }

            available = true;
            unavailableReason = string.Empty;
            return RequiredInteraction.Upgrade;
        }

        private void OnValidate()
        {
            requiredActionHoldDuration = RequiredActionHoldDuration;
            ResolveReferences();
        }
    }
}
