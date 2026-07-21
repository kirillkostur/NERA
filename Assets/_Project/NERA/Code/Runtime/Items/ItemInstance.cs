using System;
using UnityEngine;

namespace NERA.Items
{
    [Serializable]
    public sealed class ItemInstance
    {
        [SerializeField] private string instanceId;
        [SerializeField] private ItemData itemData;
        [SerializeField] private float charge;

        public string InstanceId => instanceId;
        public ItemData ItemData => itemData;
        public float Charge => IsChargeable ? Mathf.Clamp(charge, 0f, MaxCharge) : 0f;
        public float MaxCharge => itemData?.EnergyDefinition?.Capacity ?? 0f;
        public float Charge01 => IsChargeable && MaxCharge > 0f ? Charge / MaxCharge : 1f;
        public bool IsChargeable => itemData != null && itemData.EnergyDefinition != null;
        public bool IsDepleted => IsChargeable && Charge <= 0.001f;
        public bool IsFullyCharged => !IsChargeable || Charge >= MaxCharge - 0.001f;

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
            if (data == null)
                return null;

            ItemInstance instance = new ItemInstance
            {
                instanceId = string.IsNullOrWhiteSpace(id)
                    ? Guid.NewGuid().ToString("N")
                    : id.Trim(),
                itemData = data
            };
            instance.SetCharge(savedCharge);
            return instance;
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
