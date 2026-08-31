using UnityEngine;

namespace NERA.Enemies
{
    public sealed class IORotatingVisual : MonoBehaviour
    {
        [SerializeField] private Vector3 rotationAxis = Vector3.up;
        [SerializeField] private float degreesPerSecond = 45f;

        private void Update()
        {
            Vector3 axis = rotationAxis.sqrMagnitude > 0.001f
                ? rotationAxis.normalized
                : Vector3.up;
            transform.Rotate(
                axis,
                degreesPerSecond * Time.deltaTime,
                Space.Self);
        }
    }
}