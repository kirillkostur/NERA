using UnityEngine;

[CreateAssetMenu(menuName = "Configs/Loot Config")]
public class LootConfig : ScriptableObject
{
    [System.Serializable]
    public class LootEntry
    {
        public InventoryItem item;
        public int count;
    }

    public LootEntry[] lootItems;
}
