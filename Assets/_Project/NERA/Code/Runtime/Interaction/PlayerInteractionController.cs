using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PlayerInteractionController : MonoBehaviour
    {
        [Header("Proximity Detection")]
        [SerializeField] private Vector3 detectionOffset =
            new Vector3(0f, 0.9f, 0f);
        [SerializeField, Min(0.1f)] private float interactionDistance = 2.5f;
        [SerializeField, Min(0.1f)] private float releaseDistance = 2.8f;
        [Tooltip("Layers containing IInteractable colliders.")]
        [SerializeField] private LayerMask overlapMask =
            (1 << 6) | (1 << 7);
        [SerializeField] private QueryTriggerInteraction triggerInteraction =
            QueryTriggerInteraction.Collide;
        [Tooltip("Environment layers that can block proximity interaction. " +
                 "Facing direction is never required.")]
        [SerializeField] private LayerMask obstructionMask =
            (1 << 0) | (1 << 9) | (1 << 10) | (1 << 11) |
            (1 << 14) | (1 << 15);

        [Header("Input")]
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        private readonly Collider[] overlapBuffer = new Collider[128];
        private readonly RaycastHit[] obstructionBuffer = new RaycastHit[16];
        private readonly List<MonoBehaviour> componentBuffer =
            new List<MonoBehaviour>(16);
        private IInteractable currentInteractable;
        private IInteractable activeInteractable;
        private float holdElapsed;

        public event Action TargetChanged;
        public event Action InteractionStateChanged;

        public IInteractable CurrentInteractable => currentInteractable;
        public bool IsInteracting => activeInteractable != null;
        public float HoldProgress { get; private set; }

        private void OnValidate()
        {
            interactionDistance = Mathf.Max(0.1f, interactionDistance);
            releaseDistance = Mathf.Max(
                interactionDistance,
                releaseDistance);
        }

        private void Update()
        {
            DetectInteractable();
            ProcessInput();
        }

        private void OnDisable()
        {
            CancelActiveInteraction();
            SetCurrentInteractable(null);
        }

        private void DetectInteractable()
        {
            Vector3 origin = transform.TransformPoint(detectionOffset);
            int hitCount = Physics.OverlapSphereNonAlloc(
                origin,
                releaseDistance,
                overlapBuffer,
                overlapMask,
                triggerInteraction);

            IInteractable closestAvailable = null;
            IInteractable closestUnavailable = null;
            float closestAvailableDistance = float.PositiveInfinity;
            float closestUnavailableDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider candidateCollider = overlapBuffer[i];
                if (candidateCollider == null ||
                    candidateCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                IInteractable interactable =
                    FindInteractable(candidateCollider);
                if (interactable == null)
                    continue;

                InteractionPrompt prompt = interactable.GetPrompt();
                if (!prompt.IsVisible)
                    continue;

                if (!HasClearPath(
                        origin,
                        candidateCollider,
                        interactable))
                    continue;

                float allowedDistance =
                    ReferenceEquals(interactable, currentInteractable)
                        ? releaseDistance
                        : interactionDistance;
                float distance = Vector3.Distance(
                    origin,
                    candidateCollider.ClosestPoint(origin));
                if (distance > allowedDistance)
                    continue;

                if (prompt.IsAvailable)
                {
                    if (distance >= closestAvailableDistance)
                        continue;

                    closestAvailable = interactable;
                    closestAvailableDistance = distance;
                }
                else
                {
                    if (distance >= closestUnavailableDistance)
                        continue;

                    closestUnavailable = interactable;
                    closestUnavailableDistance = distance;
                }
            }

            // Prefer an object that can be used now. If there is none, keep
            // the nearest unavailable object selected so the HUD can explain
            // why it cannot yet be used (for example, missing station power).
            IInteractable detected =
                closestAvailable ?? closestUnavailable;

            if (ReferenceEquals(detected, currentInteractable))
                return;

            CancelActiveInteraction();
            SetCurrentInteractable(detected);
        }

        private bool HasClearPath(
            Vector3 origin,
            Collider targetCollider,
            IInteractable target)
        {
            if (obstructionMask.value == 0)
                return true;

            Vector3 targetPoint = targetCollider.ClosestPoint(origin);
            Vector3 offset = targetPoint - origin;
            float distance = offset.magnitude;
            if (distance <= 0.01f)
                return true;

            int count = Physics.RaycastNonAlloc(
                origin,
                offset / distance,
                obstructionBuffer,
                distance,
                obstructionMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider blocker = obstructionBuffer[i].collider;
                if (blocker == null)
                    continue;

                IInteractable blockerInteractable = FindInteractable(blocker);
                if (ReferenceEquals(blockerInteractable, target))
                    continue;

                return false;
            }

            return true;
        }

        private IInteractable FindInteractable(Collider hitCollider)
        {
            componentBuffer.Clear();
            hitCollider.GetComponentsInParent(true, componentBuffer);

            foreach (MonoBehaviour behaviour in componentBuffer)
            {
                if (behaviour is IInteractable interactable &&
                    behaviour.isActiveAndEnabled)
                {
                    return interactable;
                }
            }

            return null;
        }

        private void ProcessInput()
        {
            if (currentInteractable == null)
                return;

            InteractionPrompt prompt = currentInteractable.GetPrompt();
            if (!prompt.IsVisible || !prompt.IsAvailable)
            {
                if (activeInteractable != null)
                    CancelActiveInteraction();
                return;
            }

            if (prompt.Mode == InteractionMode.Press)
            {
                if (Input.GetKeyDown(interactionKey))
                    CompletePressInteraction(currentInteractable);
                return;
            }

            ProcessHoldInteraction(prompt);
        }

        private void CompletePressInteraction(IInteractable interactable)
        {
            interactable.BeginInteraction(gameObject);
            interactable.CompleteInteraction(gameObject);
            InteractionStateChanged?.Invoke();
        }

        private void ProcessHoldInteraction(InteractionPrompt prompt)
        {
            if (Input.GetKeyDown(interactionKey))
                BeginHoldInteraction(currentInteractable);

            if (activeInteractable == null)
                return;

            if (Input.GetKeyUp(interactionKey) ||
                !Input.GetKey(interactionKey))
            {
                CancelActiveInteraction();
                return;
            }

            holdElapsed += Time.deltaTime;
            HoldProgress = Mathf.Clamp01(
                holdElapsed / Mathf.Max(0.1f, prompt.HoldDuration));
            InteractionStateChanged?.Invoke();

            if (HoldProgress < 1f)
                return;

            IInteractable completed = activeInteractable;
            ResetInteractionState();
            completed.CompleteInteraction(gameObject);
            InteractionStateChanged?.Invoke();
        }

        private void BeginHoldInteraction(IInteractable interactable)
        {
            activeInteractable = interactable;
            holdElapsed = 0f;
            HoldProgress = 0f;
            interactable.BeginInteraction(gameObject);
            InteractionStateChanged?.Invoke();
        }

        private void CancelActiveInteraction()
        {
            if (activeInteractable == null)
                return;

            IInteractable cancelled = activeInteractable;
            ResetInteractionState();
            cancelled.CancelInteraction(gameObject);
            InteractionStateChanged?.Invoke();
        }

        private void ResetInteractionState()
        {
            activeInteractable = null;
            holdElapsed = 0f;
            HoldProgress = 0f;
        }

        private void SetCurrentInteractable(IInteractable interactable)
        {
            currentInteractable = interactable;
            TargetChanged?.Invoke();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.3f);
            Gizmos.DrawWireSphere(
                transform.TransformPoint(detectionOffset),
                interactionDistance);
        }
    }
}
