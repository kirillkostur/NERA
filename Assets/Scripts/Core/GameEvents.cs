using System;

public static class GameEvents
{
    public static event Action<QuestEventData> OnQuestEventData;

    public static void RaiseQuestEvent(QuestEventData data)
    {
        OnQuestEventData?.Invoke(data);
    }

    // Для удобства оставляем старый метод как обёртку
    public static void RaiseQuestEvent(string id, int amount)
    {
        var data = new QuestEventData(QuestEventType.CollectItem, id, amount);
        RaiseQuestEvent(data);
    }

    public static void RaiseQuestStarted(QuestAsset quest) { /* ... */ }
    public static void RaiseQuestCompleted(QuestAsset quest) { /* ... */ }
}
