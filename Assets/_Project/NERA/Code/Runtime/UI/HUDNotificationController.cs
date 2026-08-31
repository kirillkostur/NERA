using System.Collections;
using System.Collections.Generic;
using NERA.Localization;
using UnityEngine;

namespace NERA.UI
{
    public sealed class HUDNotificationController : MonoBehaviour
    {
        private sealed class ActiveNotification
        {
            public HUDNotificationView View;
        }

        [SerializeField] private HUDNotificationCatalog catalog;
        [SerializeField] private HUDNotificationView notificationPrefab;
        [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.16f;
        [SerializeField, Min(0.01f)] private float fadeOutSeconds = 0.22f;

        private readonly List<ActiveNotification> active =
            new List<ActiveNotification>();

        public int ActiveCount => active.Count;
        public int QueuedCount => HUDNotificationService.PendingCount;
        public string ActiveNotificationId =>
            active.Count > 0 && active[0].View != null
                ? active[0].View.NotificationId
                : string.Empty;

        private void Awake()
        {
            catalog ??= HUDNotificationCatalog.LoadDefault();
            notificationPrefab ??= Resources.Load<HUDNotificationView>(
                "UI/P_HUD_Notification");
        }

        private void OnEnable()
        {
            NERALocalization.EnsureInitialized();
            HUDNotificationService.QueueChanged += HandleQueueChanged;
            NERALocalization.LocaleChanged += RefreshActiveLocalization;
            DrainQueue();
        }

        private void OnDisable()
        {
            HUDNotificationService.QueueChanged -= HandleQueueChanged;
            NERALocalization.LocaleChanged -= RefreshActiveLocalization;
            StopAllCoroutines();

            for (int index = active.Count - 1; index >= 0; index--)
            {
                if (active[index].View != null)
                    Destroy(active[index].View.gameObject);
            }
            active.Clear();
        }

        private void HandleQueueChanged()
        {
            DrainQueue();
        }

        private void DrainQueue()
        {
            if (!isActiveAndEnabled || catalog == null ||
                notificationPrefab == null)
            {
                return;
            }

            while (active.Count == 0 &&
                   HUDNotificationService.TryDequeueHighestPriority(
                       GetPriority,
                       out HUDNotificationRequest request))
            {
                if (!catalog.TryGet(request.Id, out
                        HUDNotificationDefinition definition))
                {
                    Debug.LogWarning(
                        $"HUD notification '{request.Id}' is not present " +
                        "in the notification catalog.",
                        this);
                    continue;
                }

                HUDNotificationView view = Instantiate(
                    notificationPrefab,
                    transform,
                    false);
                view.name = $"Notification_{definition.Id}";
                view.Initialize(definition, request.Arguments, catalog);

                ActiveNotification item = new ActiveNotification
                {
                    View = view
                };
                active.Add(item);
                StartCoroutine(
                    AnimateAndRelease(item, definition.VisibleSeconds));
            }
        }

        private IEnumerator AnimateAndRelease(
            ActiveNotification item,
            float visibleSeconds)
        {
            CanvasGroup group = item.View.CanvasGroup;
            yield return Fade(group, 0f, 1f, fadeInSeconds);

            float remaining = Mathf.Max(0f, visibleSeconds);
            while (remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            yield return Fade(group, 1f, 0f, fadeOutSeconds);
            Release(item);
        }

        private static IEnumerator Fade(
            CanvasGroup group,
            float from,
            float to,
            float duration)
        {
            if (group == null)
                yield break;

            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);
            while (elapsed < safeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(
                    from,
                    to,
                    Mathf.Clamp01(elapsed / safeDuration));
                yield return null;
            }
            group.alpha = to;
        }

        private void Release(ActiveNotification item)
        {
            active.Remove(item);
            if (item.View != null)
                Destroy(item.View.gameObject);
            DrainQueue();
        }

        private int GetPriority(HUDNotificationRequest request)
        {
            if (!catalog.TryGet(
                    request.Id,
                    out HUDNotificationDefinition definition))
            {
                return int.MinValue;
            }

            return definition.Severity switch
            {
                HUDNotificationSeverity.Critical => 300,
                HUDNotificationSeverity.Warning => 200,
                _ => 100
            };
        }

        private void RefreshActiveLocalization()
        {
            foreach (ActiveNotification item in active)
                item.View?.RefreshLocalization();
        }
    }
}
