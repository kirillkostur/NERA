using UnityEngine;

[System.Serializable]
public class QuestObjective
{
    public QuestEventType Type;
    public string TargetID;
    public int TargetValue = 1;
    public string DisplayText;

    [HideInInspector] public int CurrentValue = 0;

    public bool IsComplete => CurrentValue >= TargetValue;

    public bool UpdateProgress(string eventId, int amount)
    {
        if (eventId == TargetID)
        {
            CurrentValue += amount;
            if (CurrentValue > TargetValue)
                CurrentValue = TargetValue;
            return true;
        }
        return false;
    }
}
