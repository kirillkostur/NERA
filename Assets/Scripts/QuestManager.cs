using System.Collections.Generic;
using UnityEngine;

public class QuestManager : MonoBehaviour
{
    [SerializeField] private List<QuestAsset> quests;

    private readonly Dictionary<string, QuestProgress> active = new();
    private readonly HashSet<string> completed = new();

    public static event System.Action OnQuestsUpdated;

    private void OnEnable()
    {
        GameEvents.OnQuestEvent += HandleQuestEvent;
    }

    private void OnDisable()
    {
        GameEvents.OnQuestEvent -= HandleQuestEvent;
    }

    private void Start()
    {
        // автозапуск квестов
        foreach (var quest in quests)
        {
            if (quest != null && quest.StartOnSceneLoad && CanStartQuest(quest))
                StartQuest(quest);
        }

        NotifyUpdate();
    }

    public IEnumerable<QuestProgress> GetActiveQuests() => active.Values;

    private bool CanStartQuest(QuestAsset quest)
    {
        if (quest == null) return false;
        if (completed.Contains(quest.QuestID) || active.ContainsKey(quest.QuestID)) return false;

        if (quest.Prerequisites != null)
        {
            foreach (var pre in quest.Prerequisites)
                if (!completed.Contains(pre)) return false;
        }
        return true;
    }

    private void StartQuest(QuestAsset quest)
    {
        if (quest == null) return;
        if (active.ContainsKey(quest.QuestID) || completed.Contains(quest.QuestID)) return;

        var progress = new QuestProgress(quest);
        active[quest.QuestID] = progress;

        GameEvents.RaiseQuestStarted(quest);
        Debug.Log($"[QUEST] Запущен: {quest.Title}");
    }

    public void StartQuestById(string questId)
    {
        var quest = quests.Find(q => q.QuestID == questId);
        if (quest != null && CanStartQuest(quest))
        {
            StartQuest(quest);
            NotifyUpdate();
        }
    }

    private void CompleteQuest(string questId)
    {
        if (!active.ContainsKey(questId)) return;

        var quest = active[questId].Asset;
        active.Remove(questId);
        completed.Add(questId);

        Debug.Log($"[QUEST] Завершён: {quest.Title}");
        GameEvents.RaiseQuestCompleted(quest);

        // после завершения — попробуем автостарт залежащих
        AutoStartDependentQuests(questId);
    }

    private void AutoStartDependentQuests(string justCompletedId)
    {
        foreach (var q in quests)
        {
            if (q == null) continue;
            if (active.ContainsKey(q.QuestID) || completed.Contains(q.QuestID)) continue;

            if (q.Prerequisites != null && q.Prerequisites.Count > 0)
            {
                bool allDone = true;
                foreach (var pre in q.Prerequisites)
                    if (!completed.Contains(pre)) { allDone = false; break; }

                if (allDone && CanStartQuest(q))
                    StartQuest(q);
            }
        }
    }

    private void HandleQuestEvent(string eventId, int amount)
    {
        bool anyChange = false;
        List<string> completedNow = new();

        foreach (var kv in active)
        {
            var progress = kv.Value;
            // UpdateProgress возвращает true, если изменился хоть один Objective
            if (progress.UpdateProgress(eventId, amount))
            {
                anyChange = true;
                if (progress.IsComplete)
                    completedNow.Add(progress.Asset.QuestID);
            }
        }

        // обновляем HUD сразу при любом изменении, не дожидаясь завершения
        if (anyChange) NotifyUpdate();

        // закрываем квесты
        if (completedNow.Count > 0)
        {
            foreach (var id in completedNow)
                CompleteQuest(id);

            // после автозапуска зависимых тоже обновим HUD
            NotifyUpdate();
        }
    }

    private void NotifyUpdate() => OnQuestsUpdated?.Invoke();
}
