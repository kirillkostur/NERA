using NERA.Items;
using UnityEngine;

namespace NERA.Inventory
{
    /// <summary>
    /// Owns the visual selected through the player's quick-access slots.
    /// Behaviour for individual tools can live on their equipped prefabs.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerEquipmentController : MonoBehaviour
    {
        private const string DefaultAnchorName = "mixamorig1:RightHand";

        [SerializeField] private Transform equipmentAnchor;
        [SerializeField] private string fallbackAnchorName =
            DefaultAnchorName;

        private PlayerInventory inventory;
        private GameObject equippedVisual;

        public ItemData EquippedItem { get; private set; }
        public GameObject EquippedVisual => equippedVisual;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();

            if (equipmentAnchor == null)
                equipmentAnchor = FindChildRecursive(
                    transform,
                    fallbackAnchorName
                );
        }

        private void OnEnable()
        {
            if (inventory == null)
                inventory = GetComponent<PlayerInventory>();

            inventory.QuickAccessSelectionChanged +=
                HandleQuickAccessSelectionChanged;
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.QuickAccessSelectionChanged -=
                    HandleQuickAccessSelectionChanged;
            }
        }

        public void Equip(ItemData item)
        {
            ClearEquippedVisual();
            EquippedItem = item;

            if (item == null || item.EquippedVisualPrefab == null)
                return;

            if (equipmentAnchor == null)
            {
                Debug.LogWarning(
                    "PlayerEquipmentController: equipment anchor is missing.",
                    this
                );
                return;
            }

            equippedVisual = Instantiate(
                item.EquippedVisualPrefab,
                equipmentAnchor,
                false
            );
            equippedVisual.name = $"Equipped_{item.DisplayName}";
        }

        private void HandleQuickAccessSelectionChanged(
            int index,
            ItemData item
        )
        {
            Equip(item);
        }

        private void ClearEquippedVisual()
        {
            if (equippedVisual != null)
                Destroy(equippedVisual);

            equippedVisual = null;
            EquippedItem = null;
        }

        private static Transform FindChildRecursive(
            Transform root,
            string childName
        )
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
                return null;

            if (root.name == childName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform result = FindChildRecursive(
                    root.GetChild(i),
                    childName
                );
                if (result != null)
                    return result;
            }

            return null;
        }
    }
}
