using System.Collections.Generic;
using UnityEngine;

namespace NERA.Station
{
    public sealed class StationPowerVisualController : MonoBehaviour
    {
        [SerializeField] private Light[] poweredLights;
        [SerializeField] private Renderer[] poweredRenderers;
        [SerializeField] private Color offlineColor = new Color(0.08f, 0.09f, 0.1f);
        [SerializeField] private Color onlineColor = new Color(0.1f, 0.75f, 0.9f);

        private readonly List<Material> runtimeMaterials = new List<Material>();
        private StationPowerController powerController;

        private void Awake()
        {
            foreach (Renderer targetRenderer in poweredRenderers)
            {
                if (targetRenderer != null)
                    runtimeMaterials.Add(targetRenderer.material);
            }
        }

        private void Start()
        {
            powerController = StationPowerController.Instance;

            if (powerController == null)
            {
                Debug.LogError(
                    "StationPowerVisualController: StationPowerController is missing.",
                    this
                );
                return;
            }

            powerController.StateChanged += ApplyState;
            ApplyState(powerController.State);
        }

        private void ApplyState(StationPowerState state)
        {
            bool isPowered = state == StationPowerState.Online;

            foreach (Light poweredLight in poweredLights)
            {
                if (poweredLight != null)
                    poweredLight.enabled = isPowered;
            }

            Color targetColor = isPowered ? onlineColor : offlineColor;

            foreach (Material material in runtimeMaterials)
            {
                if (material != null)
                    material.color = targetColor;
            }
        }

        private void OnDestroy()
        {
            if (powerController != null)
                powerController.StateChanged -= ApplyState;

            foreach (Material material in runtimeMaterials)
            {
                if (material != null)
                    Destroy(material);
            }
        }
    }
}
