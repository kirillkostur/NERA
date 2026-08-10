using NERA.Interaction;
using NERA.Localization;
using TMPro;
using UnityEngine;

namespace NERA.UI
{
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private PlayerInteractionController interactionController;

        [Header("View")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private string interactionKeyLabel = "E";

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (promptText == null)
                promptText = GetComponentInChildren<TMP_Text>(true);
        }

        private void OnEnable()
        {
            NERALocalization.LocaleChanged += Refresh;
            if (interactionController != null)
            {
                interactionController.TargetChanged += Refresh;
                interactionController.InteractionStateChanged += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            NERALocalization.LocaleChanged -= Refresh;
            if (interactionController == null)
                return;

            interactionController.TargetChanged -= Refresh;
            interactionController.InteractionStateChanged -= Refresh;
        }

        public void SetInteractionController(PlayerInteractionController controller)
        {
            if (isActiveAndEnabled && interactionController != null)
            {
                interactionController.TargetChanged -= Refresh;
                interactionController.InteractionStateChanged -= Refresh;
            }

            interactionController = controller;

            if (isActiveAndEnabled && interactionController != null)
            {
                interactionController.TargetChanged += Refresh;
                interactionController.InteractionStateChanged += Refresh;
            }

            Refresh();
        }

        private void Refresh()
        {
            IInteractable interactable = interactionController != null
                ? interactionController.CurrentInteractable
                : null;

            SetVisible(interactable != null);

            if (interactable == null)
                return;

            InteractionPrompt prompt = interactable.GetPrompt();
            SetVisible(prompt.IsVisible);
            if (!prompt.IsVisible)
                return;

            if (promptText != null)
                promptText.text = BuildPromptText(prompt);
        }

        private string BuildPromptText(InteractionPrompt prompt)
        {
            if (!prompt.IsAvailable)
                return prompt.UnavailableReason;

            if (prompt.Mode == InteractionMode.Press)
                return NERALocalization.Get(
                    NERALocalization.HudTable,
                    "interaction.press",
                    "[{0}] Press — {1}",
                    interactionKeyLabel,
                    prompt.ActionText);

            if (!interactionController.IsInteracting)
                return NERALocalization.Get(
                    NERALocalization.HudTable,
                    "interaction.hold",
                    "[{0}] Hold — {1}",
                    interactionKeyLabel,
                    prompt.ActionText);

            int percentage = Mathf.RoundToInt(interactionController.HoldProgress * 100f);
            return NERALocalization.Get(
                NERALocalization.HudTable,
                "interaction.hold_progress",
                "[{0}] Hold — {1}%",
                interactionKeyLabel,
                percentage);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }
}
