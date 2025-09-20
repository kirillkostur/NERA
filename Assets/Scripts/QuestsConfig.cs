using UnityEngine;

[CreateAssetMenu(fileName = "QuestsConfig", menuName = "Game/QuestsConfig", order = 2)]
public class QuestsConfig : ScriptableObject
{
    public QuestData[] quests;
}
