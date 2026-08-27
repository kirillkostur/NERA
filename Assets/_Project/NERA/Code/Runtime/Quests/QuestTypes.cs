using System;
using System.Collections.Generic;
using NERA.Localization;

namespace NERA.Quests
{
    public enum QuestCategory
    {
        Main,
        Side
    }

    public enum QuestAvailability
    {
        Once,
        Repeatable
    }

    public enum QuestTargetScope
    {
        Single,
        PerTriggeringObject
    }

    public enum QuestConditionTarget
    {
        SpecificObject,
        AnyObject,
        QuestTarget
    }

    public enum QuestConditionLogic
    {
        All,
        Any
    }

    public enum QuestConditionEvaluation
    {
        Event,
        CurrentState
    }

    public enum QuestValueComparison
    {
        Less,
        LessOrEqual,
        Equal,
        GreaterOrEqual,
        Greater
    }

    public enum QuestSignalType
    {
        LocationDiscovered,
        LocationEntered,
        AreaExplored,
        ItemCollected,
        ResearchAnalyzed,
        EnemyEncountered,
        EnemyKilled,
        DeviceConditionBelow,
        DeviceConditionRestored,
        StationFaultStarted,
        StationSystemActivated,
        QuestCompleted,
        LocationExited,
        ObjectInteractionCompleted,
        ItemRemoved,
        ItemDelivered,
        InventoryItemCountChanged,
        StationSystemDeactivated,
        StationSystemUpgraded,
        StationPowerOnline,
        StationPowerOffline,
        EnergyChargeChanged,
        StationFaultResolved,
        StationAttackStarted,
        StationAttackRepelled,
        DroneScanCompleted,
        AntennaSignalFound,
        WeatherChanged,
        TimerElapsed,
        Custom,
        EnemyWaveSpawned,
        EnemyWaveCleared
    }

    public enum QuestWeatherAction
    {
        None,
        StartSandstorm,
        StopSandstorm
    }

    public readonly struct QuestSignal
    {
        public QuestSignal(
            QuestSignalType type,
            string targetId,
            string targetName = null,
            int amount = 1,
            float value = 0f,
            string cause = null)
        {
            Type = type;
            TargetId = QuestIdUtility.Normalize(targetId);
            TargetName = string.IsNullOrWhiteSpace(targetName)
                ? TargetId
                : targetName.Trim();
            Amount = Math.Max(1, amount);
            Value = value;
            Cause = QuestIdUtility.Normalize(cause);
        }

        public QuestSignalType Type { get; }
        public string TargetId { get; }
        public string TargetName { get; }
        public int Amount { get; }
        public float Value { get; }
        public string Cause { get; }
    }

    [Serializable]
    public sealed class QuestInstanceSaveData
    {
        public string instanceId;
        public string questId;
        public string contextTargetId;
        public string contextTargetName;
        public int currentStageIndex;
        public List<int> conditionProgress = new List<int>();
    }

    [Serializable]
    public sealed class QuestHistorySaveData
    {
        public string instanceId;
        public string questId;
        public string contextTargetId;
        public string contextTargetName;
        public int completionCount;
    }

    [Serializable]
    public sealed class QuestActivationSaveData
    {
        public string activationId;
        public string questId;
        public string contextTargetId;
        public string contextTargetName;
        public List<int> conditionProgress = new List<int>();
    }

    public sealed class QuestRuntimeState
    {
        private int[] conditionProgress;

        internal QuestRuntimeState(
            QuestDefinition definition,
            string instanceId,
            string contextTargetId,
            string contextTargetName,
            int currentStageIndex = 0,
            IReadOnlyList<int> restoredProgress = null)
        {
            Definition = definition;
            InstanceId = QuestIdUtility.Normalize(instanceId);
            ContextTargetId = QuestIdUtility.Normalize(contextTargetId);
            ContextTargetName = string.IsNullOrWhiteSpace(contextTargetName)
                ? ContextTargetId
                : contextTargetName.Trim();
            CurrentStageIndex = Math.Max(0, currentStageIndex);
            ResetConditionProgress(restoredProgress);
        }

        public QuestDefinition Definition { get; }
        public string InstanceId { get; }
        public string QuestId => Definition != null
            ? Definition.QuestId
            : string.Empty;
        public string ContextTargetId { get; }
        public string ContextTargetName { get; }
        public int CurrentStageIndex { get; private set; }
        public QuestStageDefinition CurrentStage =>
            Definition != null &&
            CurrentStageIndex >= 0 &&
            CurrentStageIndex < Definition.Stages.Count
                ? Definition.Stages[CurrentStageIndex]
                : null;
        public string Title => FormatText(NERALocalization.Quest(
            QuestId,
            "title",
            Definition?.Title));
        public string Description => FormatText(NERALocalization.Quest(
            QuestId,
            "description",
            Definition?.Description));
        public string ObjectiveTitle => FormatText(NERALocalization.Quest(
            QuestId,
            $"stage.{CurrentStageIndex + 1:00}.title",
            CurrentStage?.Title));
        public string ObjectiveDescription => FormatText(NERALocalization.Quest(
            QuestId,
            $"stage.{CurrentStageIndex + 1:00}.description",
            CurrentStage?.Description));
        public IReadOnlyList<int> ConditionProgress => conditionProgress;

        internal int GetConditionProgress(int index)
        {
            return index >= 0 && index < conditionProgress.Length
                ? conditionProgress[index]
                : 0;
        }

        internal bool AddConditionProgress(
            int index,
            int amount,
            int requiredCount)
        {
            if (index < 0 || index >= conditionProgress.Length)
                return false;

            int next = Math.Min(
                Math.Max(1, requiredCount),
                conditionProgress[index] + Math.Max(1, amount));
            if (next == conditionProgress[index])
                return false;

            conditionProgress[index] = next;
            return true;
        }

        internal bool SetConditionProgress(
            int index,
            bool complete,
            int requiredCount)
        {
            if (index < 0 || index >= conditionProgress.Length)
                return false;

            int next = complete ? Math.Max(1, requiredCount) : 0;
            if (conditionProgress[index] == next)
                return false;

            conditionProgress[index] = next;
            return true;
        }

        internal bool IsStageComplete()
        {
            QuestStageDefinition stage = CurrentStage;
            if (stage == null || stage.CompletionConditions.Count == 0)
                return false;

            bool anyComplete = false;
            for (int index = 0;
                 index < stage.CompletionConditions.Count;
                 index++)
            {
                bool complete = GetConditionProgress(index) >=
                    stage.CompletionConditions[index].RequiredCount;
                if (stage.CompletionLogic == QuestConditionLogic.All &&
                    !complete)
                {
                    return false;
                }

                anyComplete |= complete;
            }

            return stage.CompletionLogic == QuestConditionLogic.All ||
                anyComplete;
        }

        internal bool AdvanceStage()
        {
            CurrentStageIndex++;
            if (Definition == null ||
                CurrentStageIndex >= Definition.Stages.Count)
            {
                conditionProgress = Array.Empty<int>();
                return false;
            }

            ResetConditionProgress();
            return true;
        }

        internal QuestInstanceSaveData Capture()
        {
            QuestInstanceSaveData saved = new QuestInstanceSaveData
            {
                instanceId = InstanceId,
                questId = QuestId,
                contextTargetId = ContextTargetId,
                contextTargetName = ContextTargetName,
                currentStageIndex = CurrentStageIndex
            };
            saved.conditionProgress.AddRange(conditionProgress);
            return saved;
        }

        private void ResetConditionProgress(
            IReadOnlyList<int> restoredProgress = null)
        {
            int count = CurrentStage?.CompletionConditions.Count ?? 0;
            conditionProgress = new int[count];
            if (restoredProgress == null)
                return;

            int restoredCount = Math.Min(count, restoredProgress.Count);
            for (int index = 0; index < restoredCount; index++)
            {
                int required =
                    CurrentStage.CompletionConditions[index].RequiredCount;
                conditionProgress[index] = Math.Min(
                    required,
                    Math.Max(0, restoredProgress[index]));
            }
        }

        private string FormatText(string value)
        {
            string localizedTargetName = NERALocalization.Content(
                "target",
                ContextTargetId,
                "name",
                ContextTargetName);
            return (value ?? string.Empty)
                .Replace("{targetId}", ContextTargetId)
                .Replace("{targetName}", localizedTargetName);
        }
    }

    internal static class QuestIdUtility
    {
        public static string Normalize(string value)
        {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        public static bool Equals(string left, string right)
        {
            return string.Equals(
                Normalize(left),
                Normalize(right),
                StringComparison.Ordinal);
        }
    }
}
