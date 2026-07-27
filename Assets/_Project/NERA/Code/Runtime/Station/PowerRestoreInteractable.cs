using NERA.Interaction;
using UnityEngine;

namespace NERA.Station
{
    public sealed class PowerRestoreInteractable : BaseInteractable
    {
        [Header("Visual Feedback")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color onlineColor = new Color(0.1f, 0.8f, 0.45f);

        private Material runtimeMaterial;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            if (targetRenderer != null)
                runtimeMaterial = targetRenderer.material;
        }

        public override InteractionPrompt GetPrompt()
        {
            StationPowerController power = StationPowerController.Instance;

            if (power != null && power.IsPowered)
            {
                return new InteractionPrompt(
                    "Restore Power",
                    InteractionMode.Hold,
                    0f,
                    false,
                    "Station Power Online"
                );
            }

            return base.GetPrompt();
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            StationPowerController power = StationPowerController.Instance;

            if (power == null)
            {
                Debug.LogError("PowerRestoreInteractable: StationPowerController is missing.", this);
                return;
            }

            if (!power.RestorePower())
                return;

            StationSystemsController.Instance?.SetCriticalSystemActive(
                StationSystemType.Battery,
                true);

            base.CompleteInteraction(interactor);

            if (runtimeMaterial != null)
                runtimeMaterial.color = onlineColor;
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }
    }
}
