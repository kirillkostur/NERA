using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.UI
{
    public readonly struct HUDNotificationRequest
    {
        public HUDNotificationRequest(string id, object[] arguments)
        {
            Id = id;
            Arguments = arguments ?? Array.Empty<object>();
        }

        public string Id { get; }
        public object[] Arguments { get; }
    }

    public static class HUDNotificationService
    {
        private const int MaximumPendingCount = 32;
        private static readonly List<HUDNotificationRequest> Pending =
            new List<HUDNotificationRequest>();

        public static event Action QueueChanged;

        public static int PendingCount => Pending.Count;

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Pending.Clear();
            QueueChanged = null;
        }

        public static void Publish(string id, params object[] arguments)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            while (Pending.Count >= MaximumPendingCount)
                Pending.RemoveAt(0);

            object[] safeArguments = arguments == null || arguments.Length == 0
                ? Array.Empty<object>()
                : (object[])arguments.Clone();
            Pending.Add(new HUDNotificationRequest(
                id.Trim(),
                safeArguments));
            QueueChanged?.Invoke();
        }

        public static bool TryDequeueHighestPriority(
            Func<HUDNotificationRequest, int> prioritySelector,
            out HUDNotificationRequest request)
        {
            if (Pending.Count == 0)
            {
                request = default;
                return false;
            }

            int selectedIndex = 0;
            int selectedPriority = prioritySelector(Pending[0]);
            for (int index = 1; index < Pending.Count; index++)
            {
                int candidatePriority = prioritySelector(Pending[index]);
                if (candidatePriority <= selectedPriority)
                    continue;

                selectedIndex = index;
                selectedPriority = candidatePriority;
            }

            request = Pending[selectedIndex];
            Pending.RemoveAt(selectedIndex);
            return true;
        }

        public static void ClearPending()
        {
            Pending.Clear();
        }
    }
}
