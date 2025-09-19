using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Главный аккумулятор станции.
/// - Единственный стартер игры (как старый GeneratorSystem): первый успешный запуск ставит gameStarted = true.
/// - Логика ремонта/поломки отделена от RepairableObject: у аккумулятора свои булы isRepaired/gameStarted.
/// - Зарядка днём от исправных панелей; подача питания, если isRepaired && gameStarted && charge ≥ threshold.
/// - При падении ниже порога питание выключается и аккумулятор «ломается» (чтобы игрок мог перезапускать).
/// </summary>
[RequireComponent(typeof(RepairableObject))]
public class StationBatterySystem : MonoBehaviour, IGameStartProvider
{
    public static StationBatterySystem Instance;

    [Header("Ссылки")]
    public DayNightCycle dayNight;

    [Header("Параметры аккумулятора")]
    [Range(0, 100)] public float startCharge = 50f;
    [Range(0, 100)] public float minPercentToSupply = 30f;

    [Header("Состояние (как у генератора)")]
    public bool isRepaired = false;   // текущая «починенность» аккумулятора
    public bool gameStarted = false;  // первый запуск игры совершён?

    [Header("Диагностика (readonly)")]
    [SerializeField, Range(0, 100)] private float chargePercent;
    [SerializeField] private bool hasPower = false;

    // IGameStartProvider
    public bool GameStarted => gameStarted;

    public float ChargePercent => chargePercent;
    public bool HasPower => hasPower;

    public event Action<bool> OnPowerStateChanged;
    public event Action<float> OnChargeChanged;

    private RepairableObject repairable;
    private readonly List<IPowerConsumer> consumers = new();
    private readonly List<SolarPanelSystem> panels = new();

    private void Awake()
    {
        Instance = this;
        repairable = GetComponent<RepairableObject>();

        // Синхронизируем визуальную «обёртку» с нашим флагом.
        if (repairable != null)
        {
            repairable.isRepaired = isRepaired;
            repairable.OnRepaired += OnBatteryRepairedByPlayer;
        }

        chargePercent = Mathf.Clamp(startCharge, 0, 100);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (repairable != null) repairable.OnRepaired -= OnBatteryRepairedByPlayer;
    }

    private void Update()
    {
        // === генерация днём от исправных панелей ===
        float genPerSec = 0f;
        if (dayNight != null && !dayNight.isNight)
        {
            for (int i = 0; i < panels.Count; i++)
                genPerSec += panels[i].GetCurrentOutput();
        }

        // === потребление, если реально подаём питание ===
        float consPerSec = 0f;
        if (hasPower)
        {
            for (int i = 0; i < consumers.Count; i++)
                if (consumers[i].IsConsuming())
                    consPerSec += consumers[i].GetConsumptionPerSecond();
        }

        float delta = (genPerSec - consPerSec) * Time.deltaTime;
        SetCharge(chargePercent + delta);

        // === решаем, подаём ли питание ===
        bool canSupply = isRepaired && gameStarted && chargePercent >= minPercentToSupply;
        if (canSupply != hasPower)
        {
            bool wasPower = hasPower;
            hasPower = canSupply;

            foreach (var c in consumers)
                c.OnPowerChanged(hasPower);

            OnPowerStateChanged?.Invoke(hasPower);
            Logger.Log(hasPower ? "🔌 Питание подано" : "🚫 Питание отключено");

            // если была реальная просадка питания — «ломаем» аккумулятор для ручного рестарта
            if (wasPower && !hasPower && isRepaired)
                BreakBattery(); // даст интерактив игроку
        }
    }

    private void SetCharge(float newPercent)
    {
        newPercent = Mathf.Clamp(newPercent, 0f, 100f);
        if (Mathf.Approximately(newPercent, chargePercent)) return;
        chargePercent = newPercent;
        OnChargeChanged?.Invoke(chargePercent);
    }

    /// <summary>
    /// Игрок закончил ремонт (удерживал E) на аккумуляторе.
    /// Здесь решаем: это первый запуск игры или обычный рестарт.
    /// </summary>
    private void OnBatteryRepairedByPlayer(RepairableObject _)
    {
        // Разрешаем «успех ремонта» только если заряда достаточно.
        if (chargePercent >= minPercentToSupply)
        {
            isRepaired = true;
            repairable.isRepaired = true;

            if (!gameStarted)
            {
                gameStarted = true;
                Logger.Log("🔋 Аккумулятор впервые активирован — игра начнётся с ближайшего рассвета.");
            }
            else
            {
                Logger.Log("🔧 Аккумулятор снова запущен.");
            }
        }
        else
        {
            Logger.Log($"⚠ Недостаточно заряда (< {minPercentToSupply}%) для запуска аккумулятора.");
            if (dayNight != null && dayNight.isNight)
                Logger.Log("ℹ Сейчас ночь — панели не заряжают. Дождитесь рассвета.");
            else if (GetPanelsOutputNow() <= 0.01f)
                Logger.Log("ℹ Зарядка не идёт. Проверь солнечные панели (возможно, их повредила буря).");

            repairable.BreakObject();
            if (repairable != null) repairable.ShowHighlight(); // оставляем подсветку
            isRepaired = false;
        }
    }


    private float GetPanelsOutputNow()
    {
        float s = 0f;
        for (int i = 0; i < panels.Count; i++) s += panels[i].GetCurrentOutput();
        return s;
    }

    // вызывать при реальной поломке/просадке
    private void BreakBattery()
    {
        isRepaired = false;
        if (repairable != null && repairable.isRepaired)
            repairable.BreakObject();
    }

    // === регистрация узлов ===
    public void RegisterConsumer(IPowerConsumer c)
    {
        if (!consumers.Contains(c))
        {
            consumers.Add(c);
            c.OnPowerChanged(hasPower);
        }
    }
    public void UnregisterConsumer(IPowerConsumer c) => consumers.Remove(c);

    public void RegisterPanel(SolarPanelSystem p)
    {
        if (!panels.Contains(p)) panels.Add(p);
    }
    public void UnregisterPanel(SolarPanelSystem p) => panels.Remove(p);
}
