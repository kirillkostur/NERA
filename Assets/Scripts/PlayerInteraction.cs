using System.Collections;
using UnityEngine;

public class PlayerInteraction : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Animator animator;

    [Header("Detection")]
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private float interactionHeight = 1f;
    [SerializeField] private LayerMask interactionMask;

    [Header("Input")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    [Header("Interrupt")]
    [SerializeField] private bool interruptOnMoveInput = true;
    [SerializeField] private bool interruptWhenTargetLost = true;
    [SerializeField] private float maxDistanceDuringInteraction = 3.5f;

    private Interactable currentInteractable;
    private Interactable activeInteractable;
    private Coroutine interactionRoutine;
    private bool isInteracting;

    private static readonly int AnimatorIsInteracting = Animator.StringToHash("IsInteracting");
    private static readonly int AnimatorPickup = Animator.StringToHash("InteractPickup");
    private static readonly int AnimatorSearch = Animator.StringToHash("InteractSearch");
    private static readonly int AnimatorRepair = Animator.StringToHash("InteractRepair");
    private static readonly int AnimatorPush = Animator.StringToHash("InteractPush");
    private static readonly int AnimatorUse = Animator.StringToHash("InteractUse");

    public Interactable CurrentInteractable => currentInteractable;
    public bool IsInteracting => isInteracting;

    private void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (isInteracting)
            return;

        FindInteractable();

        if (Input.GetKeyDown(interactionKey))
            TryInteract();
    }

    private void FindInteractable()
    {
        currentInteractable = null;

        Vector3 origin = transform.position + Vector3.up * interactionHeight;

        Collider[] hits = Physics.OverlapSphere(
            origin,
            interactionDistance,
            interactionMask,
            QueryTriggerInteraction.Ignore
        );

        float bestScore = float.MinValue;
        Interactable bestInteractable = null;

        Vector3 lookDirection = GetLookDirection();

        for (int i = 0; i < hits.Length; i++)
        {
            if (!hits[i].TryGetComponent(out Interactable interactable))
                continue;

            if (!interactable.CanInteract)
                continue;

            Vector3 directionToTarget = interactable.transform.position - transform.position;
            directionToTarget.y = 0f;

            float distance = directionToTarget.magnitude;

            if (distance < 0.01f)
                distance = 0.01f;

            Vector3 normalizedDirection = directionToTarget.normalized;

            float lookScore = Vector3.Dot(lookDirection, normalizedDirection);
            float distanceScore = 1f / distance;

            float score = lookScore + distanceScore;

            if (score > bestScore)
            {
                bestScore = score;
                bestInteractable = interactable;
            }
        }

        currentInteractable = bestInteractable;
    }

    private Vector3 GetLookDirection()
    {
        if (cameraTransform != null)
        {
            Vector3 cameraForward = cameraTransform.forward;
            cameraForward.y = 0f;

            if (cameraForward.sqrMagnitude > 0.01f)
                return cameraForward.normalized;
        }

        return transform.forward;
    }

    private void TryInteract()
    {
        if (currentInteractable == null)
        {
            Debug.Log("No interactable object nearby.");
            return;
        }

        if (currentInteractable.Preset == null)
        {
            Debug.LogWarning($"Interactable has no preset: {currentInteractable.name}");
            return;
        }

        Debug.Log($"Interact with: {currentInteractable.name}");

        interactionRoutine = StartCoroutine(
            InteractionRoutine(currentInteractable, currentInteractable.Preset)
        );
    }

    private IEnumerator InteractionRoutine(Interactable interactable, InteractionPreset preset)
    {
        isInteracting = true;
        activeInteractable = interactable;

        if (preset.FaceTarget)
            FaceTarget(interactable.transform);

        PlayAnimation(preset.AnimationType);

        interactable.StartInteraction(this);

        if (preset.Duration > 0f)
        {
            float timer = 0f;

            while (timer < preset.Duration)
            {
                if (preset.CanBeInterrupted && ShouldInterrupt(interactable))
                {
                    CancelInteraction();
                    yield break;
                }

                timer += Time.deltaTime;
                yield return null;
            }
        }

        interactable.CompleteInteraction(this);
        FinishInteraction();
    }

    private bool ShouldInterrupt(Interactable interactable)
    {
        if (interruptOnMoveInput && HasMoveInput())
            return true;

        if (interruptWhenTargetLost && !IsTargetStillValid(interactable))
            return true;

        return false;
    }

    private bool HasMoveInput()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        return Mathf.Abs(horizontal) > 0.01f || Mathf.Abs(vertical) > 0.01f;
    }

    private bool IsTargetStillValid(Interactable interactable)
    {
        if (interactable == null)
            return false;

        if (!interactable.gameObject.activeInHierarchy)
            return false;

        if (!interactable.CanInteract)
            return false;

        float distance = Vector3.Distance(transform.position, interactable.transform.position);

        return distance <= maxDistanceDuringInteraction;
    }

    private void FaceTarget(Transform target)
    {
        Vector3 direction = target.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.01f)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized);
    }

    private void PlayAnimation(InteractionAnimationType animationType)
    {
        if (animator == null)
            return;

        animator.SetBool(AnimatorIsInteracting, true);

        switch (animationType)
        {
            case InteractionAnimationType.Pickup:
                animator.SetTrigger(AnimatorPickup);
                break;

            case InteractionAnimationType.Search:
                animator.SetTrigger(AnimatorSearch);
                break;

            case InteractionAnimationType.Repair:
                animator.SetTrigger(AnimatorRepair);
                break;

            case InteractionAnimationType.Push:
                animator.SetTrigger(AnimatorPush);
                break;

            case InteractionAnimationType.Use:
                animator.SetTrigger(AnimatorUse);
                break;
        }
    }

    public void CancelInteraction()
    {
        if (!isInteracting)
            return;

        if (interactionRoutine != null)
            StopCoroutine(interactionRoutine);

        if (activeInteractable != null)
            activeInteractable.CancelInteraction(this);

        FinishInteraction();
    }

    private void FinishInteraction()
    {
        if (animator != null)
            animator.SetBool(AnimatorIsInteracting, false);

        isInteracting = false;
        activeInteractable = null;
        interactionRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up * interactionHeight;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(origin, interactionDistance);
    }
}
