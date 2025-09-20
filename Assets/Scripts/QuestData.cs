using UnityEngine;

public enum QuestConditionType
{
    BatteryStarted,          // Запустить аккумулятор (однократно)
    ObjectRepairedByName,    // Отремонтировать объект с указанным именем (objectName из RepairableObject)
    SpidersKilled,           // Убить N пауков
    Custom                   // Ручной ключ-событие (на будущее)
}

[CreateAssetMenu(fileName = "QuestData", menuName = "Game/Quest", order = 1)]
public class QuestData : ScriptableObject
{
    [Header("Идентификатор и название")]
    public string questID = "launch_battery";
    public string questName = "Запустить аккумулятор";
    [TextArea] public string description = "Отремонтируй и запусти главный аккумулятор станции.";

    [Header("Условия")]
    public QuestConditionType conditionType = QuestConditionType.BatteryStarted;
    public string targetObjectName;   // для ObjectRepairedByName (сравнивается с RepairableObject.objectName)
    public int requiredCount = 1;     // например, убить 5 пауков

    [Header("Награды (по желанию)")]
    public int rewardXP = 0;
    public int rewardCredits = 0;
}
