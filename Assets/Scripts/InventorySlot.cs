using UnityEngine;

[System.Serializable]
public class InventorySlot
{
    public InventoryItem item;
    public int count;

    public bool IsEmpty => item == null || count <= 0;

    public int FreeSpace => (item == null) ? 0 : Mathf.Max(0, item.maxStack - count);

    public void Clear() { item = null; count = 0; }
}
