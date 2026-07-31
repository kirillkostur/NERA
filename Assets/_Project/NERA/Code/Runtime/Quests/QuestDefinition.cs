using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.Quests
{
    [Serializable]
    public sealed class QuestConditionDefinition
    {
        [SerializeField] private QuestSignalType signalType;
        [SerializeField] private QuestConditionTarget target =
            QuestConditionTarget.SpecificObject;
        [Tooltip("Stable ID of the object required by this condition.")]
        [SerializeField] private string targetId = "*";
        [Tooltip("Optional cause filter, for example EnemySabotage.")]
        [SerializeField] private string cause;
        [SerializeField, Min(1)] private int requiredCount = 1;
        [Tooltip("Used by DeviceConditionBelow and DeviceConditionRestored.")]
        [SerializeField, Range(0f, 1f)] private float threshold = 0.5f;

        public QuestSignalType SignalType => signalType;
        public QuestConditionTarget Target => target;
        public string TargetId => targetId?.Trim() ?? string.Empty;
        public string Cause => cause?.Trim() ?? string.Empty;
        public int RequiredCount => Mathf.Max(1, requiredCount);
        public float Threshold => Mathf.Clamp01(threshold);

        internal bool Matches(
            QuestSignal signal,
            string contextTargetId)
        {
            if (signal.Type != signalType)
                return false;

            if (target == QuestConditionTarget.QuestTarget)
            {
                string normalizedContext =
                    QuestIdUtility.Normalize(contextTargetId);
                if (string.IsNullOrEmpty(normalizedContext) ||
                    !QuestIdUtility.Equals(signal.TargetId, normalizedContext))
                {
                    return false;
                }
            }
            else if (target == QuestConditionTarget.SpecificObject &&
                     !QuestIdUtility.Equals(signal.TargetId, TargetId))
            {
                return false;
            }

            string expectedCause = QuestIdUtility.Normalize(Cause);
            if (signalType == QuestSignalType.StationFaultStarted &&
                !string.IsNullOrEmpty(expectedCause) &&
                !QuestIdUtility.Equals(signal.Cause, expectedCause))
            {
                return false;
            }

            return signalType switch
            {
                QuestSignalType.DeviceConditionBelow =>
                    signal.Value <= Threshold,
                QuestSignalType.DeviceConditionRestored =>
                    signal.Value >= Threshold,
                _ => true
            };
        }

        internal bool TryValidate(
            bool activationCondition,
            QuestTargetScope questTargetScope,
            out string error)
        {
            if (target == QuestConditionTarget.SpecificObject &&
                string.IsNullOrWhiteSpace(TargetId))
            {
                error = "a specific object condition has no Target ID";
                return false;
            }

            if (target == QuestConditionTarget.QuestTarget &&
                questTargetScope != QuestTargetScope.PerTriggeringObject)
            {
                error = "Quest Target can only be used by a per-object quest";
                return false;
            }

            if (activationCondition &&
                target == QuestConditionTarget.QuestTarget)
            {
                error = "an activation condition cannot use Quest Target";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class QuestStageDefinition
    {
        [SerializeField] private string title;
        [SerializeField, TextArea] private string description;
        [SerializeField] private List<QuestConditionDefinition>
            completionConditions = new List<QuestConditionDefinition>();

        public string Title => title?.Trim() ?? string.Empty;
        public string Description => description?.Trim() ?? string.Empty;
        public IReadOnlyList<QuestConditionDefinition> CompletionConditions =>
            completionConditions ??
            (IReadOnlyList<QuestConditionDefinition>)
            Array.Empty<QuestConditionDefinition>();
    }

    [CreateAssetMenu(
        fileName = "QuestDefinition",
        menuName = "NERA/Quests/Quest Definition")]
    public sealed class QuestDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable unique save-game ID. Do not change after release.")]
        [SerializeField] private string questId;
        [SerializeField] private QuestCategory category;
        [SerializeField] private QuestAvailability availability;
        [SerializeField] private QuestTargetScope targetScope;

        [Header("Presentation")]
        [SerializeField] private string title;
        [SerializeField, TextArea] private string description;
        [SerializeField] private int priority;
        [SerializeField] private bool showInHud = true;

        [Header("Lifecycle")]
        [SerializeField] private List<QuestConditionDefinition>
            activationConditions = new List<QuestConditionDefinition>();
        [SerializeField] private List<QuestStageDefinition> stages =
            new List<QuestStageDefinition>();

        public string QuestId => NormalizeQuestId(questId);
        public QuestCategory Category => category;
        public QuestAvailability Availability => availability;
        public QuestTargetScope TargetScope => targetScope;
        public string Title => title?.Trim() ?? string.Empty;
        public string Description => description?.Trim() ?? string.Empty;
        public int Priority => priority;
        public bool ShowInHud => showInHud;
        public bool CanRepeat => availability == QuestAvailability.Repeatable;
        public bool UsesTriggeringObject =>
            targetScope == QuestTargetScope.PerTriggeringObject;
        public IReadOnlyList<QuestConditionDefinition> ActivationConditions =>
            activationConditions ??
            (IReadOnlyList<QuestConditionDefinition>)
            Array.Empty<QuestConditionDefinition>();
        public IReadOnlyList<QuestStageDefinition> Stages =>
            stages ??
            (IReadOnlyList<QuestStageDefinition>)
            Array.Empty<QuestStageDefinition>();

        public bool TryValidate(out string error)
        {
            if (string.IsNullOrWhiteSpace(QuestId))
            {
                error = "Quest ID is empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(Title))
            {
                error = $"Quest '{QuestId}' has no title.";
                return false;
            }

            if (CanRepeat && ActivationConditions.Count == 0)
            {
                error = $"Repeatable quest '{QuestId}' needs at least one " +
                    "appearance condition.";
                return false;
            }

            if (UsesTriggeringObject && ActivationConditions.Count == 0)
            {
                error = $"Per-object quest '{QuestId}' needs an appearance " +
                    "condition that supplies its target.";
                return false;
            }

            for (int index = 0; index < ActivationConditions.Count; index++)
            {
                QuestConditionDefinition condition =
                    ActivationConditions[index];
                if (condition == null)
                {
                    error = $"Quest '{QuestId}' has an empty appearance " +
                        $"condition at position {index + 1}.";
                    return false;
                }

                if (!condition.TryValidate(true, targetScope, out string issue))
                {
                    error = $"Quest '{QuestId}' appearance condition " +
                        $"{index + 1}: {issue}.";
                    return false;
                }
            }

            if (Stages.Count == 0)
            {
                error = $"Quest '{QuestId}' has no stages.";
                return false;
            }

            for (int stageIndex = 0; stageIndex < Stages.Count; stageIndex++)
            {
                QuestStageDefinition stage = Stages[stageIndex];
                if (stage == null || stage.CompletionConditions.Count == 0)
                {
                    error = $"Quest '{QuestId}' stage {stageIndex + 1} has " +
                        "no completion conditions.";
                    return false;
                }

                for (int conditionIndex = 0;
                     conditionIndex < stage.CompletionConditions.Count;
                     conditionIndex++)
                {
                    QuestConditionDefinition condition =
                        stage.CompletionConditions[conditionIndex];
                    if (condition == null)
                    {
                        error = $"Quest '{QuestId}' stage {stageIndex + 1} " +
                            $"has an empty condition at position " +
                            $"{conditionIndex + 1}.";
                        return false;
                    }

                    if (!condition.TryValidate(
                            false,
                            targetScope,
                            out string issue))
                    {
                        error = $"Quest '{QuestId}' stage {stageIndex + 1}, " +
                            $"condition {conditionIndex + 1}: {issue}.";
                        return false;
                    }
                }
            }

            error = string.Empty;
            return true;
        }

        private static string NormalizeQuestId(string value)
        {
            return value?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private void OnValidate()
        {
            questId = NormalizeQuestId(questId);
            title = title?.Trim();
        }
    }
}
