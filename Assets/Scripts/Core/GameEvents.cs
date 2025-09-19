using System;

public static class GameEvents
{
    // 🌪 Буря
    public static event Action StormStarted;
    public static event Action StormEnded;

    public static void RaiseStormStarted() => StormStarted?.Invoke();
    public static void RaiseStormEnded() => StormEnded?.Invoke();
}
