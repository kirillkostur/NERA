using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Localization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class LanguageToggleButton : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            label ??= GetComponentInChildren<TMP_Text>(true);
            button.onClick.AddListener(NERALocalization.ToggleEnglishRussian);
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

        private void OnDestroy()
        {
            if (button != null)
                button.onClick.RemoveListener(NERALocalization.ToggleEnglishRussian);
        }

        private void Refresh()
        {
            label ??= GetComponentInChildren<TMP_Text>(true);
            if (label == null)
                return;

            string language = NERALocalization.CurrentLocaleCode.StartsWith(
                NERALocalization.RussianCode,
                System.StringComparison.OrdinalIgnoreCase)
                    ? "РУССКИЙ"
                    : "ENGLISH";
            label.text = NERALocalization.Get(
                NERALocalization.MainMenuTable,
                "options.language",
                "LANGUAGE: {0}",
                language);
        }
    }
}
