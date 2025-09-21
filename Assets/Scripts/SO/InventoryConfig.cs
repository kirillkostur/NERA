using UnityEngine;

[CreateAssetMenu(fileName = "InventoryConfig", menuName = "Game/Inventory/Config")]
public class InventoryConfig : ScriptableObject
{
    [Header("Количество слотов инвентаря")]
    [Min(1)] public int slotCount = 15;

    [System.Serializable]
    public class Entry
    {
        public InventoryItem item;
        [Min(1)] public int count = 1;
    }

    [Header("Стартовые предметы (опционально)")]
    public Entry[] startItems;
}
