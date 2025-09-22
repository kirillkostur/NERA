using UnityEngine;

[CreateAssetMenu(fileName = "UpgradeConfig", menuName = "Game/Upgrade/Config")]
public class UpgradeConfig : ScriptableObject
{
    [System.Serializable]
    public class RequirementSlot
    {
        public InventoryItem item;              // предмет, который требуется
        [Min(1)] public int requiredCount = 1;  // сколько нужно
        public Sprite placeholderIcon;          // иконка-подсказка (полупрозрачная)
    }

    [System.Serializable]
    public class LevelRequirement
    {
        [Header("Слоты для перехода на следующий уровень")]
        public RequirementSlot[] slots;
    }

    [System.Serializable]
    public class Entry
    {
        [Header("Имя и иконка объекта")]
        public string displayName;
        public Sprite displayIcon;

        [Header("ID объекта (совпадает с ObjectLevelSwitch.upgradeID)")]
        public string targetID;

        [Header("Требования для каждого уровня (индекс = текущий уровень)")]
        public LevelRequirement[] levelRequirements;
    }

    [Header("Список объектов, доступных для апгрейда")]
    public Entry[] entries;
}
