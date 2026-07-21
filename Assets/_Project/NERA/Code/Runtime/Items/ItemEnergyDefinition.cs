using UnityEngine;

namespace NERA.Items
{
    [CreateAssetMenu(
        fileName = "ItemEnergy_New",
        menuName = "NERA/Items/Item Energy Definition"
    )]
    public sealed class ItemEnergyDefinition : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float capacity = 100f;
        [SerializeField, Min(0f)] private float initialCharge = 100f;
        [SerializeField, Min(0f)] private float energyPerUse = 10f;
        [SerializeField, Min(0.1f)] private float rechargePerSecond = 20f;

        public float Capacity => Mathf.Max(0.1f, capacity);
        public float InitialCharge => Mathf.Clamp(initialCharge, 0f, Capacity);
        public float EnergyPerUse => Mathf.Max(0f, energyPerUse);
        public float RechargePerSecond => Mathf.Max(0.1f, rechargePerSecond);

        private void OnValidate()
        {
            capacity = Mathf.Max(0.1f, capacity);
            initialCharge = Mathf.Clamp(initialCharge, 0f, capacity);
            energyPerUse = Mathf.Max(0f, energyPerUse);
            rechargePerSecond = Mathf.Max(0.1f, rechargePerSecond);
        }
    }
}
