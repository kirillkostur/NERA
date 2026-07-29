namespace NERA.Core
{
    public enum GameLaunchMode
    {
        Continue,
        NewGame
    }

    /// <summary>
    /// Carries the menu choice across the Boot -> MainScene transition.
    /// The value is consumed once by the persistent runtime root.
    /// </summary>
    public static class GameSessionLaunchState
    {
        private static GameLaunchMode pendingMode;
        private static bool hasPendingMode;

        public static void Request(GameLaunchMode mode)
        {
            pendingMode = mode;
            hasPendingMode = true;
        }

        public static GameLaunchMode ConsumeOrDefault()
        {
            if (!hasPendingMode)
                return GameLaunchMode.Continue;

            hasPendingMode = false;
            return pendingMode;
        }

        public static void Clear()
        {
            hasPendingMode = false;
            pendingMode = GameLaunchMode.Continue;
        }
    }
}
