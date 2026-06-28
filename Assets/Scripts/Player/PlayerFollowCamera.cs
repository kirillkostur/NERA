using UnityEngine;

public class PlayerFollowCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

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

    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask;
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private float collisionOffset = 0.15f;
    [SerializeField] private float minCollisionDistance = 0.6f;
    [SerializeField] private float collisionSmoothIn = 25f;
    [SerializeField] private float collisionSmoothOut = 8f;

    private float yaw;
    private float pitch;

    private float targetDistance;
    private float currentDistance;

    private float defaultMinDistance;
    private float defaultMaxDistance;
    private float defaultDistance;

    private void Start()
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

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        ReadMouseInput();
        ReadZoomInput();
        UpdateDistance();
        UpdateCamera();
    }

    private void ReadMouseInput()
    {
        yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
        pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    private void ReadZoomInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Abs(scroll) < 0.001f)
            return;

        targetDistance -= scroll * zoomSpeed;
        targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
    }

    private void UpdateDistance()
    {
        distance = Mathf.MoveTowards(
            distance,
            targetDistance,
            distanceTransitionSpeed * Time.deltaTime
        );
    }

    private void UpdateCamera()
    {
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 lookPoint = target.position + Vector3.up * height;
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

        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position);

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

        currentDistance = Mathf.Clamp(currentDistance, minCollisionDistance, Mathf.Max(currentDistance, maxDistance));
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
}
