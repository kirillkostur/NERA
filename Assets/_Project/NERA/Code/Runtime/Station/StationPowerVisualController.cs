using System.Collections.Generic;
using NERA.Energy;
using UnityEngine;

namespace NERA.Station
{
    public sealed class StationPowerVisualController : MonoBehaviour
    {
        private const string LightingConsumerId = "station_lighting";
        [SerializeField] private Light[] poweredLights;
        [SerializeField] private Renderer[] poweredRenderers;
        [SerializeField] private Color offlineColor = new Color(0.08f, 0.09f, 0.1f);
        [SerializeField] private Color onlineColor = new Color(0.1f, 0.75f, 0.9f);

        private readonly List<Material> runtimeMaterials = new List<Material>();
        private StationPowerController powerController;
        private EnergySystemController energySystem;

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
            energySystem = EnergySystemController.Instance;
            if (energySystem != null)
            {
                energySystem.RegisterConsumer(
                    LightingConsumerId,
                    energySystem.Config.LightingConsumption,
                    true
                );
                energySystem.SetConsumerActive(LightingConsumerId, true);
                energySystem.EnergyChanged += ApplyEnergyState;
            }
            ApplyState(powerController.State);
        }

        private void ApplyState(StationPowerState state)
        {
            bool isPowered =
                state == StationPowerState.Online &&
                (energySystem == null ||
                 energySystem.IsConsumerPowered(LightingConsumerId));

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

        private void ApplyEnergyState()
        {
            if (powerController != null)
                ApplyState(powerController.State);
        }

        private void OnDestroy()
        {
            if (powerController != null)
                powerController.StateChanged -= ApplyState;

            if (energySystem != null)
                energySystem.EnergyChanged -= ApplyEnergyState;

            foreach (Material material in runtimeMaterials)
            {
                if (material != null)
                    Destroy(material);
            }
        }
    }
}
