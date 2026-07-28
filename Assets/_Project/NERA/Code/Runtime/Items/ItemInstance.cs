using System;
using NERA.Combat;
using UnityEngine;

namespace NERA.Items
{
    [Serializable]
    public sealed class ItemInstance
    {
        [SerializeField] private string instanceId;
        [SerializeField] private ItemData itemData;
        [SerializeField] private float charge;
        [SerializeField] private bool isScanned;
        [SerializeField] private ItemData integratedAnomaly;
        [SerializeField] private int anomalyCharges;

        public string InstanceId => instanceId;
        public ItemData ItemData => itemData;
        public float Charge => IsChargeable ? Mathf.Clamp(charge, 0f, MaxCharge) : 0f;
        public float MaxCharge => itemData?.EnergyDefinition?.Capacity ?? 0f;
        public float Charge01 => IsChargeable && MaxCharge > 0f ? Charge / MaxCharge : 1f;
        public bool IsChargeable => itemData != null && itemData.EnergyDefinition != null;
        public bool IsDepleted => IsChargeable && Charge <= 0.001f;
        public bool IsFullyCharged => !IsChargeable || Charge >= MaxCharge - 0.001f;
        public bool IsScanned => isScanned;
        public ItemData IntegratedAnomaly => integratedAnomaly;
        public AnomalyIntegrationDefinition AnomalyIntegration =>
            integratedAnomaly?.AnomalyIntegrationDefinition;
        public int AnomalyCharges => Mathf.Max(0, anomalyCharges);
        public bool HasAnomalyIntegration => AnomalyIntegration != null;
        public bool CanUseAnomalyIntegration =>
            HasAnomalyIntegration &&
            AnomalyCharges > 0 &&
            IsChargeable &&
            IsFullyCharged;

        private ItemInstance() { }

        public static ItemInstance Create(ItemData data)
        {
            if (data == null)
                return null;

            return Restore(
                Guid.NewGuid().ToString("N"),
                data,
                data.EnergyDefinition?.InitialCharge ?? 0f
            );
        }

        public static ItemInstance Restore(string id, ItemData data, float savedCharge)
        {
            return Restore(id, data, savedCharge, null, 0, false);
        }

        public static ItemInstance Restore(
            string id,
            ItemData data,
            float savedCharge,
            ItemData savedIntegratedAnomaly,
            int savedAnomalyCharges)
        {
            return Restore(
                id,
                data,
                savedCharge,
                savedIntegratedAnomaly,
                savedAnomalyCharges,
                false);
        }

        public static ItemInstance Restore(
            string id,
            ItemData data,
            float savedCharge,
            ItemData savedIntegratedAnomaly,
            int savedAnomalyCharges,
            bool savedIsScanned)
        {
            if (data == null)
                return null;

            ItemInstance instance = new ItemInstance
            {
                instanceId = string.IsNullOrWhiteSpace(id)
                    ? Guid.NewGuid().ToString("N")
                    : id.Trim(),
                itemData = data,
                integratedAnomaly =
                    savedIntegratedAnomaly?.AnomalyIntegrationDefinition != null
                        ? savedIntegratedAnomaly
                        : null,
                anomalyCharges = Mathf.Max(0, savedAnomalyCharges),
                isScanned = savedIsScanned
            };
            instance.SetCharge(savedCharge);
            if (instance.integratedAnomaly == null)
                instance.anomalyCharges = 0;
            return instance;
        }

        public bool MarkScanned()
        {
            if (isScanned)
                return false;

            isScanned = true;
            return true;
        }

        public bool TryInstallAnomaly(ItemInstance anomalyInstance)
        {
            ItemData anomaly = anomalyInstance?.ItemData;
            AnomalyIntegrationDefinition definition =
                anomaly?.AnomalyIntegrationDefinition;
            if (itemData?.ItemType != ItemType.Equipment ||
                !itemData.AcceptsAnomalyIntegration ||
                !IsChargeable ||
                !IsFullyCharged ||
                HasAnomalyIntegration ||
                anomalyInstance?.IsScanned != true ||
                definition == null ||
                !definition.Supports(itemData))
            {
                return false;
            }

            integratedAnomaly = anomaly;
            anomalyCharges = definition.ChargesGranted;
            return true;
        }

        public bool TryConsumeAnomalyCharge()
        {
            if (!CanUseAnomalyIntegration)
                return false;

            charge = 0f;
            integratedAnomaly = null;
            anomalyCharges = 0;
            return true;
        }

        public bool CanConsume(float amount)
        {
            return !IsChargeable || amount <= 0f || Charge + 0.001f >= amount;
        }

        public bool TryConsume(float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (!CanConsume(amount))
                return false;

            if (IsChargeable)
                charge = Mathf.Max(0f, Charge - amount);

            return true;
        }

        public float Recharge(float amount)
        {
            if (!IsChargeable || amount <= 0f || IsFullyCharged)
                return 0f;

            float previous = Charge;
            charge = Mathf.Min(MaxCharge, previous + amount);
            return charge - previous;
        }

        public void SetCharge(float value)
        {
            charge = IsChargeable ? Mathf.Clamp(value, 0f, MaxCharge) : 0f;
        }
    }
}
