using System;
using System.Collections.Generic;
using NERA.Items;
using UnityEngine;

namespace NERA.Inventory
{
    /// <summary>
    /// Keeps quick-access items visually equipped and routes input from any
    /// quick-access item that owns the pressed key.
    /// </summary>
    [RequireComponent(typeof(PlayerInventory))]
    public sealed class PlayerEquipmentController : MonoBehaviour
    {
        private const string DefaultAnchorName = "mixamorig1:RightHand";

        [SerializeField] private string fallbackAnchorName = DefaultAnchorName;

        private readonly Dictionary<int, GameObject> equippedVisuals =
            new Dictionary<int, GameObject>();
        private PlayerInventory inventory;

        public event Func<ItemInstance, QuickAccessAction, bool> EquipmentUseRequested;

        public ItemData EquippedItem => FindPreferredQuickAccessItem();
        public bool HasEquippedWeapon => FindEquippedWeapon() != null;
        public GameObject EquippedVisual
        {
            get
            {
                int index = FindPreferredQuickAccessIndex();
                return index >= 0 &&
                       equippedVisuals.TryGetValue(index, out GameObject visual)
                    ? visual
                    : null;
            }
        }

        public IReadOnlyDictionary<int, GameObject> EquippedVisuals =>
            equippedVisuals;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
        }

        private void OnEnable()
        {
            if (inventory == null)
                inventory = GetComponent<PlayerInventory>();

            inventory.InventoryChanged += RefreshEquippedVisuals;
            RefreshEquippedVisuals();
        }

        private void OnDisable()
        {
            if (inventory != null)
            {
                inventory.InventoryChanged -= RefreshEquippedVisuals;
            }

            ClearEquippedVisuals();
        }

        private void Update()
        {
            if (inventory == null || Cursor.lockState != CursorLockMode.Locked)
                return;

            ItemInstance instance = FindPressedQuickAccessItem();
            TryUseItem(instance);
        }

        public bool TryUseItem(ItemInstance instance)
        {
            if (inventory == null)
                inventory = GetComponent<PlayerInventory>();

            ItemData item = instance?.ItemData;
            if (inventory == null || !CanUseItem(item))
                return false;

            float energyCost = item.EnergyDefinition?.EnergyPerUse ?? 0f;
            if (!instance.CanConsume(energyCost))
                return false;

            bool actionSucceeded = item.QuickAccessAction != QuickAccessAction.Fire;
            if (EquipmentUseRequested != null)
            {
                foreach (Delegate callback in EquipmentUseRequested.GetInvocationList())
                {
                    actionSucceeded |= ((Func<ItemInstance, QuickAccessAction, bool>)callback)(
                        instance,
                        item.QuickAccessAction
                    );
                }
            }

            if (!actionSucceeded || !inventory.TryConsumeCharge(instance, energyCost))
                return false;

            Debug.Log(
                $"Equipment: {item.DisplayName} used " +
                $"({item.QuickAccessAction}). Charge: " +
                $"{instance.Charge:0.##}/{instance.MaxCharge:0.##}.",
                this
            );
            return true;
        }

        private ItemInstance FindPressedQuickAccessItem()
        {
            ItemInstance fallbackItem = null;

            for (int i = 0; i < inventory.QuickAccessItemInstances.Count; i++)
            {
                if (!PlayerInventory.IsActiveQuickAccessSlot(i))
                    continue;

                ItemInstance instance = inventory.QuickAccessItemInstances[i];
                ItemData item = instance?.ItemData;
                if (!CanUseItem(item) || !Input.GetKeyDown(item.UseKey))
                    continue;

                if (item.QuickAccessAction == QuickAccessAction.Fire)
                    return instance;

                fallbackItem ??= instance;
            }

            return fallbackItem;
        }

        private ItemData FindPreferredQuickAccessItem()
        {
            int index = FindPreferredQuickAccessIndex();
            return inventory != null && index >= 0
                ? inventory.QuickAccessItemInstances[index]?.ItemData
                : null;
        }

        private ItemData FindEquippedWeapon()
        {
            if (inventory == null)
                return null;

            for (int i = 0; i < inventory.QuickAccessItemInstances.Count; i++)
            {
                if (!PlayerInventory.IsActiveQuickAccessSlot(i))
                    continue;

                ItemData item = inventory.QuickAccessItemInstances[i]?.ItemData;
                if (item != null &&
                    item.QuickAccessAction == QuickAccessAction.Fire &&
                    item.WeaponDefinition != null)
                {
                    return item;
                }
            }

            return null;
        }

        private int FindPreferredQuickAccessIndex()
        {
            if (inventory == null)
                return -1;

            int fallbackIndex = -1;

            for (int i = 0; i < inventory.QuickAccessItemInstances.Count; i++)
            {
                if (!PlayerInventory.IsActiveQuickAccessSlot(i))
                    continue;

                ItemData item = inventory.QuickAccessItemInstances[i]?.ItemData;
                if (!CanUseItem(item))
                    continue;

                if (item.QuickAccessAction == QuickAccessAction.Fire)
                    return i;

                if (fallbackIndex < 0)
                    fallbackIndex = i;
            }

            return fallbackIndex;
        }

        private static bool CanUseItem(ItemData item)
        {
            return item != null &&
                   item.QuickAccessAction != QuickAccessAction.None;
        }

        private void RefreshEquippedVisuals()
        {
            ClearEquippedVisuals();
            if (inventory == null)
                return;

            for (int index = 0; index < inventory.QuickAccessItemInstances.Count; index++)
            {
                if (!PlayerInventory.IsActiveQuickAccessSlot(index))
                    continue;

                ItemData item = inventory.QuickAccessItemInstances[index]?.ItemData;
                if (item == null || item.EquippedVisualPrefab == null)
                    continue;

                Transform anchor = FindChildRecursive(
                    transform,
                    string.IsNullOrWhiteSpace(item.EquipmentAnchorName)
                        ? fallbackAnchorName
                        : item.EquipmentAnchorName
                );
                if (anchor == null)
                {
                    Debug.LogWarning(
                        $"PlayerEquipmentController: anchor '{item.EquipmentAnchorName}' " +
                        $"was not found for '{item.DisplayName}'.",
                        this
                    );
                    continue;
                }

                GameObject visual = Instantiate(
                    item.EquippedVisualPrefab,
                    anchor,
                    false
                );
                visual.name = $"Equipped_{index + 1}_{item.DisplayName}";
                visual.transform.localPosition = item.EquippedLocalPosition;
                visual.transform.localRotation = Quaternion.Euler(
                    item.EquippedLocalEulerAngles
                );
                equippedVisuals[index] = visual;
            }
        }

        private void ClearEquippedVisuals()
        {
            foreach (GameObject visual in equippedVisuals.Values)
            {
                if (visual != null)
                    Destroy(visual);
            }

            equippedVisuals.Clear();
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
