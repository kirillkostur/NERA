using System.Collections.Generic;
using NERA.Items;
using NERA.Localization;
using UnityEngine;
using UnityEngine.Serialization;

namespace NERA.Combat
{
    public enum AnomalyIntegrationEffect
    {
        EnableElectronics = 0,
        DamageAnomalies = 1,
        RestoreFullHealth = 3,
        RevealThroughWalls = 4,
        DisableElectronicsPermanently = 5
    }

    public interface IAnomalyElectronic
    {
        void ApplyAnomalyPowerState(
            bool powered,
            float duration,
            GameObject source);
    }

    [CreateAssetMenu(
        fileName = "Integration_NewAnomaly",
        menuName = "NERA/Combat/Anomaly Integration Definition")]
    public sealed class AnomalyIntegrationDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string integrationId;
        [SerializeField] private string displayName;
        [SerializeField] private Color displayColor = Color.white;

        [Header("Compatibility")]
        [SerializeField, FormerlySerializedAs("compatibleEquipment")]
        private List<ItemData> compatibleContainers =
            new List<ItemData>();

        [Header("Synthesis")]
        [SerializeField, Min(0.1f)] private float synthesisDuration = 5f;

        [Header("Activation")]
        [SerializeField] private AnomalyIntegrationEffect effect =
            AnomalyIntegrationEffect.DamageAnomalies;
        [SerializeField, Min(0.1f)] private float radius = 8f;
        [SerializeField, Min(0f)] private float anomalyDamage = 40f;
        [SerializeField, Min(0f)] private float electronicDuration = 5f;
        [SerializeField] private LayerMask affectedLayers = ~0;

        public string IntegrationId => integrationId;
        public string DisplayName => NERALocalization.Content(
            "integration", integrationId, "name", displayName);
        public Color DisplayColor => displayColor;
        public float SynthesisDuration => Mathf.Max(0.1f, synthesisDuration);
        public AnomalyIntegrationEffect Effect => effect;
        public int ChargesGranted => 1;
        public float Radius => Mathf.Max(0.1f, radius);
        public float AnomalyDamage => Mathf.Max(0f, anomalyDamage);
        public float ElectronicDuration => Mathf.Max(0f, electronicDuration);
        public float EffectDuration => Mathf.Max(0f, electronicDuration);
        public LayerMask AffectedLayers => affectedLayers;

        public bool Supports(ItemData container)
        {
            if (container == null ||
                container.ItemType != ItemType.AnomalyContainer ||
                !container.AcceptsAnomalyIntegration)
            {
                return false;
            }

            return compatibleContainers == null ||
                compatibleContainers.Count == 0 ||
                compatibleContainers.Contains(container);
        }

        private void OnValidate()
        {
            integrationId = integrationId?.Trim();
            displayName = displayName?.Trim();
            synthesisDuration = Mathf.Max(0.1f, synthesisDuration);
            radius = Mathf.Max(0.1f, radius);
            anomalyDamage = Mathf.Max(0f, anomalyDamage);
            electronicDuration = Mathf.Max(0f, electronicDuration);
        }
    }
}
