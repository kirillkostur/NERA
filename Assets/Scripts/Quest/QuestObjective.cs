using UnityEngine;

[System.Serializable]
public class QuestObjective
{
    public QuestEventType Type;
    public string TargetId;        // 👈 единый стиль (Id)
    public int TargetValue = 1;
    public string DisplayText;

    [HideInInspector] public int CurrentValue = 0;

    public bool IsComplete => CurrentValue >= TargetValue;

    /// <summary>
    /// Обновляет прогресс по событию.
    /// Возвращает true, если прогресс изменился.
    /// </summary>
    public bool UpdateProgress(QuestEventData data)
    {
        if (IsComplete) return false;
        if (data.Type != Type) return false;
        if (!string.IsNullOrEmpty(TargetId) && data.TargetId != TargetId) return false;

        if (Type == QuestEventType.UpgradeObj)
        {
            // Прогресс = текущий уровень объекта
            CurrentValue = data.Amount;
            if (CurrentValue >= TargetValue)
                CurrentValue = TargetValue;
        }
        else
        {
            // Прогресс += количество
            CurrentValue += data.Amount;
            if (CurrentValue > TargetValue)
                CurrentValue = TargetValue;
        }

        return true;
    }
}
