using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInventory : MonoBehaviour
{
    [Header("Inventory")]
    [SerializeField] private int maxBackpackSlots = 5;

    private Inventory backpack;

    public Inventory Backpack => backpack;

    private void Awake()
    {
        backpack = new Inventory(maxBackpackSlots);

        Debug.Log($"PlayerInventory initialized. Slots: {maxBackpackSlots}");
    }

    public bool TryAddItem(ItemData itemData)
    {
        if (backpack == null)
        {
            Debug.LogError("PlayerInventory: Backpack is not initialized.");
            return false;
        }

        bool added = backpack.AddItem(itemData);

        if (added)
            backpack.PrintDebug();

        return added;
    }

    public bool HasFreeSlot()
    {
        return backpack != null && backpack.HasFreeSlot();
    }

    public void PrintInventory()
    {
        if (backpack == null)
        {
            Debug.LogWarning("PlayerInventory: Backpack is not initialized.");
            return;
        }

        backpack.PrintDebug();
    }
}