using System;
using UnityEngine;

namespace NERA.Interaction
{
    public sealed class PlayerInteractionController : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private Camera interactionCamera;
        [SerializeField, Min(0.1f)] private float interactionDistance = 2.5f;
        [SerializeField] private LayerMask raycastMask = Physics.DefaultRaycastLayers;
        [SerializeField] private QueryTriggerInteraction triggerInteraction =
            QueryTriggerInteraction.Collide;

        [Header("Input")]
        [SerializeField] private KeyCode interactionKey = KeyCode.E;

        private IInteractable currentInteractable;
        private IInteractable activeInteractable;
        private float holdElapsed;
        private readonly RaycastHit[] raycastHits = new RaycastHit[8];

        public event Action TargetChanged;
        public event Action InteractionStateChanged;

        public IInteractable CurrentInteractable => currentInteractable;
        public bool IsInteracting => activeInteractable != null;
        public float HoldProgress { get; private set; }

        private void Awake()
        {
            if (interactionCamera == null)
                interactionCamera = Camera.main;
        }

        private void Update()
        {
            if (interactionCamera == null)
                interactionCamera = Camera.main;

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
            IInteractable detected = null;

            if (interactionCamera != null)
            {
                Ray ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                float cameraOffset = Vector3.Distance(
                    interactionCamera.transform.position,
                    transform.position
                );
                float raycastDistance = cameraOffset + interactionDistance;

                int hitCount = Physics.RaycastNonAlloc(
                    ray,
                    raycastHits,
                    raycastDistance,
                    raycastMask,
                    triggerInteraction
                );

                Collider closestCollider = GetClosestNonPlayerCollider(hitCount);

                if (closestCollider != null)
                {
                    detected = FindInteractable(closestCollider);

                    if (detected != null &&
                        Vector3.Distance(
                            transform.position,
                            detected.InteractionTransform.position
                        ) > interactionDistance)
                    {
                        detected = null;
                    }
                }
            }

            if (ReferenceEquals(detected, currentInteractable))
                return;

            CancelActiveInteraction();
            SetCurrentInteractable(detected);
        }

        private Collider GetClosestNonPlayerCollider(int hitCount)
        {
            Collider closestCollider = null;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = raycastHits[i];

                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(transform) ||
                    hit.distance >= closestDistance)
                {
                    continue;
                }

                closestCollider = hit.collider;
                closestDistance = hit.distance;
            }

            return closestCollider;
        }

        private static IInteractable FindInteractable(Collider hitCollider)
        {
            MonoBehaviour[] behaviours = hitCollider.GetComponentsInParent<MonoBehaviour>(true);

            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IInteractable interactable && behaviour.isActiveAndEnabled)
                    return interactable;
            }

            return null;
        }

        private void ProcessInput()
        {
            if (currentInteractable == null)
                return;

            InteractionPrompt prompt = currentInteractable.GetPrompt();

            if (!prompt.IsAvailable)
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

            if (Input.GetKeyUp(interactionKey) || !Input.GetKey(interactionKey))
            {
                CancelActiveInteraction();
                return;
            }

            holdElapsed += Time.deltaTime;
            HoldProgress = Mathf.Clamp01(holdElapsed / Mathf.Max(0.1f, prompt.HoldDuration));
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
    }
}
