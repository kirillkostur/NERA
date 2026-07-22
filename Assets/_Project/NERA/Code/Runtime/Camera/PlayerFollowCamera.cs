using UnityEngine;

public class PlayerFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private bool autoFindPlayerOnStart = true;
    [SerializeField] private bool autoFindPlayerIfMissing = true;

    [Header("Follow")]
    [SerializeField] private float height = 1.8f;
    [SerializeField] private float positionSmooth = 10f;
    [SerializeField] private float rotationSmooth = 12f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 1.2f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;
    [SerializeField] private bool lockCursor = true;

    [Header("Distance")]
    [SerializeField] private float distance = 5f;
    [SerializeField] private float minDistance = 2.5f;
    [SerializeField] private float maxDistance = 8f;
    [SerializeField] private float distanceTransitionSpeed = 4f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 1f;

    [Header("Aim")]
    [SerializeField] private KeyCode aimKey = KeyCode.Mouse1;
    [SerializeField, Min(0.1f)] private float aimDistance = 2.2f;
    [SerializeField] private Vector3 aimShoulderOffset = new Vector3(0.65f, -0.05f, 0.25f);
    [SerializeField, Min(0f)] private float aimLookAhead = 8f;

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask;
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float collisionOffset = 0.15f;
    [SerializeField] private float minCollisionDistance = 0.6f;
    [SerializeField] private float collisionSmoothIn = 25f;
    [SerializeField] private float collisionSmoothOut = 8f;

    private const string PlayerTag = "Player";

    private float yaw;
    private float pitch;

    private float targetDistance;
    private float currentDistance;

    private float defaultMinDistance;
    private float defaultMaxDistance;
    private float defaultDistance;
    private bool inputEnabled = true;
    private bool isAiming;
    private float aimWeight;
    private PlayerController targetPlayerController;

    public bool IsAiming => isAiming && aimWeight > 0.5f;
    public float Yaw => yaw;

    private void Start()
    {
        InitializeCamera();
        CacheTargetPlayerController();

        if (autoFindPlayerOnStart)
            TryFindPlayerTarget();

        ApplyGameplayCursorState();
    }

    private void LateUpdate()
    {
        if (target == null && autoFindPlayerIfMissing)
            TryFindPlayerTarget();

        if (inputEnabled)
            ApplyGameplayCursorState();

        if (target == null)
            return;

        if (inputEnabled)
        {
            ReadMouseInput();
            ReadAimInput();
            ReadZoomInput();
        }
        else
        {
            isAiming = false;
        }

        UpdateAimWeight();
        UpdateDistance();
        UpdateCamera();
    }

    private void ApplyGameplayCursorState()
    {
        if (!lockCursor)
            return;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void InitializeCamera()
    {
        Vector3 startAngles = transform.eulerAngles;

        yaw = startAngles.y;
        pitch = startAngles.x;

        minDistance = Mathf.Max(0.1f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);

        distance = Mathf.Clamp(distance, minDistance, maxDistance);
        targetDistance = distance;
        currentDistance = distance;

        SaveDefaultDistanceSettings();
    }

    private void TryFindPlayerTarget()
    {
        Transform foundTarget = null;

        GameObject playerByTag = GameObject.FindGameObjectWithTag(PlayerTag);

        if (playerByTag != null)
            foundTarget = playerByTag.transform;

        if (foundTarget == null)
        {
            PlayerController playerController = FindFirstObjectByType<PlayerController>();

            if (playerController != null)
                foundTarget = playerController.transform;
        }

        if (foundTarget == null)
        {
            Debug.LogWarning("PlayerFollowCamera: Player target not found.");
            return;
        }

        SetTarget(foundTarget);
        Debug.Log($"PlayerFollowCamera: Target auto assigned to '{foundTarget.name}'.");
    }

    private void ReadMouseInput()
    {
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void ReadZoomInput()
    {
        if (isAiming)
            return;

        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) < 0.001f)
            return;

        targetDistance -= scroll * zoomSpeed;
        targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
    }

    private void ReadAimInput()
    {
        isAiming = !IsTargetCrouching() && Input.GetKey(aimKey);
    }

    private bool IsTargetCrouching()
    {
        return targetPlayerController != null
            && targetPlayerController.IsCrouching;
    }

    private void CacheTargetPlayerController()
    {
        targetPlayerController = target != null
            ? target.GetComponentInParent<PlayerController>()
            : null;
    }

    private void UpdateAimWeight()
    {
        float targetAimWeight = isAiming ? 1f : 0f;

        aimWeight = Mathf.MoveTowards(
            aimWeight,
            targetAimWeight,
            distanceTransitionSpeed * Time.deltaTime
        );
    }

    private void UpdateDistance()
    {
        float desiredDistance = isAiming
            ? Mathf.Clamp(aimDistance, minDistance, maxDistance)
            : targetDistance;

        distance = Mathf.MoveTowards(
            distance,
            desiredDistance,
            distanceTransitionSpeed * Time.deltaTime
        );
    }

    private void UpdateCamera()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 baseLookPoint = target.position + Vector3.up * height;
        Vector3 shoulderOffset = rotation * aimShoulderOffset * aimWeight;
        Vector3 lookPoint = baseLookPoint + shoulderOffset;
        Vector3 aimLookPoint = baseLookPoint + rotation * Vector3.forward * aimLookAhead * aimWeight;
        Vector3 rotationLookPoint = Vector3.Lerp(baseLookPoint, aimLookPoint, aimWeight);
        Vector3 cameraDirection = rotation * Vector3.back;

        float availableDistance = GetAvailableDistance(lookPoint, cameraDirection);

        float collisionSmooth = availableDistance < currentDistance
            ? collisionSmoothIn
            : collisionSmoothOut;

        currentDistance = Mathf.Lerp(
            currentDistance,
            availableDistance,
            collisionSmooth * Time.deltaTime
        );

        Vector3 desiredPosition = lookPoint + cameraDirection * currentDistance;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            positionSmooth * Time.deltaTime
        );

        Quaternion desiredRotation = Quaternion.LookRotation(rotationLookPoint - transform.position);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSmooth * Time.deltaTime
        );
    }

    private float GetAvailableDistance(Vector3 origin, Vector3 direction)
    {
        if (collisionMask.value == 0)
            return distance;

        bool hasHit = Physics.SphereCast(
            origin,
            collisionRadius,
            direction,
            out RaycastHit hit,
            distance,
            collisionMask,
            QueryTriggerInteraction.Ignore
        );

        if (!hasHit)
            return distance;

        float blockedDistance = hit.distance - collisionOffset;

        return Mathf.Clamp(
            blockedDistance,
            minCollisionDistance,
            distance
        );
    }

    private void SaveDefaultDistanceSettings()
    {
        defaultMinDistance = minDistance;
        defaultMaxDistance = maxDistance;
        defaultDistance = distance;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        CacheTargetPlayerController();
    }

    public void ClearTarget()
    {
        target = null;
        targetPlayerController = null;
        isAiming = false;
    }

    public bool HasTarget()
    {
        return target != null;
    }

    public Transform GetTarget()
    {
        return target;
    }

    public void ApplyPreset(CameraPreset preset)
    {
        if (preset == null)
            return;

        SetDistanceRange(
            preset.MinDistance,
            preset.MaxDistance,
            preset.DefaultDistance
        );
    }

    public void RestoreDefaultPreset()
    {
        SetDistanceRange(
            defaultMinDistance,
            defaultMaxDistance,
            defaultDistance
        );
    }

    public void SetDistanceRange(float newMinDistance, float newMaxDistance, float newDistance)
    {
        minDistance = Mathf.Max(0.1f, newMinDistance);
        maxDistance = Mathf.Max(minDistance, newMaxDistance);

        targetDistance = Mathf.Clamp(newDistance, minDistance, maxDistance);

        if (distance < minDistance && targetDistance >= minDistance)
            distance = minDistance;

        currentDistance = Mathf.Clamp(
            currentDistance,
            minCollisionDistance,
            Mathf.Max(currentDistance, maxDistance)
        );
    }

    public void SetDistance(float newDistance)
    {
        targetDistance = Mathf.Clamp(newDistance, minDistance, maxDistance);
    }

    public float GetDistance()
    {
        return distance;
    }

    public float GetTargetDistance()
    {
        return targetDistance;
    }

    public float GetCurrentDistance()
    {
        return currentDistance;
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;

        if (inputEnabled)
            ApplyGameplayCursorState();
        else
            isAiming = false;
    }
}
