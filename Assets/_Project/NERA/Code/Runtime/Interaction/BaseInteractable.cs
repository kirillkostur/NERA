using UnityEngine;
using UnityEngine.Events;

namespace NERA.Interaction
{
    public class BaseInteractable : MonoBehaviour, IInteractable
    {
        [Header("Prompt")]
        [SerializeField] private string actionText = "Interact";
        [SerializeField] private InteractionMode mode = InteractionMode.Press;
        [SerializeField, Min(0.1f)] private float holdDuration = 1f;

        [Header("Availability")]
        [SerializeField] private bool isAvailable = true;
        [SerializeField] private string unavailableReason = "Unavailable";

        [Header("Events")]
        [SerializeField] private UnityEvent onInteractionStarted;
        [SerializeField] private UnityEvent onInteractionCancelled;
        [SerializeField] private UnityEvent onInteractionCompleted;

        public Transform InteractionTransform => transform;

        public virtual InteractionPrompt GetPrompt()
        {
            return new InteractionPrompt(
                actionText,
                mode,
                holdDuration,
                isAvailable,
                unavailableReason
            );
        }

        public virtual void BeginInteraction(GameObject interactor)
        {
            onInteractionStarted?.Invoke();
        }

        public virtual void CancelInteraction(GameObject interactor)
        {
            onInteractionCancelled?.Invoke();
        }

        public virtual void CompleteInteraction(GameObject interactor)
        {
            onInteractionCompleted?.Invoke();
        }

        public void SetAvailable(bool available, string reason = null)
        {
            isAvailable = available;

            if (!string.IsNullOrWhiteSpace(reason))
                unavailableReason = reason;
        }

        public void SetActionText(string newActionText)
        {
            if (!string.IsNullOrWhiteSpace(newActionText))
                actionText = newActionText;
        }

        private void OnValidate()
        {
            holdDuration = Mathf.Max(0.1f, holdDuration);
        }
    }
}
