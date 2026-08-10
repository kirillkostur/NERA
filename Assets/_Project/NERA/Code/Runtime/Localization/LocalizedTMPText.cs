using TMPro;
using UnityEngine;

namespace NERA.Localization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedTMPText : MonoBehaviour
    {
        [SerializeField] private string table;
        [SerializeField] private string key;
        [SerializeField, TextArea] private string fallback;

        private TMP_Text label;

        public string Table => table;
        public string Key => key;

        private void Awake()
        {
            label = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            NERALocalization.LocaleChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            NERALocalization.LocaleChanged -= Refresh;
        }

        public void Configure(string tableName, string entryKey, string fallbackText)
        {
            table = tableName;
            key = entryKey;
            fallback = fallbackText;
            if (Application.isPlaying)
                Refresh();
        }

        public void Refresh()
        {
            label ??= GetComponent<TMP_Text>();
            if (label != null)
                label.text = NERALocalization.Get(table, key, fallback);
        }
    }
}
