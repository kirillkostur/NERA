using UnityEngine;

[CreateAssetMenu(fileName = "UpgradeConfig", menuName = "Game/Upgrade/Config")]
public class UpgradeConfig : ScriptableObject
{
    [System.Serializable]
    public class RequirementSlot
    {
        public InventoryItem item;
        [Min(1)] public int requiredCount = 1;
        public Sprite placeholderIcon;
    }

    [System.Serializable]
    public class Entry
    {
        [Header("Имя и иконка")]
        public string displayName;
        public Sprite displayIcon;

        [Header("ID объекта для апгрейда (совпадает с ObjectLevelSwitch.upgradeID)")]
        public string targetID;

        [Header("Требования")]
        public RequirementSlot[] slots;
    }

    [Header("Список объектов для апгрейда")]
    public Entry[] entries;
}
