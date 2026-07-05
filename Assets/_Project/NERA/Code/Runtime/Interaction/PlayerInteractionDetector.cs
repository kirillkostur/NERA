using UnityEngine;

public class PlayerInteractionDetector : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private bool autoFindCamera = true;

    [Header("Interaction Area")]
    [SerializeField] private float interactionRadius = 2.2f;
    [SerializeField] private float maxInteractionDistance = 3.2f;
    [SerializeField] private float maxViewAngle = 70f;
    [SerializeField] private LayerMask interactionMask;

    [Header("Scoring")]
    [SerializeField] private float angleWeight = 1.5f;
    [SerializeField] private float distanceWeight = 1f;

    [Header("Input")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;
    [SerializeField] private bool logCurrentInteractable = false;

    private IInteractable currentInteractable;
    private IInteractable lastInteractable;
    private IInteractable activeHoldInteractable;

    private readonly Collider[] overlapResults = new Collider[16];

    private void Start()
    {
        if (playerCamera == null && autoFindCamera)
            TryFindCamera();
    }

    private void Update()
    {
        if (playerCamera == null && autoFindCamera)
            TryFindCamera();

        DetectInteractable();
        HandleInput();
    }

    private void TryFindCamera()
    {
        playerCamera = Camera.main;

        if (playerCamera == null)
        {
            PlayerFollowCamera followCamera = FindFirstObjectByType<PlayerFollowCamera>();

            if (followCamera != null)
                playerCamera = followCamera.GetComponent<Camera>();
        }

        if (playerCamera != null)
            Debug.Log($"PlayerInteractionDetector: Camera assigned: {playerCamera.name}");
    }

    private void DetectInteractable()
    {
        currentInteractable = null;

        Vector3 origin = transform.position + Vector3.up * 1f;
        Vector3 viewForward = GetViewForward();

        int hitCount = Physics.OverlapSphereNonAlloc(
            origin,
            interactionRadius,
            overlapResults,
            interactionMask,
            QueryTriggerInteraction.Ignore
        );

        IInteractable bestInteractable = null;
        float bestScore = float.MinValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = overlapResults[i];

            if (hitCollider == null)
                continue;

            IInteractable interactable = hitCollider.GetComponentInParent<IInteractable>();

            if (interactable == null)
                continue;

            if (!interactable.CanInteract)
                continue;

            Transform interactableTransform = GetInteractableTransform(interactable);

            if (interactableTransform == null)
                continue;

            Vector3 targetPoint = GetTargetPoint(hitCollider, interactableTransform);
            Vector3 toTarget = targetPoint - origin;

            float distance = toTarget.magnitude;

            if (distance > maxInteractionDistance)
                continue;

            Vector3 directionToTarget = toTarget.normalized;
            float angle = Vector3.Angle(viewForward, directionToTarget);

            if (angle > maxViewAngle)
                continue;

            float normalizedAngleScore = 1f - Mathf.Clamp01(angle / maxViewAngle);
            float normalizedDistanceScore = 1f - Mathf.Clamp01(distance / maxInteractionDistance);

            float score =
                normalizedAngleScore * angleWeight +
                normalizedDistanceScore * distanceWeight;

            if (score > bestScore)
            {
                bestScore = score;
                bestInteractable = interactable;
            }
        }

        currentInteractable = bestInteractable;

        if (activeHoldInteractable != null && activeHoldInteractable != currentInteractable)
            CancelActiveHold();

        UpdateLastInteractable(currentInteractable);

        if (showDebug)
            DrawDebug(origin, viewForward);
    }

    private Vector3 GetViewForward()
    {
        if (playerCamera != null)
            return playerCamera.transform.forward;

        return transform.forward;
    }

    private Transform GetInteractableTransform(IInteractable interactable)
    {
        if (interactable is MonoBehaviour monoBehaviour)
            return monoBehaviour.transform;

        return null;
    }

    private Vector3 GetTargetPoint(Collider hitCollider, Transform fallbackTransform)
    {
        if (hitCollider != null)
            return hitCollider.bounds.center;

        return fallbackTransform.position;
    }

    private void UpdateLastInteractable(IInteractable interactable)
    {
        if (lastInteractable == interactable)
            return;

        lastInteractable = interactable;

        if (!logCurrentInteractable)
            return;

        if (lastInteractable == null)
            Debug.Log("PlayerInteractionDetector: No interactable.");
        else
            Debug.Log($"PlayerInteractionDetector: Found interactable: {lastInteractable.InteractionText}");
    }

    private void HandleInput()
    {
        if (currentInteractable == null)
        {
            if (activeHoldInteractable != null)
                CancelActiveHold();

            return;
        }

        if (!currentInteractable.CanInteract)
        {
            if (activeHoldInteractable != null)
                CancelActiveHold();

            return;
        }

        if (currentInteractable.InteractionType == InteractionType.Press)
        {
            HandlePressInput();
            return;
        }

        if (currentInteractable.InteractionType == InteractionType.Hold)
        {
            HandleHoldInput();
        }
    }

    private void HandlePressInput()
    {
        if (Input.GetKeyDown(interactKey))
            currentInteractable.Interact();
    }

    private void HandleHoldInput()
    {
        if (Input.GetKeyDown(interactKey))
        {
            activeHoldInteractable = currentInteractable;
            activeHoldInteractable.StartHold();
        }

        if (Input.GetKey(interactKey))
        {
            if (activeHoldInteractable == null)
            {
                activeHoldInteractable = currentInteractable;
                activeHoldInteractable.StartHold();
            }

            activeHoldInteractable.UpdateHold(Time.deltaTime);
        }

        if (Input.GetKeyUp(interactKey))
        {
            CancelActiveHold();
        }
    }

    private void CancelActiveHold()
    {
        if (activeHoldInteractable == null)
            return;

        activeHoldInteractable.CancelHold();
        activeHoldInteractable = null;
    }

    private void DrawDebug(Vector3 origin, Vector3 viewForward)
    {
        Debug.DrawRay(origin, viewForward * maxInteractionDistance, Color.cyan);

#if UNITY_EDITOR
        Vector3 leftDirection = Quaternion.Euler(0f, -maxViewAngle, 0f) * viewForward;
        Vector3 rightDirection = Quaternion.Euler(0f, maxViewAngle, 0f) * viewForward;

        Debug.DrawRay(origin, leftDirection * maxInteractionDistance, Color.yellow);
        Debug.DrawRay(origin, rightDirection * maxInteractionDistance, Color.yellow);
#endif
    }

    public bool HasInteractable()
    {
        return currentInteractable != null;
    }

    public IInteractable GetCurrentInteractable()
    {
        return currentInteractable;
    }
}