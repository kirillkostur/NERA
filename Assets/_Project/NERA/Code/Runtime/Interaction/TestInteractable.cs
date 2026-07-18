using UnityEngine;

namespace NERA.Interaction
{
    public sealed class TestInteractable : BaseInteractable
    {
        [Header("Test Feedback")]
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color completedColor = new Color(0.1f, 0.8f, 0.45f);

        private Material runtimeMaterial;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            if (targetRenderer != null)
                runtimeMaterial = targetRenderer.material;
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            base.CompleteInteraction(interactor);

            if (runtimeMaterial != null)
                runtimeMaterial.color = completedColor;

            SetAvailable(false, "Completed");
            Debug.Log($"{name}: Interaction completed.", this);
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }
    }
}
