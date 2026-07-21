using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.Items
{
    [CreateAssetMenu(
        fileName = "ItemCatalog_Default",
        menuName = "NERA/Items/Item Catalog"
    )]
    public sealed class ItemCatalogData : ScriptableObject
    {
        [SerializeField] private List<ItemData> items = new List<ItemData>();

        public IReadOnlyList<ItemData> Items => items;

        public ItemData Find(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
                return null;

            foreach (ItemData item in items)
            {
                if (item != null && string.Equals(
                        item.ItemId,
                        itemId,
                        StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }
    }
}
