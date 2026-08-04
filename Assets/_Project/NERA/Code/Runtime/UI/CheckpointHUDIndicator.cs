using System.Collections;
using NERA.Save;
using UnityEngine;

namespace NERA.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class CheckpointHUDIndicator : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float resultDuration = 1.5f;

        private CanvasGroup canvasGroup;
        private Coroutine hideRoutine;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            SetVisible(false);
        }

        private void OnEnable()
        {
            CheckpointService.ActivityChanged += HandleActivity;
        }

        private void OnDisable()
        {
            CheckpointService.ActivityChanged -= HandleActivity;
            if (hideRoutine != null)
                StopCoroutine(hideRoutine);
            hideRoutine = null;
            SetVisible(false);
        }

        private void HandleActivity(CheckpointActivity activity)
        {
            if (activity != CheckpointActivity.Saved)
                return;

            if (hideRoutine != null)
                StopCoroutine(hideRoutine);
            SetVisible(true);
            hideRoutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            if (resultDuration > 0f)
                yield return new WaitForSecondsRealtime(resultDuration);
            SetVisible(false);
            hideRoutine = null;
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
