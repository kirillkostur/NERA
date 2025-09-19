using UnityEngine;

public static class Logger
{
    public static bool Enabled = true; // этим флагом можно быстро включать/выключать свои логи

    public static void Log(object message) { if (Enabled) Debug.Log(message); }
    public static void LogWarning(object message) { if (Enabled) Debug.LogWarning(message); }
    public static void LogError(object message) { Debug.LogError(message); } // ошибки показываем всегда
}

