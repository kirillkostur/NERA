namespace NERA.Core
{
    public static class SceneTransitionState
    {
        private static string pendingSpawnPointId;

        public static void SetPendingSpawnPoint(string spawnPointId)
        {
            pendingSpawnPointId = spawnPointId;
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
    }
}
