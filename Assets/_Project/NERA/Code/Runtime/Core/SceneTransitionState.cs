namespace NERA.Core
{
    public enum SceneTransitionResult
    {
        None,
        Success,
        Failure
    }

    public static class SceneTransitionState
    {
        private static string pendingSpawnPointId;

        public static bool HasPendingSpawnPoint =>
            !string.IsNullOrWhiteSpace(pendingSpawnPointId);

        public static void SetPendingSpawnPoint(string spawnPointId)
        {
            pendingSpawnPointId = spawnPointId?.Trim();
        }

        public static bool TryConsumeSpawnPoint(string spawnPointId)
        {
            if (string.IsNullOrWhiteSpace(pendingSpawnPointId) ||
                pendingSpawnPointId != spawnPointId)
            {
                return false;
            }

            pendingSpawnPointId = null;
            return true;
        }

        public static void ClearPendingSpawnPoint()
        {
            pendingSpawnPointId = null;
        }
    }
}
