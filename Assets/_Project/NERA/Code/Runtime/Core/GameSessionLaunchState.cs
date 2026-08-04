using NERA.Save;

namespace NERA.Core
{
    public enum GameLaunchMode
    {
        Continue,
        NewGame
    }

    /// <summary>
    /// Immutable menu choice transferred to the runtime as one value so the
    /// launch mode and save slot cannot be consumed independently.
    /// </summary>
    public readonly struct GameSessionLaunchRequest
    {
        public GameSessionLaunchRequest(GameLaunchMode mode, int saveSlot)
        {
            Mode = mode;
            SaveSlot = SaveSlotStorage.NormalizeSlot(saveSlot);
        }

        public GameLaunchMode Mode { get; }
        public int SaveSlot { get; }
    }

    /// <summary>
    /// Carries the menu choice across the Boot -> MainScene transition.
    /// The value is consumed once by the persistent runtime root.
    /// </summary>
    public static class GameSessionLaunchState
    {
        private static GameLaunchMode pendingMode;
        private static int pendingSaveSlot = SaveSlotStorage.DefaultSlot;
        private static bool hasPendingMode;

        public static void Request(GameLaunchMode mode)
        {
            Request(mode, SaveSlotStorage.DefaultSlot);
        }

        public static void Request(GameLaunchMode mode, int saveSlot)
        {
            pendingMode = mode;
            pendingSaveSlot = SaveSlotStorage.NormalizeSlot(saveSlot);
            hasPendingMode = true;
        }

        public static GameSessionLaunchRequest ConsumeOrDefault()
        {
            if (!hasPendingMode)
                return new GameSessionLaunchRequest(
                    GameLaunchMode.Continue,
                    SaveSlotStorage.DefaultSlot);

            hasPendingMode = false;
            GameSessionLaunchRequest request =
                new GameSessionLaunchRequest(pendingMode, pendingSaveSlot);
            pendingSaveSlot = SaveSlotStorage.DefaultSlot;
            return request;
        }

        public static void Clear()
        {
            hasPendingMode = false;
            pendingMode = GameLaunchMode.Continue;
            pendingSaveSlot = SaveSlotStorage.DefaultSlot;
        }
    }
}
