using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LayoutElement))]
    public sealed class QuestGroupView : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private RectTransform objectivesRoot;

        private readonly List<QuestObjectiveView> objectiveViews =
            new List<QuestObjectiveView>();

        private LayoutElement layoutElement;
        private FontStyles normalTitleFontStyle;
        private bool initialized;

        public RectTransform RectTransform => (RectTransform)transform;
        public string Title => titleLabel != null
            ? titleLabel.text
            : string.Empty;
        public bool IsTitleCompleted { get; private set; }
        public float PreferredHeight => layoutElement != null
            ? layoutElement.preferredHeight
            : RectTransform.rect.height;

        private void Awake()
        {
            Initialize();
        }

        private void OnValidate()
        {
            ResolveReferences();
            DisableRaycasts();
        }

        public void BeginUpdate(
            string title,
            bool titleCompleted,
            int objectiveCount,
            QuestObjectiveView objectivePrefab)
        {
            Initialize();
            IsTitleCompleted = titleCompleted;
            if (titleLabel != null)
            {
                titleLabel.text = title ?? string.Empty;
                titleLabel.fontStyle = titleCompleted
                    ? normalTitleFontStyle | FontStyles.Strikethrough
                    : normalTitleFontStyle;
            }

            EnsureObjectiveCount(objectiveCount, objectivePrefab);
            for (int index = 0; index < objectiveViews.Count; index++)
                objectiveViews[index].gameObject.SetActive(index < objectiveCount);
            UpdatePreferredHeight(objectiveCount);
        }

        public void SetObjective(
            int index,
            string objective,
            bool completed)
        {
            if (index < 0 || index >= objectiveViews.Count)
                return;

            QuestObjectiveView view = objectiveViews[index];
            view.gameObject.SetActive(true);
            view.Configure(objective, completed);
        }

        public void ConfigureTemplate(
            TMP_Text title,
            RectTransform objectiveContainer)
        {
            titleLabel = title;
            objectivesRoot = objectiveContainer;
            initialized = false;
            Initialize();
        }

        private void EnsureObjectiveCount(
            int count,
            QuestObjectiveView objectivePrefab)
        {
            if (objectivesRoot == null || objectivePrefab == null)
                return;

            while (objectiveViews.Count < count)
            {
                QuestObjectiveView view = Instantiate(
                    objectivePrefab,
                    objectivesRoot);
                view.name = $"Quest_Text_{objectiveViews.Count:00}";
                view.gameObject.SetActive(true);
                objectiveViews.Add(view);
            }
        }

        private void UpdatePreferredHeight(int activeObjectiveCount)
        {
            if (layoutElement == null)
                return;

            float height = GetElementHeight(
                titleLabel != null ? titleLabel.rectTransform : null,
                30f);
            for (int index = 0;
                 index < activeObjectiveCount && index < objectiveViews.Count;
                 index++)
            {
                height += GetElementHeight(
                    objectiveViews[index].transform as RectTransform,
                    30f);
            }

            layoutElement.minHeight = height;
            layoutElement.preferredHeight = height;
            RectTransform.SetSizeWithCurrentAnchors(
                RectTransform.Axis.Vertical,
                height);

            if (objectivesRoot != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(objectivesRoot);
            LayoutRebuilder.ForceRebuildLayoutImmediate(RectTransform);
        }

        private static float GetElementHeight(
            RectTransform rect,
            float fallback)
        {
            if (rect == null)
                return fallback;

            LayoutElement element = rect.GetComponent<LayoutElement>();
            if (element != null && element.preferredHeight > 0f)
                return element.preferredHeight;
            return rect.sizeDelta.y > 0f
                ? rect.sizeDelta.y
                : fallback;
        }

        private void Initialize()
        {
            if (initialized)
                return;

            ResolveReferences();
            layoutElement = GetComponent<LayoutElement>();
            if (titleLabel != null)
            {
                normalTitleFontStyle = titleLabel.fontStyle &
                    ~FontStyles.Strikethrough;
                titleLabel.fontStyle = normalTitleFontStyle;
            }
            DisableRaycasts();
            initialized = true;
        }

        private void ResolveReferences()
        {
            if (titleLabel == null)
            {
                Transform title = transform.Find("QuestTitle_Text");
                if (title != null)
                    titleLabel = title.GetComponent<TMP_Text>();
            }

            if (objectivesRoot == null)
            {
                Transform root = transform.Find("Objectives");
                if (root != null)
                    objectivesRoot = root as RectTransform;
            }
        }

        private void DisableRaycasts()
        {
            if (titleLabel != null)
                titleLabel.raycastTarget = false;
        }
    }
}
