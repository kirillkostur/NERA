using System;
using System.Collections.Generic;
using NERA.Items;
using UnityEngine;

namespace NERA.Inventory
{
    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private List<ItemData> items = new List<ItemData>();

        public event Action<ItemData> ItemAdded;

        public IReadOnlyList<ItemData> Items => items;
        public int Count => items.Count;

        public bool AddItem(ItemData item)
        {
            if (item == null)
                return false;

            items.Add(item);
            ItemAdded?.Invoke(item);

            Debug.Log(
                $"PlayerInventory: Added '{item.DisplayName}' ({item.ItemId}). Total: {items.Count}.",
                this
            );

            return true;
        }

        public bool Contains(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return false;

            foreach (ItemData item in items)
            {
                if (item != null &&
                    string.Equals(item.ItemId, itemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public void RestoreItems(IEnumerable<ItemData> restoredItems)
        {
            items.Clear();

            if (restoredItems == null)
                return;

            foreach (ItemData item in restoredItems)
            {
                if (item != null)
                    items.Add(item);
            }
        }
    }
}
