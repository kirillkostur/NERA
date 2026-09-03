using System.Collections;
using System.Collections.Generic;
using Climbing;
using NERA.Combat;
using NERA.Energy;
using NERA.Interaction;
using NERA.Localization;
using NERA.Player;
using UnityEngine;

namespace NERA.Station
{
    /// <summary>
    /// Config-driven medical platform. The first hold starts an offline module;
    /// subsequent holds spend one treatment charge and lock the player into an
    /// uninterruptible walk-and-treatment sequence.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(StationObjectIdentity))]
    public sealed class MedicalModuleController : BaseInteractable
    {
        public const string DefaultObjectId = "station_medical_module";
        public const string QuestMarkerId = "quest_medical_module_01";
        private const string StartActionLocalizationKey =
            "interaction.action.start_medical_module";
        private const string SystemsUnavailableLocalizationKey =
            "interaction.unavailable.station_systems_are_unavailable";
        private const string TreatmentInUseLocalizationKey =
            "interaction.unavailable.medical_module_is_in_use.";
        private const string HealthUnavailableLocalizationKey =
            "interaction.unavailable.player_health_is_unavailable.";
        private const string HealthFullLocalizationKey =
            "interaction.unavailable.health_is_already_full.";
        private const string EnergyUnavailableLocalizationKey =
            "interaction.unavailable.not_enough_energy.";

        [Header("Medical Module")]
        [SerializeField] private StationObjectIdentity identity;
        [SerializeField] private Transform treatmentPoint;

        [Header("Required Action")]
        [Tooltip("How long E must be held to start the medical module.")]
        [SerializeField, Min(0.1f)] private float requiredActionHoldDuration = 1f;

        [Header("Scripted Entry")]
        [SerializeField, Min(0.1f)] private float entryDuration = 1.25f;
        [SerializeField] private string walkAnimationState = "Walk";
        [SerializeField] private string idleAnimationState = "Idle";
        [SerializeField] private bool hideAllCanvases = true;

        private readonly List<Canvas> hiddenCanvases = new List<Canvas>();
        private EnergySystemController boundEnergy;
        private StationSystemsController boundSystems;
        private PlayerHealth cachedPlayerHealth;
        private ParkourPlayerBridge lockedBridge;
        private ThirdPersonController lockedParkourController;
        private Rigidbody lockedBody;
        private Animator lockedAnimator;
        private bool bodyWasKinematic;
        private bool bodyUsedGravity;
        private bool bodyDetectedCollisions;
        private Coroutine treatmentRoutine;

        public bool IsTreating { get; private set; }
        public Transform TreatmentPoint => treatmentPoint;
        public string ObjectId => identity != null &&
            !string.IsNullOrWhiteSpace(identity.ObjectId)
                ? identity.ObjectId
                : DefaultObjectId;
        public float TreatmentEnergyCost => GetConfiguredStat(
            StationObjectStat.TreatmentEnergyCost,
            30f);
        public float TreatmentDuration => GetConfiguredStat(
            StationObjectStat.TreatmentDuration,
            10f);
        public float IdleEnergyConsumption => GetConfiguredStat(
            StationObjectStat.IdleEnergyConsumption,
            0f);
        public float RequiredActionHoldDuration =>
            Mathf.Max(0.1f, requiredActionHoldDuration);

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            EnergySystemController.InstanceChanged +=
                HandleEnergyInstanceChanged;
            StationSystemsController.InstanceChanged +=
                HandleSystemsInstanceChanged;
            BindSystems(StationSystemsController.Instance);
            BindEnergy(EnergySystemController.Instance);
        }

        public override InteractionPrompt GetPrompt()
        {
            InteractionPrompt configured = base.GetPrompt();
            ResolveReferences();
            EnsureEnergyRegistration();

            if (IsTreating)
            {
                return CreatePrompt(
                    configured,
                    configured.ActionText,
                    false,
                    LocalizePrompt(
                        TreatmentInUseLocalizationKey,
                        configured.UnavailableReason),
                    false);
            }

            StationSystemsController systems =
                StationSystemsController.Instance;
            StationSystemDefinition definition = GetDefinition(systems);
            if (systems == null || definition == null)
            {
                return CreatePrompt(
                    configured,
                    LocalizePrompt(
                        StartActionLocalizationKey,
                        configured.ActionText),
                    false,
                    LocalizePrompt(
                        SystemsUnavailableLocalizationKey,
                        configured.UnavailableReason),
                    useRequiredAction: true);
            }

            bool requestedActive = systems.IsRequestedActive(
                StationSystemType.MedicalModule,
                ObjectId);
            bool canStart = systems.CanStart(
                StationSystemType.MedicalModule,
                ObjectId,
                out string startReason);
            if (!requestedActive || !canStart)
            {
                return CreatePrompt(
                    configured,
                    LocalizePrompt(
                        StartActionLocalizationKey,
                        configured.ActionText),
                    !requestedActive && canStart,
                    canStart ? string.Empty : startReason,
                    useRequiredAction: true);
            }

            PlayerHealth health = ResolvePlayerHealth(null);
            if (health == null || !health.IsAlive)
            {
                return CreatePrompt(
                    configured,
                    configured.ActionText,
                    false,
                    LocalizePrompt(
                        HealthUnavailableLocalizationKey,
                        configured.UnavailableReason));
            }
            if (health.CurrentHealth >= health.MaxHealth - 0.001f)
            {
                return CreatePrompt(
                    configured,
                    configured.ActionText,
                    false,
                    LocalizePrompt(
                        HealthFullLocalizationKey,
                        configured.UnavailableReason));
            }
            if (boundEnergy == null ||
                !boundEnergy.CanSpendConsumerEnergy(
                    ObjectId,
                    TreatmentEnergyCost))
            {
                return CreatePrompt(
                    configured,
                    configured.ActionText,
                    false,
                    LocalizePrompt(
                        EnergyUnavailableLocalizationKey,
                        configured.UnavailableReason));
            }

            return CreatePrompt(
                configured,
                configured.ActionText,
                true,
                string.Empty);
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            if (IsTreating)
                return;

            ResolveReferences();
            StationSystemsController systems =
                StationSystemsController.Instance;
            if (systems == null || GetDefinition(systems) == null)
                return;

            bool requestedActive = systems.IsRequestedActive(
                StationSystemType.MedicalModule,
                ObjectId);
            if (!requestedActive)
            {
                if (systems.SetRequestedActive(
                        StationSystemType.MedicalModule,
                        true,
                        ObjectId))
                {
                    RefreshPowerRequest();
                    base.CompleteInteraction(interactor);
                }
                return;
            }

            if (!systems.CanStart(
                    StationSystemType.MedicalModule,
                    ObjectId,
                    out _))
            {
                return;
            }

            PlayerHealth health = ResolvePlayerHealth(interactor);
            if (health == null || !health.IsAlive ||
                health.CurrentHealth >= health.MaxHealth - 0.001f)
            {
                return;
            }

            EnsureEnergyRegistration();
            if (boundEnergy == null ||
                !boundEnergy.TrySpendConsumerEnergy(
                    ObjectId,
                    TreatmentEnergyCost))
            {
                return;
            }

            IsTreating = true;
            treatmentRoutine = StartCoroutine(
                RunTreatment(interactor, health));
        }

        private IEnumerator RunTreatment(
            GameObject interactor,
            PlayerHealth health)
        {
            AcquirePresentationLock(health);
            PlayAnimation(walkAnimationState, 1f, false);

            Transform playerTransform = lockedBridge != null
                ? lockedBridge.transform
                : health.transform;
            Vector3 startPosition = playerTransform.position;
            Quaternion startRotation = playerTransform.rotation;
            Vector3 destination = treatmentPoint != null
                ? treatmentPoint.position
                : transform.position;
            Vector3 movementDirection = destination - startPosition;
            movementDirection.y = 0f;
            Quaternion movementRotation =
                movementDirection.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(
                        movementDirection.normalized,
                        Vector3.up)
                    : startRotation;

            float elapsed = 0f;
            float duration = Mathf.Max(0.1f, entryDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(elapsed / duration));
                SetPlayerPose(
                    playerTransform,
                    Vector3.Lerp(startPosition, destination, progress),
                    Quaternion.Slerp(
                        startRotation,
                        movementRotation,
                        progress));
                if (lockedAnimator != null)
                    lockedAnimator.SetFloat("Velocity", 1f);
                yield return null;
            }

            SetPlayerPose(
                playerTransform,
                destination,
                movementRotation);
            PlayAnimation(idleAnimationState, 0f, true);

            elapsed = 0f;
            duration = TreatmentDuration;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetPlayerPose(
                    playerTransform,
                    destination,
                    movementRotation);
                yield return null;
            }

            SetPlayerPose(
                playerTransform,
                destination,
                movementRotation);

            if (health != null && health.IsAlive)
                health.RestoreFullHealth();

            base.CompleteInteraction(interactor);
            FinishTreatmentLock();
            IsTreating = false;
            treatmentRoutine = null;
        }

        private void AcquirePresentationLock(PlayerHealth health)
        {
            lockedBridge = health.GetComponent<ParkourPlayerBridge>();
            lockedParkourController =
                health.GetComponent<ThirdPersonController>();
            lockedBody = lockedBridge != null
                ? lockedBridge.LocomotionBody
                : health.GetComponent<Rigidbody>();
            AnimationCharacterController animation =
                health.GetComponent<AnimationCharacterController>();
            lockedAnimator = animation != null
                ? animation.animator
                : health.GetComponentInChildren<Animator>();

            lockedBridge?.SetInputEnabled(this, false);
            if (lockedParkourController != null)
            {
                lockedParkourController.DisableController();
            }
            else if (lockedBody != null)
            {
                bodyWasKinematic = lockedBody.isKinematic;
                bodyUsedGravity = lockedBody.useGravity;
                bodyDetectedCollisions = lockedBody.detectCollisions;
                if (!lockedBody.isKinematic)
                {
                    lockedBody.linearVelocity = Vector3.zero;
                    lockedBody.angularVelocity = Vector3.zero;
                }
                lockedBody.isKinematic = true;
            }

            if (!hideAllCanvases)
                return;

            hiddenCanvases.Clear();
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (Canvas canvas in canvases)
            {
                if (canvas == null || !canvas.enabled)
                    continue;
                hiddenCanvases.Add(canvas);
                canvas.enabled = false;
            }
        }

        private void FinishTreatmentLock()
        {
            foreach (Canvas canvas in hiddenCanvases)
            {
                if (canvas != null)
                    canvas.enabled = true;
            }
            hiddenCanvases.Clear();

            if (lockedParkourController != null &&
                (lockedBridge == null || !lockedBridge.IsDead))
            {
                lockedParkourController.EnableController();
            }
            else if (lockedBody != null)
            {
                lockedBody.isKinematic = bodyWasKinematic;
                lockedBody.useGravity = bodyUsedGravity;
                lockedBody.detectCollisions = bodyDetectedCollisions;
            }

            lockedBridge?.SetInputEnabled(this, true);
            lockedBridge = null;
            lockedParkourController = null;
            lockedBody = null;
            lockedAnimator = null;
        }

        private void SetPlayerPose(
            Transform playerTransform,
            Vector3 position,
            Quaternion rotation)
        {
            if (lockedBody != null)
            {
                lockedBody.position = position;
                lockedBody.rotation = rotation;
            }
            else if (playerTransform != null)
            {
                playerTransform.SetPositionAndRotation(position, rotation);
            }
            Physics.SyncTransforms();
        }

        private void PlayAnimation(
            string stateName,
            float velocity,
            bool released)
        {
            if (lockedAnimator == null)
                return;
            lockedAnimator.SetFloat("Velocity", velocity);
            lockedAnimator.SetBool("Run", false);
            lockedAnimator.SetBool("Released", released);
            if (!string.IsNullOrWhiteSpace(stateName))
                lockedAnimator.CrossFade(stateName.Trim(), 0.1f);
        }

        private void ResolveReferences()
        {
            identity ??= GetComponent<StationObjectIdentity>();
            treatmentPoint ??= transform.Find("TreatmentPoint");
        }

        private PlayerHealth ResolvePlayerHealth(GameObject interactor)
        {
            PlayerHealth health = interactor != null
                ? interactor.GetComponent<PlayerHealth>() ??
                  interactor.GetComponentInParent<PlayerHealth>()
                : null;
            if (health != null)
                cachedPlayerHealth = health;
            if (cachedPlayerHealth == null)
                cachedPlayerHealth = Object.FindFirstObjectByType<PlayerHealth>();
            return cachedPlayerHealth;
        }

        private StationSystemDefinition GetDefinition(
            StationSystemsController systems)
        {
            return systems?.GetDefinition(
                    StationSystemType.MedicalModule,
                    ObjectId) ??
                StationSystemsConfig.LoadDefault()?.Find(
                    StationSystemType.MedicalModule,
                    ObjectId);
        }

        private float GetConfiguredStat(
            StationObjectStat stat,
            float fallback)
        {
            return StationSystemsConfig.GetEffectiveStat(
                StationSystemType.MedicalModule,
                ObjectId,
                stat,
                fallback);
        }

        private void EnsureEnergyRegistration()
        {
            BindEnergy(EnergySystemController.Instance);
            if (boundEnergy == null)
                return;

            boundEnergy.RegisterConsumer(
                ObjectId,
                IdleEnergyConsumption,
                boundEnergy.Config.GetMinimumCharge01(
                    StationSystemType.MedicalModule,
                    ObjectId),
                StationSystemType.MedicalModule,
                ObjectId);
            RefreshPowerRequest();
        }

        private void RefreshPowerRequest()
        {
            if (boundEnergy == null)
                return;
            StationSystemDefinition definition = GetDefinition(boundSystems);
            bool requested = boundSystems != null
                ? boundSystems.IsRequestedActive(
                    StationSystemType.MedicalModule,
                    ObjectId)
                : definition?.InitiallyActive == true;
            boundEnergy.SetConsumerActive(ObjectId, requested);
        }

        private void HandleEnergyInstanceChanged(
            EnergySystemController energy)
        {
            BindEnergy(energy);
        }

        private void BindEnergy(EnergySystemController energy)
        {
            if (boundEnergy == energy)
                return;
            if (boundEnergy != null)
                boundEnergy.UnregisterConsumer(ObjectId);
            boundEnergy = energy;
            if (boundEnergy != null)
            {
                boundEnergy.RegisterConsumer(
                    ObjectId,
                    IdleEnergyConsumption,
                    boundEnergy.Config.GetMinimumCharge01(
                        StationSystemType.MedicalModule,
                        ObjectId),
                    StationSystemType.MedicalModule,
                    ObjectId);
                RefreshPowerRequest();
            }
        }

        private void HandleSystemsInstanceChanged(
            StationSystemsController systems)
        {
            BindSystems(systems);
        }

        private void BindSystems(StationSystemsController systems)
        {
            if (boundSystems == systems)
                return;
            if (boundSystems != null)
                boundSystems.SystemsChanged -= RefreshPowerRequest;
            boundSystems = systems;
            if (boundSystems != null)
                boundSystems.SystemsChanged += RefreshPowerRequest;
            RefreshPowerRequest();
        }

        private InteractionPrompt CreatePrompt(
            InteractionPrompt configured,
            string action,
            bool available,
            string reason,
            bool visible = true,
            bool useRequiredAction = false)
        {
            return new InteractionPrompt(
                action,
                useRequiredAction
                    ? NERA.Interaction.InteractionMode.Hold
                    : configured.Mode,
                useRequiredAction
                    ? RequiredActionHoldDuration
                    : configured.HoldDuration,
                available,
                reason ?? string.Empty,
                visible);
        }

        private static string LocalizePrompt(string key, string fallback)
        {
            return NERALocalization.Get(
                NERALocalization.HudTable,
                key,
                fallback);
        }

        private void OnDisable()
        {
            EnergySystemController.InstanceChanged -=
                HandleEnergyInstanceChanged;
            StationSystemsController.InstanceChanged -=
                HandleSystemsInstanceChanged;
            if (boundSystems != null)
                boundSystems.SystemsChanged -= RefreshPowerRequest;
            if (boundEnergy != null)
                boundEnergy.UnregisterConsumer(ObjectId);
            boundSystems = null;
            boundEnergy = null;

            if (treatmentRoutine != null)
                StopCoroutine(treatmentRoutine);
            treatmentRoutine = null;
            if (IsTreating)
                FinishTreatmentLock();
            IsTreating = false;
        }

        private void OnValidate()
        {
            entryDuration = Mathf.Max(0.1f, entryDuration);
            requiredActionHoldDuration = RequiredActionHoldDuration;
            ResolveReferences();
        }
    }
}
