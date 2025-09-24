using System.Collections.Generic;

[System.Serializable]
public class QuestProgress
{
    public string QuestID;
    public QuestAsset Asset;   // 👈 ссылка на сам квест-ассет
    public List<QuestObjective> Objectives = new List<QuestObjective>();

    public bool IsComplete
    {
        get
        {
            foreach (var obj in Objectives)
                if (!obj.IsComplete) return false;
            return true;
        }
    }

    public QuestProgress(QuestAsset asset)
    {
        Asset = asset;
        QuestID = asset.QuestID;

        // Копируем цели из ассета в прогресс
        Objectives = new List<QuestObjective>();
        foreach (var obj in asset.Objectives)
        {
            Objectives.Add(new QuestObjective
            {
                Type = obj.Type,
                TargetId = obj.TargetId,
                TargetValue = obj.TargetValue,
                DisplayText = obj.DisplayText,
                CurrentValue = 0
            });
        }
    }

    /// <summary>
    /// Обновляет прогресс по событию.
    /// Возвращает true, если что-то изменилось.
    /// </summary>
    public bool UpdateProgress(QuestEventData data)
    {
        bool changed = false;
        foreach (var obj in Objectives)
        {
            if (obj.UpdateProgress(data))
                changed = true;
        }
        return changed;
    }
}
