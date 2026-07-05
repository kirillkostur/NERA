using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class Inventory
{
    [SerializeField] private int maxSlots = 5;
    [SerializeField] private List<ItemData> items = new List<ItemData>();

    public int MaxSlots => maxSlots;
    public int Count => items.Count;
    public IReadOnlyList<ItemData> Items => items;

    public Inventory(int maxSlots)
    {
        this.maxSlots = Mathf.Max(1, maxSlots);
        items = new List<ItemData>(this.maxSlots);
    }

    public bool HasFreeSlot()
    {
        return items.Count < maxSlots;
    }

    public bool AddItem(ItemData itemData)
    {
        if (itemData == null)
        {
            Debug.LogError("Inventory: Cannot add null item.");
            return false;
        }

        if (!HasFreeSlot())
        {
            Debug.LogWarning($"Inventory: No free slots. Cannot add '{itemData.GetItemName()}'.");
            return false;
        }

        items.Add(itemData);

        Debug.Log($"Inventory: Added item '{itemData.GetItemName()}'. Slots: {items.Count}/{maxSlots}");

        return true;
    }

    public bool ContainsItem(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return false;

        for (int i = 0; i < items.Count; i++)
        {
            if (items[i] != null && items[i].ItemId == itemId)
                return true;
        }

        return false;
    }

    public void PrintDebug()
    {
        Debug.Log($"Inventory: {items.Count}/{maxSlots} items.");

        for (int i = 0; i < items.Count; i++)
        {
            ItemData item = items[i];

            if (item == null)
            {
                Debug.Log($"Slot {i + 1}: Empty / Missing ItemData");
                continue;
            }

            Debug.Log($"Slot {i + 1}: {item.GetItemName()} [{item.ItemType}]");
        }
    }
}