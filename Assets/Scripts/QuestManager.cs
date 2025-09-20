using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class QuestManager : MonoBehaviour
{
    [Header("UI Elements")]
    public RectTransform content;        // Контейнер для квестов
    public GameObject questItemPrefab;   // Префаб строки квеста

    private readonly List<string> activeQuests = new();
    private readonly List<GameObject> questItems = new();

    /// <summary>
    /// Добавить новый квест.
    /// </summary>
    public void AddQuest(string questName)
    {
        if (string.IsNullOrEmpty(questName)) return;
        if (activeQuests.Contains(questName)) return;

        activeQuests.Add(questName);
        RefreshUI();
    }

    /// <summary>
    /// Завершить квест и удалить из списка.
    /// </summary>
    public void CompleteQuest(string questName)
    {
        if (activeQuests.Remove(questName))
            RefreshUI();
    }

    private void RefreshUI()
    {
        // Очистить старые элементы
        foreach (var item in questItems)
            Destroy(item);
        questItems.Clear();

        // Создать новые
        foreach (var quest in activeQuests)
        {
            GameObject item = Instantiate(questItemPrefab, content);
            var text = item.GetComponent<TMP_Text>();
            if (text != null) text.text = "• " + quest;
            questItems.Add(item);
        }
    }
}

