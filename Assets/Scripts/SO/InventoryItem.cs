using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Game/Inventory/Item")]
public class InventoryItem : ScriptableObject
{
    [Header("ID и вид")]
    public int ID;
    public string itemName;
    public Sprite icon;

    [Header("Стек")]
    public int maxStack = 99;
}
