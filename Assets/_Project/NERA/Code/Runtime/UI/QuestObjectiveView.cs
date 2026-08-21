using TMPro;
using UnityEngine;

namespace NERA.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMeshProUGUI))]
    public sealed class QuestObjectiveView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        private FontStyles normalFontStyle;
        private bool initialized;

        public TMP_Text Label => label;
        public bool IsCompleted { get; private set; }
        public string DisplayText => label != null
            ? label.text
            : string.Empty;

        private void Awake()
        {
            Initialize();
        }

        private void OnValidate()
        {
            ResolveReferences();
            if (label != null)
                label.raycastTarget = false;
        }

        public void Configure(string objective, bool completed)
        {
            Initialize();
            IsCompleted = completed;
            if (label == null)
                return;

            label.text = "- " + (objective ?? string.Empty);
            label.fontStyle = completed
                ? normalFontStyle | FontStyles.Strikethrough
                : normalFontStyle;
        }

        public void ConfigureTemplate(TMP_Text textLabel)
        {
            label = textLabel;
            initialized = false;
            Initialize();
        }

        private void Initialize()
        {
            if (initialized)
                return;

            ResolveReferences();
            if (label != null)
            {
                normalFontStyle = label.fontStyle &
                    ~FontStyles.Strikethrough;
                label.fontStyle = normalFontStyle;
                label.raycastTarget = false;
            }

            initialized = true;
        }

        private void ResolveReferences()
        {
            if (label == null)
                label = GetComponent<TMP_Text>();
        }
    }
}
