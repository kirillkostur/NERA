using System;
using UnityEngine;

public static class GameEvents
{
    // Универсальное событие для квестов
    public static event Action<string, int> OnQuestEvent;

    public static void RaiseQuestEvent(string eventId, int amount = 1)
    {
        Debug.Log($"[GameEvents] QuestEvent: {eventId} (+{amount})");
        OnQuestEvent?.Invoke(eventId, amount);
    }

    // Когда квест стартует
    public static event Action<QuestAsset> OnQuestStarted;
    public static void RaiseQuestStarted(QuestAsset quest)
    {
        Debug.Log($"[GameEvents] QuestStarted: {quest.QuestID}");
        OnQuestStarted?.Invoke(quest);
    }

    // Когда квест завершён
    public static event Action<QuestAsset> OnQuestCompleted;
    public static void RaiseQuestCompleted(QuestAsset quest)
    {
        Debug.Log($"[GameEvents] QuestCompleted: {quest.QuestID}");
        OnQuestCompleted?.Invoke(quest);
    }
}
