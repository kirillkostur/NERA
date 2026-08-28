using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.Quests
{
    [Serializable]
    public sealed class QuestConditionDefinition
    {
        [SerializeField] private QuestSignalType signalType;
        [SerializeField] private QuestConditionEvaluation evaluation =
            QuestConditionEvaluation.Event;
        [SerializeField] private QuestConditionTarget target =
            QuestConditionTarget.SpecificObject;
        [Tooltip("Stable ID of the object required by this condition.")]
        [SerializeField] private string targetId = "*";
        [Tooltip("Optional cause filter, for example EnemySabotage.")]
        [SerializeField] private string cause;
        [SerializeField, Min(1)] private int requiredCount = 1;
        [SerializeField] private QuestValueComparison comparison =
            QuestValueComparison.GreaterOrEqual;
        [Tooltip("Numeric value used by state and threshold conditions.")]
        [SerializeField, Min(0f)] private float threshold = 0.5f;

        public QuestSignalType SignalType => signalType;
        public QuestConditionEvaluation Evaluation => evaluation;
        public QuestConditionTarget Target => target;
        public string TargetId => targetId?.Trim() ?? string.Empty;
        public string Cause => cause?.Trim() ?? string.Empty;
        public int RequiredCount => Mathf.Max(1, requiredCount);
        public QuestValueComparison Comparison => comparison;
        public float Threshold => UsesNormalizedValue
            ? Mathf.Clamp01(threshold)
            : Mathf.Max(0f, threshold);
        public bool UsesCurrentState =>
            evaluation == QuestConditionEvaluation.CurrentState;
        public bool UsesValueComparison => signalType ==
                QuestSignalType.InventoryItemCountChanged ||
            signalType == QuestSignalType.StationSystemUpgraded ||
            signalType == QuestSignalType.EnergyChargeChanged;
        public bool UsesNormalizedValue =>
            signalType == QuestSignalType.DeviceConditionBelow ||
            signalType == QuestSignalType.DeviceConditionRestored ||
            signalType == QuestSignalType.EnergyChargeChanged;

        public static bool SupportsCurrentState(QuestSignalType type)
        {
            return type == QuestSignalType.LocationDiscovered ||
                type == QuestSignalType.LocationEntered ||
                type == QuestSignalType.ResearchAnalyzed ||
                type == QuestSignalType.DeviceConditionBelow ||
                type == QuestSignalType.DeviceConditionRestored ||
                type == QuestSignalType.StationSystemActivated ||
                type == QuestSignalType.QuestCompleted ||
                type == QuestSignalType.InventoryItemCountChanged ||
                type == QuestSignalType.StationSystemDeactivated ||
                type == QuestSignalType.StationSystemUpgraded ||
                type == QuestSignalType.StationPowerOnline ||
                type == QuestSignalType.StationPowerOffline ||
                type == QuestSignalType.EnergyChargeChanged ||
                type == QuestSignalType.WeatherChanged;
        }

        internal bool Matches(
            QuestSignal signal,
            string contextTargetId)
        {
            return MatchesIdentity(signal, contextTargetId) &&
                IsSatisfiedBy(signal);
        }

        internal bool MatchesIdentity(
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
            if (!string.IsNullOrEmpty(expectedCause) &&
                !QuestIdUtility.Equals(signal.Cause, expectedCause))
            {
                return false;
            }

            return true;
        }

        internal bool IsSatisfiedBy(QuestSignal signal)
        {
            return signalType switch
            {
                QuestSignalType.DeviceConditionBelow =>
                    signal.Value <= Threshold,
                QuestSignalType.DeviceConditionRestored =>
                    signal.Value >= Threshold,
                QuestSignalType.InventoryItemCountChanged =>
                    Compare(signal.Value, Threshold),
                QuestSignalType.StationSystemUpgraded =>
                    Compare(signal.Value, Threshold),
                QuestSignalType.EnergyChargeChanged =>
                    Compare(signal.Value, Threshold),
                _ => true
            };
        }

        private bool Compare(float actual, float expected)
        {
            return comparison switch
            {
                QuestValueComparison.Less => actual < expected,
                QuestValueComparison.LessOrEqual => actual <= expected,
                QuestValueComparison.Equal =>
                    Mathf.Approximately(actual, expected),
                QuestValueComparison.Greater => actual > expected,
                _ => actual >= expected
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

            if (UsesCurrentState && !SupportsCurrentState(signalType))
            {
                error = $"event '{signalType}' cannot check current state";
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
        [SerializeField] private QuestConditionLogic completionLogic =
            QuestConditionLogic.All;
        [Tooltip(
            "Creates a full checkpoint at the player's current position " +
            "after this stage is completed.")]
        [SerializeField] private bool createCheckpointOnCompletion;
        [Tooltip(
            "QuestMarkerAnchor IDs visible while this stage is active. " +
            "Use {targetId} for a per-object quest target.")]
        [SerializeField] private List<string> questMarkerIds =
            new List<string>();
        [Tooltip(
            "Scene EnemySpawner IDs invoked when this stage becomes active.")]
        [SerializeField] private List<string>
            enemySpawnerIdsOnStart = new List<string>();
        [Tooltip(
            "Scene EnemySpawner IDs invoked after this stage completes. " +
            "The wave is created before the optional checkpoint.")]
        [SerializeField] private List<string>
            enemySpawnerIdsOnCompletion = new List<string>();
        [SerializeField] private List<QuestConditionDefinition>
            completionConditions = new List<QuestConditionDefinition>();

        public string Title => title?.Trim() ?? string.Empty;
        public string Description => description?.Trim() ?? string.Empty;
        public QuestConditionLogic CompletionLogic => completionLogic;
        public bool CreateCheckpointOnCompletion =>
            createCheckpointOnCompletion;
        public IReadOnlyList<string> QuestMarkerIds =>
            questMarkerIds ??
            (IReadOnlyList<string>)Array.Empty<string>();
        public IReadOnlyList<string> EnemySpawnerIdsOnStart =>
            enemySpawnerIdsOnStart ??
            (IReadOnlyList<string>)Array.Empty<string>();
        public IReadOnlyList<string> EnemySpawnerIdsOnCompletion =>
            enemySpawnerIdsOnCompletion ??
            (IReadOnlyList<string>)Array.Empty<string>();
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
        [SerializeField] private QuestConditionLogic activationLogic =
            QuestConditionLogic.All;
        [SerializeField] private List<QuestConditionDefinition>
            activationConditions = new List<QuestConditionDefinition>();
        [SerializeField] private List<QuestStageDefinition> stages =
            new List<QuestStageDefinition>();

        [Header("Environment Actions")]
        [Tooltip("Optional weather action executed when this quest starts.")]
        [SerializeField] private QuestWeatherAction weatherActionOnActivation;
        [Tooltip("Optional weather action executed when this quest completes.")]
        [SerializeField] private QuestWeatherAction weatherActionOnCompletion;
        [Tooltip(
            "Optional sandstorm duration override. Leave both at zero to use " +
            "the centralized environment config range.")]
        [SerializeField, Min(0f)]
        private float sandstormDurationMinSeconds;
        [SerializeField, Min(0f)]
        private float sandstormDurationMaxSeconds;

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
        public QuestConditionLogic ActivationLogic => activationLogic;
        public IReadOnlyList<QuestConditionDefinition> ActivationConditions =>
            activationConditions ??
            (IReadOnlyList<QuestConditionDefinition>)
            Array.Empty<QuestConditionDefinition>();
        public IReadOnlyList<QuestStageDefinition> Stages =>
            stages ??
            (IReadOnlyList<QuestStageDefinition>)
            Array.Empty<QuestStageDefinition>();
        public QuestWeatherAction WeatherActionOnActivation =>
            weatherActionOnActivation;
        public QuestWeatherAction WeatherActionOnCompletion =>
            weatherActionOnCompletion;
        public float SandstormDurationMinSeconds =>
            Mathf.Max(0f, sandstormDurationMinSeconds);
        public float SandstormDurationMaxSeconds => Mathf.Max(
            SandstormDurationMinSeconds,
            sandstormDurationMaxSeconds);

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

                if (!TryValidateUniqueIds(
                        stage.QuestMarkerIds,
                        "quest marker",
                        out string markerIssue))
                {
                    error = $"Quest '{QuestId}' stage {stageIndex + 1}: " +
                        markerIssue;
                    return false;
                }

                if (!TryValidateSpawnerIds(
                        stage.EnemySpawnerIdsOnStart,
                        "on start",
                        out string startIssue))
                {
                    error = $"Quest '{QuestId}' stage {stageIndex + 1}: " +
                        startIssue;
                    return false;
                }

                if (!TryValidateSpawnerIds(
                        stage.EnemySpawnerIdsOnCompletion,
                        "on completion",
                        out string completionIssue))
                {
                    error = $"Quest '{QuestId}' stage {stageIndex + 1}: " +
                        completionIssue;
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private static bool TryValidateSpawnerIds(
            IReadOnlyList<string> values,
            string phase,
            out string error)
        {
            return TryValidateUniqueIds(
                values,
                $"EnemySpawner action {phase}",
                out error);
        }

        private static bool TryValidateUniqueIds(
            IReadOnlyList<string> values,
            string label,
            out string error)
        {
            HashSet<string> ids =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < values.Count; index++)
            {
                string id = values[index]?.Trim();
                if (string.IsNullOrEmpty(id))
                {
                    error = $"empty {label} at " +
                        $"position {index + 1}.";
                    return false;
                }

                if (!ids.Add(id))
                {
                    error = $"{label} '{id}' is listed more than once.";
                    return false;
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
            sandstormDurationMinSeconds = Mathf.Max(
                0f,
                sandstormDurationMinSeconds);
            sandstormDurationMaxSeconds = Mathf.Max(
                sandstormDurationMinSeconds,
                sandstormDurationMaxSeconds);
        }
    }
}
