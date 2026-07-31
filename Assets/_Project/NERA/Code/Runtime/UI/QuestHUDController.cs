using System.Collections.Generic;
using NERA.Quests;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.UI
{
    [DisallowMultipleComponent]
    public sealed class QuestHUDController : MonoBehaviour
    {
        [Header("View")]
        [SerializeField] private GameObject displayRoot;
        [SerializeField] private TMP_Text mainQuestText;
        [SerializeField] private TMP_Text sideQuestText;

        [Header("Labels")]
        [SerializeField] private string mainHeader = "ОСНОВНОЕ ЗАДАНИЕ";
        [SerializeField] private string sideHeader = "ПОБОЧНОЕ ЗАДАНИЕ";
        [SerializeField, Min(8)] private int objectiveFontSize = 20;

        private QuestController questController;

        public string DisplayedMainText => mainQuestText != null
            ? mainQuestText.text
            : string.Empty;
        public string DisplayedSideText => sideQuestText != null
            ? sideQuestText.text
            : string.Empty;
        public bool IsVisible => displayRoot != null && displayRoot.activeSelf;

        private void Reset()
        {
            ResolveViewReferences();
        }

        private void Awake()
        {
            ResolveViewReferences();
            DisableRaycastTargets();
        }

        private void OnEnable()
        {
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
        }

        private void OnDisable()
        {
            Bind(null);
        }

        private void OnDestroy()
        {
            Bind(null);
        }

        public void Refresh()
        {
            QuestRuntimeState mainQuest = null;
            QuestRuntimeState sideQuest = null;
            IReadOnlyList<QuestRuntimeState> activeQuests =
                questController?.ActiveQuests;

            if (activeQuests != null)
            {
                for (int index = 0; index < activeQuests.Count; index++)
                {
                    QuestRuntimeState state = activeQuests[index];
                    if (state?.Definition == null ||
                        !state.Definition.ShowInHud)
                    {
                        continue;
                    }

                    if (state.Definition.Category == QuestCategory.Main)
                    {
                        if (mainQuest == null)
                            mainQuest = state;
                    }
                    else if (sideQuest == null)
                    {
                        sideQuest = state;
                    }

                    if (mainQuest != null && sideQuest != null)
                        break;
                }
            }

            SetText(mainQuestText, mainQuest, mainHeader);
            SetText(sideQuestText, sideQuest, sideHeader);

            if (displayRoot != null && displayRoot != gameObject)
            {
                displayRoot.SetActive(
                    mainQuest != null || sideQuest != null);
            }
        }

        private void Bind(QuestController controller)
        {
            if (questController == controller)
                return;

            if (questController != null)
                questController.QuestsChanged -= Refresh;

            questController = controller;
            if (questController != null)
                questController.QuestsChanged += Refresh;

            Refresh();
        }

        private void ResolveViewReferences()
        {
            Transform background = transform.Find("background");
            if (displayRoot == null && background != null)
                displayRoot = background.gameObject;

            TMP_Text[] labels = GetComponentsInChildren<TMP_Text>(true);
            for (int index = 0; index < labels.Length; index++)
            {
                TMP_Text label = labels[index];
                if (mainQuestText == null && label.name == "Text - QuestMain")
                    mainQuestText = label;
                else if (sideQuestText == null &&
                         label.name == "Text - QuestSide")
                {
                    sideQuestText = label;
                }
            }
        }

        private void DisableRaycastTargets()
        {
            if (mainQuestText != null)
                mainQuestText.raycastTarget = false;
            if (sideQuestText != null)
                sideQuestText.raycastTarget = false;

            if (displayRoot != null &&
                displayRoot.TryGetComponent(out Graphic background))
            {
                background.raycastTarget = false;
            }
        }

        private void SetText(
            TMP_Text label,
            QuestRuntimeState state,
            string header)
        {
            if (label == null)
                return;

            if (state == null)
            {
                label.text = string.Empty;
                return;
            }

            string title = string.IsNullOrWhiteSpace(state.Title)
                ? state.QuestId
                : state.Title;
            string objective = string.IsNullOrWhiteSpace(state.ObjectiveTitle)
                ? state.ObjectiveDescription
                : state.ObjectiveTitle;

            label.text =
                $"<b>{header}</b>\n{title}\n" +
                $"<size={objectiveFontSize}>{objective}</size>";
        }
    }
}
