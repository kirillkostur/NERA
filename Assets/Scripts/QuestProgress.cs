using System.Collections.Generic;

public class QuestProgress
{
    public QuestAsset Asset { get; private set; }
    public List<QuestObjective> Objectives { get; private set; }

    public bool IsComplete
    {
        get
        {
            foreach (var obj in Objectives)
            {
                if (!obj.IsComplete)
                    return false;
            }
            return true;
        }
    }

    public QuestProgress(QuestAsset asset)
    {
        Asset = asset;
        // копия целей для отслеживания прогресса
        Objectives = new List<QuestObjective>();
        foreach (var obj in asset.Objectives)
        {
            var copy = new QuestObjective
            {
                Type = obj.Type,
                TargetID = obj.TargetID,
                TargetValue = obj.TargetValue,
                DisplayText = obj.DisplayText,
                CurrentValue = 0
            };
            Objectives.Add(copy);
        }
    }

    public bool UpdateProgress(string eventId, int amount)
    {
        bool changed = false;
        foreach (var obj in Objectives)
        {
            if (obj.UpdateProgress(eventId, amount))
                changed = true;
        }
        return changed;
    }
}
