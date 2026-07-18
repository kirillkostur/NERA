using NERA.Interaction;
using UnityEngine;

namespace NERA.Energy
{
    public sealed class SolarPanelInteractable : BaseInteractable
    {
        [SerializeField, Min(0f)] private float outputMultiplier = 1f;
        [SerializeField, Range(0f, 1f)] private float initialContamination;
        [SerializeField, Min(0.1f)] private float cleaningDuration = 2f;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color cleanColor = new Color(0.12f, 0.35f, 0.55f);
        [SerializeField] private Color dirtyColor = new Color(0.55f, 0.35f, 0.12f);

        private Material runtimeMaterial;
        private bool registered;
        private string registeredPanelId;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();
            if (targetRenderer != null)
                runtimeMaterial = targetRenderer.material;
        }

        private void Start()
        {
            Register();
            RefreshVisual();
        }

        private void Update()
        {
            if (!registered)
                Register();
            RefreshVisual();
        }

        public override InteractionPrompt GetPrompt()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            float contamination = energy != null
                ? energy.GetSolarContamination(ActivePanelId)
                : 0f;

            if (contamination <= 0.01f)
            {
                string status =
                    StationEnvironmentController.Instance != null &&
                    !StationEnvironmentController.Instance.IsDaytime
                        ? "Night — No Generation"
                        : "Panel Operational";
                return new InteractionPrompt(
                    "Clean Solar Panel",
                    InteractionMode.Hold,
                    cleaningDuration,
                    false,
                    status
                );
            }

            return new InteractionPrompt(
                "Clean / Repair Solar Panel",
                InteractionMode.Hold,
                cleaningDuration,
                true,
                $"Contamination {Mathf.RoundToInt(contamination * 100f)}%"
            );
        }

        public override void CompleteInteraction(GameObject interactor)
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null || !energy.CleanSolarPanel(ActivePanelId))
                return;

            base.CompleteInteraction(interactor);
            RefreshVisual();
        }

        private void Register()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return;

            string hierarchyId = BuildHierarchyId();
            if (energy.RegisterSolarPanel(
                    hierarchyId,
                    outputMultiplier,
                    initialContamination))
            {
                registeredPanelId = hierarchyId;
            }
            else
                return;

            registered = true;
        }

        private void RefreshVisual()
        {
            if (runtimeMaterial == null || EnergySystemController.Instance == null)
                return;

            float contamination =
                EnergySystemController.Instance.GetSolarContamination(ActivePanelId);
            runtimeMaterial.color = Color.Lerp(cleanColor, dirtyColor, contamination);
        }

        private string ActivePanelId =>
            string.IsNullOrWhiteSpace(registeredPanelId)
                ? BuildHierarchyId()
                : registeredPanelId;

        private string BuildHierarchyId()
        {
            string path = gameObject.scene.path;
            Transform current = transform;

            while (current != null)
            {
                path += $"/{current.name}[{current.GetSiblingIndex()}]";
                current = current.parent;
            }

            return $"solar:{path}";
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }
    }
}
