public struct QuestEventData
{
    public QuestEventType Type;   // Тип события (RepairObject, StartBattery, CollectItem, UpgradeTurret и т.п.)
    public string TargetId;       // ID объекта/предмета
    public int Amount;            // Кол-во (например, сколько предметов собрали)

    public QuestEventData(QuestEventType type, string targetId, int amount = 1)
    {
        Type = type;
        TargetId = targetId;
        Amount = amount;
    }
}
