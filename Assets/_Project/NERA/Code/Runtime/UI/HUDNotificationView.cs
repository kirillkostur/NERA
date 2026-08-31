using NERA.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.UI
{
    public sealed class HUDNotificationView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image accent;
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private CanvasGroup canvasGroup;

        private HUDNotificationDefinition definition;
        private object[] arguments;

        public string NotificationId => definition?.Id ?? string.Empty;
        public string Message => messageText != null
            ? messageText.text
            : string.Empty;
        public CanvasGroup CanvasGroup => canvasGroup;

        public void Initialize(
            HUDNotificationDefinition notificationDefinition,
            object[] formatArguments,
            HUDNotificationCatalog catalog)
        {
            definition = notificationDefinition;
            arguments = formatArguments;

            if (background != null)
                background.color = catalog.GetBackground(definition.Severity);
            if (accent != null)
                accent.color = catalog.GetAccent(definition.Severity);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            RefreshLocalization();
        }

        public void RefreshLocalization()
        {
            if (messageText == null || definition == null)
                return;

            messageText.text = NERALocalization.Get(
                NERALocalization.HudTable,
                definition.LocalizationKey,
                string.Empty,
                arguments);
        }
    }
}
