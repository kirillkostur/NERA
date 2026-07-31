using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.Quests
{
    [DisallowMultipleComponent]
    public sealed class QuestController : MonoBehaviour
    {
        private sealed class PendingActivation
        {
            public PendingActivation(
                QuestDefinition definition,
                string activationId,
                string contextTargetId,
                string contextTargetName,
                IReadOnlyList<int> restoredProgress = null)
            {
                Definition = definition;
                ActivationId = QuestIdUtility.Normalize(activationId);
                ContextTargetId = QuestIdUtility.Normalize(contextTargetId);
                ContextTargetName = string.IsNullOrWhiteSpace(contextTargetName)
                    ? ContextTargetId
                    : contextTargetName.Trim();
                Progress = new int[definition.ActivationConditions.Count];

                if (restoredProgress == null)
                    return;

                int count = Math.Min(Progress.Length, restoredProgress.Count);
                for (int index = 0; index < count; index++)
                {
                    Progress[index] = Math.Min(
                        definition.ActivationConditions[index].RequiredCount,
                        Math.Max(0, restoredProgress[index]));
                }
            }

            public QuestDefinition Definition { get; }
            public string ActivationId { get; }
            public string ContextTargetId { get; }
            public string ContextTargetName { get; }
            public int[] Progress { get; }

            public bool IsComplete
            {
                get
                {
                    if (Progress.Length == 0)
                        return true;

                    for (int index = 0; index < Progress.Length; index++)
                    {
                        if (Progress[index] <
                            Definition.ActivationConditions[index].RequiredCount)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            public QuestActivationSaveData Capture()
            {
                QuestActivationSaveData saved = new QuestActivationSaveData
                {
                    activationId = ActivationId,
                    questId = Definition.QuestId,
                    contextTargetId = ContextTargetId,
                    contextTargetName = ContextTargetName
                };
                saved.conditionProgress.AddRange(Progress);
                return saved;
            }
        }

        [SerializeField] private QuestCatalog catalog;

        private readonly Dictionary<string, QuestRuntimeState> activeQuests =
            new Dictionary<string, QuestRuntimeState>(StringComparer.Ordinal);
        private readonly Dictionary<string, QuestHistorySaveData> history =
            new Dictionary<string, QuestHistorySaveData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PendingActivation>
            pendingActivations =
                new Dictionary<string, PendingActivation>(StringComparer.Ordinal);

        public static QuestController Instance { get; private set; }

        public event Action QuestsChanged;
        public event Action<QuestRuntimeState> QuestActivated;
        public event Action<QuestRuntimeState> QuestStageChanged;
        public event Action<QuestRuntimeState> QuestCompleted;

        public QuestCatalog Catalog => ResolveCatalog();
        public IReadOnlyList<QuestRuntimeState> ActiveQuests =>
            BuildSortedActiveList();
        public QuestRuntimeState TrackedQuest
        {
            get
            {
                IReadOnlyList<QuestRuntimeState> sorted = ActiveQuests;
                return sorted.Count > 0 ? sorted[0] : null;
            }
        }
        public string CurrentObjective => TrackedQuest?.ObjectiveTitle ??
            string.Empty;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            ResolveCatalog();
            ActivateAutomaticQuests();
        }

        public void Configure(QuestCatalog questCatalog)
        {
            catalog = questCatalog;
            ResetProgress();
        }

        public bool Report(
            QuestSignalType type,
            string targetId,
            string targetName = null,
            int amount = 1,
            float value = 0f,
            string cause = null)
        {
            return Report(new QuestSignal(
                type,
                targetId,
                targetName,
                amount,
                value,
                cause));
        }

        public bool Report(QuestSignal signal)
        {
            QuestCatalog resolvedCatalog = ResolveCatalog();
            if (resolvedCatalog == null ||
                string.IsNullOrWhiteSpace(signal.TargetId))
            {
                return false;
            }

            bool changed = false;
            foreach (QuestDefinition definition in resolvedCatalog.Definitions)
            {
                if (definition != null)
                    changed |= TryProgressActivation(definition, signal);
            }

            List<QuestRuntimeState> activeSnapshot =
                new List<QuestRuntimeState>(activeQuests.Values);
            foreach (QuestRuntimeState state in activeSnapshot)
                changed |= TryProgressActiveQuest(state, signal);

            if (changed)
                QuestsChanged?.Invoke();

            return changed;
        }

        public bool ReportDeviceCondition(
            string targetId,
            string targetName,
            float condition)
        {
            float clamped = Mathf.Clamp01(condition);
            bool changed = Report(
                QuestSignalType.DeviceConditionBelow,
                targetId,
                targetName,
                value: clamped);
            return Report(
                    QuestSignalType.DeviceConditionRestored,
                    targetId,
                    targetName,
                    value: clamped) ||
                changed;
        }

        public bool ReportStationFault(
            string targetId,
            string targetName,
            string cause)
        {
            return Report(
                QuestSignalType.StationFaultStarted,
                targetId,
                targetName,
                cause: cause);
        }

        public QuestRuntimeState FindActive(string instanceId)
        {
            activeQuests.TryGetValue(
                QuestIdUtility.Normalize(instanceId),
                out QuestRuntimeState state);
            return state;
        }

        public bool IsCompleted(string instanceId)
        {
            return history.TryGetValue(
                    QuestIdUtility.Normalize(instanceId),
                    out QuestHistorySaveData saved) &&
                saved.completionCount > 0;
        }

        public int GetCompletionCount(string instanceId)
        {
            return history.TryGetValue(
                    QuestIdUtility.Normalize(instanceId),
                    out QuestHistorySaveData saved)
                ? Mathf.Max(0, saved.completionCount)
                : 0;
        }

        public List<QuestInstanceSaveData> CaptureActiveQuests()
        {
            List<QuestInstanceSaveData> result =
                new List<QuestInstanceSaveData>();
            foreach (QuestRuntimeState state in BuildSortedActiveList())
                result.Add(state.Capture());
            return result;
        }

        public List<QuestHistorySaveData> CaptureHistory()
        {
            List<QuestHistorySaveData> result =
                new List<QuestHistorySaveData>();
            List<string> ids = new List<string>(history.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                QuestHistorySaveData source = history[id];
                result.Add(new QuestHistorySaveData
                {
                    instanceId = source.instanceId,
                    questId = source.questId,
                    contextTargetId = source.contextTargetId,
                    contextTargetName = source.contextTargetName,
                    completionCount = source.completionCount
                });
            }
            return result;
        }

        public List<QuestActivationSaveData> CapturePendingActivations()
        {
            List<QuestActivationSaveData> result =
                new List<QuestActivationSaveData>();
            List<string> ids = new List<string>(pendingActivations.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
                result.Add(pendingActivations[id].Capture());
            return result;
        }

        public void RestoreProgress(
            IEnumerable<QuestInstanceSaveData> savedActiveQuests,
            IEnumerable<QuestHistorySaveData> savedHistory,
            IEnumerable<QuestActivationSaveData> savedPendingActivations)
        {
            activeQuests.Clear();
            history.Clear();
            pendingActivations.Clear();

            QuestCatalog resolvedCatalog = ResolveCatalog();
            if (resolvedCatalog == null)
            {
                QuestsChanged?.Invoke();
                return;
            }

            if (savedHistory != null)
            {
                foreach (QuestHistorySaveData saved in savedHistory)
                {
                    if (saved == null ||
                        resolvedCatalog.Find(saved.questId) == null ||
                        saved.completionCount <= 0)
                    {
                        continue;
                    }

                    string instanceId = QuestIdUtility.Normalize(saved.instanceId);
                    if (string.IsNullOrEmpty(instanceId))
                        continue;

                    history[instanceId] = new QuestHistorySaveData
                    {
                        instanceId = instanceId,
                        questId = QuestIdUtility.Normalize(saved.questId),
                        contextTargetId = QuestIdUtility.Normalize(saved.contextTargetId),
                        contextTargetName = saved.contextTargetName,
                        completionCount = saved.completionCount
                    };
                }
            }

            if (savedActiveQuests != null)
            {
                foreach (QuestInstanceSaveData saved in savedActiveQuests)
                {
                    QuestDefinition definition = saved != null
                        ? resolvedCatalog.Find(saved.questId)
                        : null;
                    if (definition == null ||
                        saved.currentStageIndex < 0 ||
                        saved.currentStageIndex >= definition.Stages.Count)
                    {
                        continue;
                    }

                    string instanceId = BuildInstanceId(
                        definition,
                        saved.contextTargetId);
                    if (string.IsNullOrEmpty(instanceId) ||
                        activeQuests.ContainsKey(instanceId))
                    {
                        continue;
                    }

                    activeQuests.Add(
                        instanceId,
                        new QuestRuntimeState(
                            definition,
                            instanceId,
                            saved.contextTargetId,
                            saved.contextTargetName,
                            saved.currentStageIndex,
                            saved.conditionProgress));
                }
            }

            if (savedPendingActivations != null)
            {
                foreach (QuestActivationSaveData saved in
                         savedPendingActivations)
                {
                    QuestDefinition definition = saved != null
                        ? resolvedCatalog.Find(saved.questId)
                        : null;
                    if (definition == null ||
                        definition.ActivationConditions.Count == 0)
                    {
                        continue;
                    }

                    string activationId = BuildInstanceId(
                        definition,
                        saved.contextTargetId);
                    if (string.IsNullOrEmpty(activationId) ||
                        activeQuests.ContainsKey(activationId) ||
                        pendingActivations.ContainsKey(activationId))
                    {
                        continue;
                    }

                    pendingActivations.Add(
                        activationId,
                        new PendingActivation(
                            definition,
                            activationId,
                            saved.contextTargetId,
                            saved.contextTargetName,
                            saved.conditionProgress));
                }
            }

            ActivateAutomaticQuests();
            QuestsChanged?.Invoke();
        }

        public void ResetProgress()
        {
            activeQuests.Clear();
            history.Clear();
            pendingActivations.Clear();
            ActivateAutomaticQuests();
            QuestsChanged?.Invoke();
        }

        private bool TryProgressActivation(
            QuestDefinition definition,
            QuestSignal signal)
        {
            if (definition.ActivationConditions.Count == 0)
                return false;

            string contextTargetId = definition.UsesTriggeringObject
                ? signal.TargetId
                : string.Empty;
            if (definition.UsesTriggeringObject &&
                string.IsNullOrWhiteSpace(contextTargetId))
            {
                return false;
            }

            string activationId = BuildInstanceId(definition, contextTargetId);
            if (activeQuests.ContainsKey(activationId) ||
                (!definition.CanRepeat && history.ContainsKey(activationId)))
            {
                return false;
            }

            if (!pendingActivations.TryGetValue(
                    activationId,
                    out PendingActivation pending))
            {
                pending = new PendingActivation(
                    definition,
                    activationId,
                    contextTargetId,
                    signal.TargetName);
            }

            bool changed = false;
            for (int index = 0;
                 index < definition.ActivationConditions.Count;
                 index++)
            {
                QuestConditionDefinition condition =
                    definition.ActivationConditions[index];
                if (!condition.Matches(
                        signal,
                        contextTargetId))
                {
                    continue;
                }

                int next = Math.Min(
                    condition.RequiredCount,
                    pending.Progress[index] + signal.Amount);
                if (next == pending.Progress[index])
                    continue;

                pending.Progress[index] = next;
                changed = true;
            }

            if (!changed)
                return false;

            if (!pending.IsComplete)
            {
                pendingActivations[activationId] = pending;
                return true;
            }

            pendingActivations.Remove(activationId);
            return Activate(
                    definition,
                    contextTargetId,
                    pending.ContextTargetName) ||
                changed;
        }

        private bool TryProgressActiveQuest(
            QuestRuntimeState state,
            QuestSignal signal)
        {
            QuestStageDefinition stage = state.CurrentStage;
            if (stage == null)
                return false;

            bool changed = false;
            for (int index = 0;
                 index < stage.CompletionConditions.Count;
                 index++)
            {
                QuestConditionDefinition condition =
                    stage.CompletionConditions[index];
                if (!condition.Matches(
                        signal,
                        state.ContextTargetId))
                {
                    continue;
                }

                changed |= state.AddConditionProgress(
                    index,
                    signal.Amount,
                    condition.RequiredCount);
            }

            if (!changed || !state.IsStageComplete())
                return changed;

            if (state.AdvanceStage())
            {
                QuestStageChanged?.Invoke(state);
                return true;
            }

            Complete(state);
            return true;
        }

        private bool Activate(
            QuestDefinition definition,
            string contextTargetId,
            string contextTargetName)
        {
            string instanceId = BuildInstanceId(
                definition,
                contextTargetId);
            if (string.IsNullOrEmpty(instanceId) ||
                activeQuests.ContainsKey(instanceId) ||
                (!definition.CanRepeat && history.ContainsKey(instanceId)))
            {
                return false;
            }

            QuestRuntimeState state = new QuestRuntimeState(
                definition,
                instanceId,
                contextTargetId,
                contextTargetName);
            activeQuests.Add(instanceId, state);
            QuestActivated?.Invoke(state);
            Debug.Log(
                $"Quest activated: '{state.InstanceId}' — {state.Title}.",
                this);
            return true;
        }

        private void Complete(QuestRuntimeState state)
        {
            activeQuests.Remove(state.InstanceId);
            if (!history.TryGetValue(
                    state.InstanceId,
                    out QuestHistorySaveData saved))
            {
                saved = new QuestHistorySaveData
                {
                    instanceId = state.InstanceId,
                    questId = state.QuestId,
                    contextTargetId = state.ContextTargetId,
                    contextTargetName = state.ContextTargetName
                };
                history.Add(state.InstanceId, saved);
            }

            saved.completionCount++;
            QuestCompleted?.Invoke(state);
            Debug.Log(
                $"Quest completed: '{state.InstanceId}' — {state.Title}.",
                this);
        }

        private void ActivateAutomaticQuests()
        {
            QuestCatalog resolvedCatalog = ResolveCatalog();
            if (resolvedCatalog == null)
                return;

            foreach (QuestDefinition definition in resolvedCatalog.Definitions)
            {
                if (definition != null &&
                    !definition.UsesTriggeringObject &&
                    definition.ActivationConditions.Count == 0)
                {
                    Activate(definition, string.Empty, string.Empty);
                }
            }
        }

        private QuestCatalog ResolveCatalog()
        {
            if (catalog == null)
                catalog = QuestCatalog.LoadDefault();
            return catalog;
        }

        private List<QuestRuntimeState> BuildSortedActiveList()
        {
            List<QuestRuntimeState> result =
                new List<QuestRuntimeState>(activeQuests.Values);
            result.Sort((left, right) =>
            {
                int priority = right.Definition.Priority.CompareTo(
                    left.Definition.Priority);
                return priority != 0
                    ? priority
                    : string.CompareOrdinal(left.InstanceId, right.InstanceId);
            });
            return result;
        }

        private static string BuildInstanceId(
            QuestDefinition definition,
            string contextTargetId)
        {
            if (definition == null)
                return string.Empty;

            if (!definition.UsesTriggeringObject)
                return definition.QuestId;

            string context = QuestIdUtility.Normalize(contextTargetId);
            return string.IsNullOrEmpty(context)
                ? string.Empty
                : $"{definition.QuestId}:{context}";
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
