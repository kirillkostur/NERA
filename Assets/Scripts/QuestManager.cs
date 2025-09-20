using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class QuestManager : MonoBehaviour
{
    [Header("Конфиг")]
    public QuestsConfig config;

    [Header("HUD / UI")]
    public RectTransform content;        // Контейнер ScrollView/Content
    public GameObject questItemPrefab;   // Префаб TMP_Text

    private readonly Dictionary<string, int> progress = new();          // questID -> текущий счёт
    private readonly Dictionary<string, TMP_Text> questUI = new();      // questID -> UI элемент
    private readonly HashSet<string> completed = new();

    private int currentQuestIndex = 0;
    private string currentQuestID;

    private void OnEnable()
    {
        GameEvents.BatteryStarted += OnBatteryStarted;
        GameEvents.ObjectRepaired += OnObjectRepaired;
        GameEvents.SpiderKilled += OnSpiderKilled;
    }

    private void OnDisable()
    {
        GameEvents.BatteryStarted -= OnBatteryStarted;
        GameEvents.ObjectRepaired -= OnObjectRepaired;
        GameEvents.SpiderKilled -= OnSpiderKilled;
    }

    private void Start()
    {
        ActivateNextQuest();
    }

    private void ActivateNextQuest()
    {
        // Проверяем, есть ли ещё квесты
        if (config == null || config.quests == null || currentQuestIndex >= config.quests.Length)
        {
            currentQuestID = null;
            return;
        }

        var quest = config.quests[currentQuestIndex];
        if (quest == null)
        {
            currentQuestIndex++;
            ActivateNextQuest();
            return;
        }

        currentQuestID = quest.questID;
        progress[currentQuestID] = 0;

        // Создаём UI для нового квеста
        var item = Instantiate(questItemPrefab, content);
        var text = item.GetComponent<TMP_Text>();
        if (text == null) text = item.AddComponent<TextMeshProUGUI>();
        questUI[currentQuestID] = text;

        UpdateUI(currentQuestID);
    }

    private void CompleteQuest(string questID)
    {
        if (completed.Contains(questID)) return;
        completed.Add(questID);

        // Удаляем из UI
        if (questUI.TryGetValue(questID, out var txt))
        {
            Destroy(txt.gameObject);
            questUI.Remove(questID);
        }

        // Переходим к следующему
        currentQuestIndex++;
        ActivateNextQuest();
    }

    private void IncrementProgress(string questID, int amount)
    {
        if (string.IsNullOrEmpty(questID) || !progress.ContainsKey(questID)) return;

        progress[questID] = Mathf.Clamp(progress[questID] + amount, 0, GetQuest(questID).requiredCount);

        if (progress[questID] >= GetQuest(questID).requiredCount)
        {
            CompleteQuest(questID);
        }
        else
        {
            UpdateUI(questID);
        }
    }

    private QuestData GetQuest(string questID)
    {
        foreach (var q in config.quests)
        {
            if (q != null && q.questID == questID) return q;
        }
        return null;
    }

    private void UpdateUI(string questID)
    {
        if (!questUI.TryGetValue(questID, out var text)) return;

        var quest = GetQuest(questID);
        if (quest == null) return;

        int cur = progress.TryGetValue(questID, out var p) ? p : 0;
        int req = Mathf.Max(1, quest.requiredCount);

        text.text = $"• {quest.questName} <size=80%><color=#CCCCCC>({cur}/{req})</color></size>";
    }

    // === Обработчики событий ===
    private void OnBatteryStarted()
    {
        if (currentQuestID == null) return;
        var quest = GetQuest(currentQuestID);
        if (quest != null && quest.conditionType == QuestConditionType.BatteryStarted)
        {
            IncrementProgress(currentQuestID, 1);
        }
    }

    private void OnObjectRepaired(RepairableObject obj)
    {
        if (currentQuestID == null || obj == null) return;
        var quest = GetQuest(currentQuestID);
        if (quest != null &&
            quest.conditionType == QuestConditionType.ObjectRepairedByName &&
            quest.targetObjectName == obj.objectName)
        {
            IncrementProgress(currentQuestID, 1);
        }
    }

    private void OnSpiderKilled(SpiderHealth _)
    {
        if (currentQuestID == null) return;
        var quest = GetQuest(currentQuestID);
        if (quest != null && quest.conditionType == QuestConditionType.SpidersKilled)
        {
            IncrementProgress(currentQuestID, 1);
        }
    }
}
