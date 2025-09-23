using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Quests/Quest Asset")]
public class QuestAsset : ScriptableObject
{
    [Header("Основное")]
    public string QuestID;
    public string Title;
    [TextArea] public string Description;

    [Header("Цели квеста")]
    public List<QuestObjective> Objectives = new();

    [Header("Автозапуск")]
    public bool StartOnSceneLoad = false;

    [Header("Зависимости")]
    public List<string> Prerequisites = new();
}
