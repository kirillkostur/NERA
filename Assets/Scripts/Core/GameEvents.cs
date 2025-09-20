using System;

public static class GameEvents
{
    // 🌪 Буря (как было)
    public static event Action StormStarted;
    public static event Action StormEnded;

    public static void RaiseStormStarted() => StormStarted?.Invoke();
    public static void RaiseStormEnded() => StormEnded?.Invoke();

    // ✅ Квестовые события
    // Запуск аккумулятора (первый запуск или перезапуск — не важно)
    public static event Action BatteryStarted;
    public static void RaiseBatteryStarted() => BatteryStarted?.Invoke();

    // Любой успешный ремонт RepairableObject
    public static event Action<RepairableObject> ObjectRepaired;
    public static void RaiseObjectRepaired(RepairableObject obj) => ObjectRepaired?.Invoke(obj);

    // Убит паук
    public static event Action<SpiderHealth> SpiderKilled;
    public static void RaiseSpiderKilled(SpiderHealth spider) => SpiderKilled?.Invoke(spider);
}
