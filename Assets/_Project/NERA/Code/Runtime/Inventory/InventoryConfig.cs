using UnityEngine;

namespace NERA.Inventory
{
    [CreateAssetMenu(
        fileName = "InventoryConfig",
        menuName = "NERA/Inventory/Inventory Config"
    )]
    public sealed class InventoryConfig : ScriptableObject
    {
        public const int DefaultBackpackCapacity = 5;
        public const int MaxBackpackCapacity = 12;
        public const string DefaultResourcesPath =
            "Inventory/DefaultInventoryConfig";

        [Header("Capacity")]
        [SerializeField, Range(1, MaxBackpackCapacity)] private int backpackCapacity =
            DefaultBackpackCapacity;

        [Header("UI")]
        [SerializeField] private InventorySlotView slotPrefab;

        public int BackpackCapacity => Mathf.Clamp(
            backpackCapacity,
            1,
            MaxBackpackCapacity
        );
        public InventorySlotView SlotPrefab => slotPrefab;

        public static InventoryConfig Resolve(InventoryConfig assigned)
        {
            return assigned != null
                ? assigned
                : Resources.Load<InventoryConfig>(DefaultResourcesPath);
        }
    }
}
