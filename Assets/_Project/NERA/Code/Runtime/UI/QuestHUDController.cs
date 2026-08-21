using System.Collections.Generic;
using System.Text;
using NERA.Localization;
using NERA.Quests;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.UI
{
    [DisallowMultipleComponent]
    public sealed class QuestHUDController : MonoBehaviour
    {
        private const int MainQuestLimit = 3;
        private const int SideQuestLimit = 4;

        [Header("Containers")]
        [SerializeField] private GameObject mainDisplayRoot;
        [SerializeField] private RectTransform mainContentRoot;
        [SerializeField] private GameObject sideDisplayRoot;
        [SerializeField] private RectTransform sideContentRoot;

        [Header("Prefabs")]
        [SerializeField] private QuestGroupView questGroupPrefab;
        [SerializeField] private QuestObjectiveView questObjectivePrefab;

        [Header("Completion")]
        [SerializeField, Min(0.1f)]
        private float completedDisplayDuration = 1.25f;

        private readonly Dictionary<string, QuestDisplayEntry>
            completedEntries = new Dictionary<string, QuestDisplayEntry>();
        private readonly Dictionary<string, QuestDisplayEntry>
            visibleActiveEntries = new Dictionary<string, QuestDisplayEntry>();
        private readonly List<string> expiredEntryIds = new List<string>();
        private readonly List<QuestGroupView> mainGroupViews =
            new List<QuestGroupView>();
        private readonly List<QuestGroupView> sideGroupViews =
            new List<QuestGroupView>();
        private readonly List<string> visibleMainGroupOrder =
            new List<string>();
        private readonly List<string> visibleSideGroupOrder =
            new List<string>();
        private readonly List<string> heldMainGroupOrder =
            new List<string>();
        private readonly List<string> heldSideGroupOrder =
            new List<string>();

        private QuestController questController;
        private RootLayout mainRootLayout;
        private RootLayout sideRootLayout;
        private string displayedMainText = string.Empty;
        private string displayedSideText = string.Empty;

        public string DisplayedMainText => displayedMainText;
        public string DisplayedSideText => displayedSideText;
        public bool IsVisible =>
            (mainDisplayRoot != null && mainDisplayRoot.activeSelf) ||
            (sideDisplayRoot != null && sideDisplayRoot.activeSelf);
        public int MaxDisplayedMainQuests => MainQuestLimit;
        public int MaxDisplayedSideQuests => SideQuestLimit;
        public float CompletedDisplayDuration => completedDisplayDuration;

        private void Reset()
        {
            ResolveViewReferences();
        }

        private void OnValidate()
        {
            completedDisplayDuration = Mathf.Max(
                0.1f,
                completedDisplayDuration);
            ResolveViewReferences();
        }

        private void Awake()
        {
            ResolveViewReferences();
            CaptureRootLayouts();
            DisableRootRaycasts();
        }

        private void OnEnable()
        {
            NERALocalization.LocaleChanged += Refresh;
            Bind(QuestController.Instance);
            Refresh();
        }

        private void Start()
        {
            Bind(QuestController.Instance);
        }

        private void Update()
        {
            if (questController != QuestController.Instance)
                Bind(QuestController.Instance);

            if (RemoveExpiredCompletedEntries())
                Refresh();
        }

        private void OnDisable()
        {
            NERALocalization.LocaleChanged -= Refresh;
            Bind(null);
        }

        private void OnDestroy()
        {
            Bind(null);
        }

        public void ConfigureView(
            GameObject mainRoot,
            RectTransform mainContent,
            GameObject sideRoot,
            RectTransform sideContent,
            QuestGroupView groupPrefab,
            QuestObjectiveView objectivePrefab)
        {
            mainDisplayRoot = mainRoot;
            mainContentRoot = mainContent;
            sideDisplayRoot = sideRoot;
            sideContentRoot = sideContent;
            questGroupPrefab = groupPrefab;
            questObjectivePrefab = objectivePrefab;
            CaptureRootLayouts();
            DisableRootRaycasts();
            Refresh();
        }

        public void Refresh()
        {
            Dictionary<string, QuestDisplayGroup> mainGroupsById =
                new Dictionary<string, QuestDisplayGroup>();
            Dictionary<string, QuestDisplayGroup> sideGroupsById =
                new Dictionary<string, QuestDisplayGroup>();

            foreach (QuestDisplayEntry entry in completedEntries.Values)
                AddToGroup(entry, mainGroupsById, sideGroupsById);

            IReadOnlyList<QuestRuntimeState> activeQuests =
                questController?.ActiveQuests;
            if (activeQuests != null)
            {
                for (int index = 0; index < activeQuests.Count; index++)
                {
                    QuestRuntimeState state = activeQuests[index];
                    if (state?.Definition == null ||
                        !state.Definition.ShowInHud ||
                        completedEntries.ContainsKey(state.InstanceId))
                    {
                        continue;
                    }

                    AddToGroup(
                        QuestDisplayEntry.From(state),
                        mainGroupsById,
                        sideGroupsById);
                }
            }

            List<QuestDisplayGroup> mainGroups =
                new List<QuestDisplayGroup>(mainGroupsById.Values);
            List<QuestDisplayGroup> sideGroups =
                new List<QuestDisplayGroup>(sideGroupsById.Values);
            SortAndLimit(
                mainGroups,
                MainQuestLimit,
                heldMainGroupOrder);
            SortAndLimit(
                sideGroups,
                SideQuestLimit,
                heldSideGroupOrder);
            CacheVisibleGroupOrder(mainGroups, visibleMainGroupOrder);
            CacheVisibleGroupOrder(sideGroups, visibleSideGroupOrder);
            CacheVisibleActiveEntries(mainGroups, sideGroups);

            displayedMainText = FormatGroups(mainGroups);
            displayedSideText = FormatGroups(sideGroups);
            RenderCategory(
                mainDisplayRoot,
                mainContentRoot,
                mainRootLayout,
                mainGroups,
                mainGroupViews);
            RenderCategory(
                sideDisplayRoot,
                sideContentRoot,
                sideRootLayout,
                sideGroups,
                sideGroupViews);
        }

        private void Bind(QuestController controller)
        {
            if (questController == controller)
                return;

            if (questController != null)
            {
                questController.QuestsChanged -= Refresh;
                questController.QuestCompleted -= HandleQuestCompleted;
            }

            completedEntries.Clear();
            visibleActiveEntries.Clear();
            visibleMainGroupOrder.Clear();
            visibleSideGroupOrder.Clear();
            heldMainGroupOrder.Clear();
            heldSideGroupOrder.Clear();
            questController = controller;

            if (questController != null)
            {
                questController.QuestsChanged += Refresh;
                questController.QuestCompleted += HandleQuestCompleted;
            }

            Refresh();
        }

        private void HandleQuestCompleted(QuestRuntimeState state)
        {
            if (state == null ||
                !visibleActiveEntries.TryGetValue(
                    state.InstanceId,
                    out QuestDisplayEntry entry))
            {
                return;
            }

            HoldVisibleGroupOrder(entry.Category);
            completedEntries[state.InstanceId] = entry.AsCompleted(
                Time.unscaledTime + completedDisplayDuration);
            Refresh();
        }

        private bool RemoveExpiredCompletedEntries()
        {
            if (completedEntries.Count == 0)
                return false;

            float now = Time.unscaledTime;
            expiredEntryIds.Clear();
            foreach (KeyValuePair<string, QuestDisplayEntry> pair in
                     completedEntries)
            {
                if (now >= pair.Value.ExpiresAt)
                    expiredEntryIds.Add(pair.Key);
            }

            for (int index = 0; index < expiredEntryIds.Count; index++)
                completedEntries.Remove(expiredEntryIds[index]);

            if (!HasCompletedEntry(QuestCategory.Main))
                heldMainGroupOrder.Clear();
            if (!HasCompletedEntry(QuestCategory.Side))
                heldSideGroupOrder.Clear();

            return expiredEntryIds.Count > 0;
        }

        private void HoldVisibleGroupOrder(QuestCategory category)
        {
            List<string> heldOrder = category == QuestCategory.Main
                ? heldMainGroupOrder
                : heldSideGroupOrder;
            if (heldOrder.Count > 0)
                return;

            IReadOnlyList<string> visibleOrder =
                category == QuestCategory.Main
                    ? visibleMainGroupOrder
                    : visibleSideGroupOrder;
            for (int index = 0; index < visibleOrder.Count; index++)
                heldOrder.Add(visibleOrder[index]);
        }

        private bool HasCompletedEntry(QuestCategory category)
        {
            foreach (QuestDisplayEntry entry in completedEntries.Values)
            {
                if (entry.Category == category)
                    return true;
            }

            return false;
        }

        private static void AddToGroup(
            QuestDisplayEntry entry,
            Dictionary<string, QuestDisplayGroup> mainGroups,
            Dictionary<string, QuestDisplayGroup> sideGroups)
        {
            Dictionary<string, QuestDisplayGroup> groups =
                entry.Category == QuestCategory.Main
                    ? mainGroups
                    : sideGroups;
            if (!groups.TryGetValue(
                    entry.GroupId,
                    out QuestDisplayGroup group))
            {
                group = new QuestDisplayGroup(entry);
                groups.Add(entry.GroupId, group);
            }

            group.Add(entry);
        }

        private static void SortAndLimit(
            List<QuestDisplayGroup> groups,
            int limit,
            IReadOnlyList<string> heldOrder)
        {
            for (int index = 0; index < groups.Count; index++)
                groups[index].SortEntries();

            if (heldOrder.Count > 0)
            {
                Dictionary<string, QuestDisplayGroup> groupsById =
                    new Dictionary<string, QuestDisplayGroup>();
                for (int index = 0; index < groups.Count; index++)
                    groupsById[groups[index].GroupId] = groups[index];

                groups.Clear();
                for (int index = 0;
                     index < heldOrder.Count && groups.Count < limit;
                     index++)
                {
                    if (groupsById.TryGetValue(
                            heldOrder[index],
                            out QuestDisplayGroup group))
                    {
                        groups.Add(group);
                    }
                }
                return;
            }

            groups.Sort(CompareGroups);
            if (groups.Count > limit)
                groups.RemoveRange(limit, groups.Count - limit);
        }

        private static void CacheVisibleGroupOrder(
            IReadOnlyList<QuestDisplayGroup> groups,
            List<string> target)
        {
            target.Clear();
            for (int index = 0; index < groups.Count; index++)
                target.Add(groups[index].GroupId);
        }

        private static int CompareGroups(
            QuestDisplayGroup left,
            QuestDisplayGroup right)
        {
            int priority = right.Priority.CompareTo(left.Priority);
            return priority != 0
                ? priority
                : string.CompareOrdinal(left.GroupId, right.GroupId);
        }

        private void CacheVisibleActiveEntries(
            IReadOnlyList<QuestDisplayGroup> mainGroups,
            IReadOnlyList<QuestDisplayGroup> sideGroups)
        {
            visibleActiveEntries.Clear();
            CacheVisibleActiveEntries(mainGroups);
            CacheVisibleActiveEntries(sideGroups);
        }

        private void CacheVisibleActiveEntries(
            IReadOnlyList<QuestDisplayGroup> groups)
        {
            for (int groupIndex = 0;
                 groupIndex < groups.Count;
                 groupIndex++)
            {
                IReadOnlyList<QuestDisplayEntry> entries =
                    groups[groupIndex].Entries;
                for (int entryIndex = 0;
                     entryIndex < entries.Count;
                     entryIndex++)
                {
                    QuestDisplayEntry entry = entries[entryIndex];
                    if (!entry.IsCompleted)
                        visibleActiveEntries[entry.InstanceId] = entry;
                }
            }
        }

        private void RenderCategory(
            GameObject displayRoot,
            RectTransform contentRoot,
            RootLayout rootLayout,
            IReadOnlyList<QuestDisplayGroup> groups,
            List<QuestGroupView> views)
        {
            bool canRender = displayRoot != null &&
                contentRoot != null &&
                questGroupPrefab != null &&
                questObjectivePrefab != null;
            if (!canRender || groups.Count == 0)
            {
                SetViewsActive(views, 0);
                rootLayout.Restore(displayRoot);
                SetRootActive(displayRoot, false);
                return;
            }

            rootLayout.Restore(displayRoot);
            SetRootActive(displayRoot, true);
            EnsureGroupViewCount(views, groups.Count, contentRoot);

            for (int index = 0; index < groups.Count; index++)
            {
                QuestDisplayGroup group = groups[index];
                QuestGroupView view = views[index];
                view.gameObject.SetActive(true);
                view.transform.SetSiblingIndex(index);

                bool strikeTitle = !group.IsGrouped &&
                    group.Entries.Count == 1 &&
                    group.Entries[0].IsCompleted;
                view.BeginUpdate(
                    group.Title,
                    strikeTitle,
                    group.ObjectiveCount,
                    questObjectivePrefab);

                int objectiveIndex = 0;
                for (int entryIndex = 0;
                     entryIndex < group.Entries.Count;
                     entryIndex++)
                {
                    QuestDisplayEntry entry = group.Entries[entryIndex];
                    if (string.IsNullOrWhiteSpace(entry.Objective))
                        continue;

                    view.SetObjective(
                        objectiveIndex,
                        entry.Objective,
                        entry.IsCompleted);
                    objectiveIndex++;
                }
            }

            SetViewsActive(views, groups.Count);
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
            float contentHeight = CalculateContentHeight(
                views,
                groups.Count,
                contentRoot);
            rootLayout.SetHeight(
                displayRoot,
                Mathf.Max(rootLayout.Size.y, contentHeight));
        }

        private void EnsureGroupViewCount(
            List<QuestGroupView> views,
            int count,
            RectTransform contentRoot)
        {
            while (views.Count < count)
            {
                QuestGroupView view = Instantiate(
                    questGroupPrefab,
                    contentRoot);
                view.name = $"QuestGroup_{views.Count:00}";
                views.Add(view);
            }
        }

        private static void SetViewsActive(
            IReadOnlyList<QuestGroupView> views,
            int activeCount)
        {
            for (int index = 0; index < views.Count; index++)
            {
                if (views[index] != null)
                    views[index].gameObject.SetActive(index < activeCount);
            }
        }

        private static float CalculateContentHeight(
            IReadOnlyList<QuestGroupView> views,
            int activeCount,
            RectTransform contentRoot)
        {
            float height = 0f;
            for (int index = 0;
                 index < activeCount && index < views.Count;
                 index++)
            {
                height += Mathf.Max(0f, views[index].PreferredHeight);
            }

            if (contentRoot.TryGetComponent(
                    out VerticalLayoutGroup layout))
            {
                height += layout.padding.top + layout.padding.bottom;
                height += Mathf.Max(0, activeCount - 1) * layout.spacing;
            }

            contentRoot.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                height);
            return height;
        }

        private void ResolveViewReferences()
        {
            if (mainDisplayRoot == null)
                mainDisplayRoot = FindChild("background_QuestMain");
            if (sideDisplayRoot == null)
                sideDisplayRoot = FindChild("background_QuestSide");
            if (mainContentRoot == null && mainDisplayRoot != null)
            {
                mainContentRoot = mainDisplayRoot.transform.Find("Content")
                    as RectTransform;
            }
            if (sideContentRoot == null && sideDisplayRoot != null)
            {
                sideContentRoot = sideDisplayRoot.transform.Find("Content")
                    as RectTransform;
            }
        }

        private GameObject FindChild(string childName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < children.Length; index++)
            {
                if (children[index].name == childName)
                    return children[index].gameObject;
            }
            return null;
        }

        private void CaptureRootLayouts()
        {
            mainRootLayout = RootLayout.Capture(mainDisplayRoot);
            sideRootLayout = RootLayout.Capture(sideDisplayRoot);
        }

        private void DisableRootRaycasts()
        {
            DisableRootRaycast(mainDisplayRoot);
            DisableRootRaycast(sideDisplayRoot);
        }

        private static void DisableRootRaycast(GameObject root)
        {
            if (root != null && root.TryGetComponent(out Graphic background))
                background.raycastTarget = false;
        }

        private static string FormatGroups(
            IReadOnlyList<QuestDisplayGroup> groups)
        {
            StringBuilder builder = new StringBuilder();
            for (int groupIndex = 0;
                 groupIndex < groups.Count;
                 groupIndex++)
            {
                if (builder.Length > 0)
                    builder.Append('\n');

                QuestDisplayGroup group = groups[groupIndex];
                bool strikeTitle = !group.IsGrouped &&
                    group.Entries.Count == 1 &&
                    group.Entries[0].IsCompleted;
                AppendLine(builder, group.Title, strikeTitle);

                for (int entryIndex = 0;
                     entryIndex < group.Entries.Count;
                     entryIndex++)
                {
                    QuestDisplayEntry entry = group.Entries[entryIndex];
                    if (string.IsNullOrWhiteSpace(entry.Objective))
                        continue;

                    builder.Append('\n');
                    AppendLine(
                        builder,
                        "- " + entry.Objective,
                        entry.IsCompleted);
                }
            }
            return builder.ToString();
        }

        private static void AppendLine(
            StringBuilder builder,
            string text,
            bool strikethrough)
        {
            if (strikethrough)
                builder.Append("<s>");
            builder.Append(text);
            if (strikethrough)
                builder.Append("</s>");
        }

        private static void SetRootActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
                root.SetActive(active);
        }

        private readonly struct RootLayout
        {
            private RootLayout(Vector2 position, Vector2 size, float pivotY)
            {
                Position = position;
                Size = size;
                PivotY = pivotY;
                IsValid = true;
            }

            public Vector2 Position { get; }
            public Vector2 Size { get; }
            public float PivotY { get; }
            public bool IsValid { get; }

            public static RootLayout Capture(GameObject root)
            {
                return root != null &&
                    root.TryGetComponent(out RectTransform rect)
                        ? new RootLayout(
                            rect.anchoredPosition,
                            rect.sizeDelta,
                            rect.pivot.y)
                        : default;
            }

            public void Restore(GameObject root)
            {
                if (!IsValid || root == null ||
                    !root.TryGetComponent(out RectTransform rect))
                {
                    return;
                }

                rect.anchoredPosition = Position;
                rect.sizeDelta = Size;
            }

            public void SetHeight(GameObject root, float height)
            {
                if (!IsValid || root == null ||
                    !root.TryGetComponent(out RectTransform rect))
                {
                    return;
                }

                float originalTop = Position.y +
                    Size.y * (1f - PivotY);
                rect.sizeDelta = new Vector2(Size.x, height);
                rect.anchoredPosition = new Vector2(
                    Position.x,
                    originalTop - height * (1f - PivotY));
            }
        }

        private sealed class QuestDisplayGroup
        {
            private readonly List<QuestDisplayEntry> entries =
                new List<QuestDisplayEntry>();

            public QuestDisplayGroup(QuestDisplayEntry firstEntry)
            {
                GroupId = firstEntry.GroupId;
                Category = firstEntry.Category;
                Priority = firstEntry.Priority;
                Title = firstEntry.Title;
                IsGrouped = firstEntry.IsGrouped;
            }

            public string GroupId { get; }
            public QuestCategory Category { get; }
            public int Priority { get; private set; }
            public string Title { get; private set; }
            public bool IsGrouped { get; }
            public IReadOnlyList<QuestDisplayEntry> Entries => entries;
            public int ObjectiveCount
            {
                get
                {
                    int count = 0;
                    for (int index = 0; index < entries.Count; index++)
                    {
                        if (!string.IsNullOrWhiteSpace(entries[index].Objective))
                            count++;
                    }
                    return count;
                }
            }

            public void Add(QuestDisplayEntry entry)
            {
                entries.Add(entry);
                Priority = Mathf.Max(Priority, entry.Priority);
                if (!entry.IsCompleted)
                    Title = entry.Title;
            }

            public void SortEntries()
            {
                entries.Sort((left, right) =>
                {
                    int objective = string.Compare(
                        left.Objective,
                        right.Objective,
                        System.StringComparison.CurrentCultureIgnoreCase);
                    return objective != 0
                        ? objective
                        : string.CompareOrdinal(
                            left.InstanceId,
                            right.InstanceId);
                });
            }
        }

        private sealed class QuestDisplayEntry
        {
            private QuestDisplayEntry(
                string instanceId,
                string groupId,
                QuestCategory category,
                int priority,
                string title,
                string objective,
                bool isGrouped,
                bool isCompleted,
                float expiresAt)
            {
                InstanceId = instanceId;
                GroupId = groupId;
                Category = category;
                Priority = priority;
                Title = title;
                Objective = objective;
                IsGrouped = isGrouped;
                IsCompleted = isCompleted;
                ExpiresAt = expiresAt;
            }

            public string InstanceId { get; }
            public string GroupId { get; }
            public QuestCategory Category { get; }
            public int Priority { get; }
            public string Title { get; }
            public string Objective { get; }
            public bool IsGrouped { get; }
            public bool IsCompleted { get; }
            public float ExpiresAt { get; }

            public static QuestDisplayEntry From(QuestRuntimeState state)
            {
                bool isGrouped = state.Definition.UsesTriggeringObject;
                string groupId = isGrouped
                    ? state.QuestId
                    : state.InstanceId;
                string title = state.Title;
                if (string.IsNullOrWhiteSpace(title))
                    title = state.QuestId;

                string objective = string.IsNullOrWhiteSpace(
                        state.ObjectiveTitle)
                    ? state.ObjectiveDescription
                    : state.ObjectiveTitle;
                if (isGrouped && string.IsNullOrWhiteSpace(objective))
                {
                    objective = NERALocalization.Content(
                        "target",
                        state.ContextTargetId,
                        "name",
                        state.ContextTargetName);
                }

                return new QuestDisplayEntry(
                    state.InstanceId,
                    groupId,
                    state.Definition.Category,
                    state.Definition.Priority,
                    title,
                    objective,
                    isGrouped,
                    false,
                    0f);
            }

            public QuestDisplayEntry AsCompleted(float expiresAt)
            {
                return new QuestDisplayEntry(
                    InstanceId,
                    GroupId,
                    Category,
                    Priority,
                    Title,
                    Objective,
                    IsGrouped,
                    true,
                    expiresAt);
            }
        }
    }
}
