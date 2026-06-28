using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class CameraDistanceZone : MonoBehaviour
{
    [Header("Preset")]
    [SerializeField] private CameraPreset preset;

    [Header("Player")]
    [SerializeField] private string playerTag = "Player";

    private PlayerFollowCamera playerCamera;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        playerCamera = FindFirstObjectByType<PlayerFollowCamera>();

        if (playerCamera == null)
            return;

        playerCamera.ApplyPreset(preset);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag))
            return;

        if (playerCamera == null)
            playerCamera = FindFirstObjectByType<PlayerFollowCamera>();

        if (playerCamera == null)
            return;

        playerCamera.RestoreDefaultPreset();
    }
}
