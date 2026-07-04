using UnityEngine;

[CreateAssetMenu(
    fileName = "CameraPreset",
    menuName = "NERA/Camera/Camera Preset"
)]
public class CameraPreset : ScriptableObject
{
    [Header("Distance")]
    [SerializeField] private float minDistance = 2.5f;
    [SerializeField] private float maxDistance = 5f;
    [SerializeField] private float defaultDistance = 3.5f;

    public float MinDistance => minDistance;
    public float MaxDistance => maxDistance;
    public float DefaultDistance => defaultDistance;

    private void OnValidate()
    {
        minDistance = Mathf.Max(0.1f, minDistance);
        maxDistance = Mathf.Max(minDistance, maxDistance);
        defaultDistance = Mathf.Clamp(defaultDistance, minDistance, maxDistance);
    }
}
