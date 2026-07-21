using UnityEngine;
using UnityEngine.UI;

namespace NERA.Combat
{
    [RequireComponent(typeof(Image))]
    public sealed class AimCrosshairHUD : MonoBehaviour
    {
        private Image crosshair;
        private PlayerFollowCamera followCamera;

        private void Awake()
        {
            crosshair = GetComponent<Image>();
            crosshair.raycastTarget = false;
            crosshair.enabled = false;
        }

        private void LateUpdate()
        {
            if (followCamera == null)
                followCamera = FindFirstObjectByType<PlayerFollowCamera>();

            crosshair.enabled =
                followCamera != null &&
                followCamera.IsAiming &&
                Cursor.lockState == CursorLockMode.Locked;
        }
    }
}
