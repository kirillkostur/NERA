using System;
using System.Collections.Generic;
using NERA.Quests;
using NERA.Station;
using UnityEngine;

namespace NERA.Energy
{
    public sealed class EnergySystemController : MonoBehaviour
    {
        private sealed class BatteryRecord
        {
            public float Capacity;
            public float InitialCharge;
        }

        private sealed class SolarRecord
        {
            public float OutputMultiplier;
        }

        private sealed class ConsumerRecord
        {
            public float Rate;
            public float MinimumCharge01;
            public bool RequestedActive;
            public bool Powered;
            public StationSystemType? StationSystem;
            public string StationObjectId;
        }

        [SerializeField] private EnergyBalanceConfig config;
        [SerializeField] private bool gridEnabled;
        [SerializeField] private float currentEnergy;

        private readonly Dictionary<string, BatteryRecord> batteries =
            new Dictionary<string, BatteryRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, SolarRecord> solarPanels =
            new Dictionary<string, SolarRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, ConsumerRecord> consumers =
            new Dictionary<string, ConsumerRecord>(StringComparer.Ordinal);

        private bool restoredFromSave;
        private EnergyState state = EnergyState.Offline;
        private float lastQuestReportedCharge01 = float.NaN;
        private StationSystemsController stationSystems;

        public static EnergySystemController Instance { get; private set; }

        public event Action EnergyChanged;
        public event Action<EnergyState> StateChanged;

        public EnergyBalanceConfig Config =>
            config != null ? config : config = EnergyBalanceConfig.LoadDefault();
        public float CurrentEnergy => currentEnergy;
        public float TotalCapacity { get; private set; }
        public float CurrentGeneration { get; private set; }
        public float CurrentConsumption { get; private set; }
        public float Charge01 =>
            TotalCapacity > 0f ? Mathf.Clamp01(currentEnergy / TotalCapacity) : 0f;
        public bool GridEnabled => gridEnabled;
        public bool IsRestoringState { get; private set; }
        public bool HasUsablePower =>
            gridEnabled && currentEnergy > 0.001f && TotalCapacity > 0f;
        public EnergyState State => state;
        public int ConnectedConsumerCount
        {
            get
            {
                HashSet<string> connections = new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, ConsumerRecord> pair in
                         consumers)
                {
                    ConsumerRecord consumer = pair.Value;
                    if (!IsConsumerConnected(consumer))
                        continue;

                    string connectionId = consumer.StationSystem.HasValue
                        ? $"system:{(int)consumer.StationSystem.Value}:" +
                          consumer.StationObjectId
                        : $"consumer:{pair.Key}";
                    connections.Add(connectionId);
                }
                return connections.Count;
            }
        }
        public int RegisteredConsumerCount => consumers.Count;
        public int ActiveConsumerCount
        {
            get
            {
                int count = 0;
                foreach (ConsumerRecord consumer in consumers.Values)
                {
                    if (consumer.Powered)
                        count++;
                }
                return count;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            StationSystemsController.InstanceChanged +=
                HandleStationSystemsInstanceChanged;
            BindStationSystems(StationSystemsController.Instance);
            RefreshState();
        }

        private void Update()
        {
            AdvanceSimulation(Time.deltaTime);
        }

        public void AdvanceSimulation(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            CurrentGeneration = CalculateGeneration();
            RefreshState();
            RefreshConsumers();
            CurrentConsumption = CalculateConsumption();

            if (TotalCapacity > 0f)
            {
                currentEnergy = Mathf.Clamp(
                    currentEnergy + (CurrentGeneration - CurrentConsumption) * deltaTime,
                    0f,
                    TotalCapacity
                );
            }
            else
            {
                currentEnergy = 0f;
            }

            RefreshState();
            RefreshConsumers();
            EnergyChanged?.Invoke();
            ReportQuestCharge();
        }

        public bool RegisterBattery(
            string batteryId,
            float capacity,
            float initialCharge
        )
        {
            if (string.IsNullOrWhiteSpace(batteryId) || capacity <= 0f)
                return false;

            float clampedInitialCharge = Mathf.Clamp(
                initialCharge,
                0f,
                capacity);
            if (batteries.TryGetValue(
                    batteryId,
                    out BatteryRecord existing))
            {
                if (Mathf.Approximately(existing.Capacity, capacity) &&
                    Mathf.Approximately(
                        existing.InitialCharge,
                        clampedInitialCharge))
                {
                    return true;
                }

                TotalCapacity = Mathf.Max(
                    0f,
                    TotalCapacity - existing.Capacity + capacity);
                existing.Capacity = capacity;
                existing.InitialCharge = clampedInitialCharge;
                currentEnergy = TotalCapacity > 0f
                    ? Mathf.Min(currentEnergy, TotalCapacity)
                    : 0f;

                RefreshState();
                RefreshConsumers();
                EnergyChanged?.Invoke();
                ReportQuestCharge();
                return true;
            }

            batteries.Add(
                batteryId,
                new BatteryRecord
                {
                    Capacity = capacity,
                    InitialCharge = clampedInitialCharge
                }
            );
            TotalCapacity += capacity;

            if (!restoredFromSave)
                currentEnergy = Mathf.Min(
                    TotalCapacity,
                    currentEnergy + clampedInitialCharge);
            else
                currentEnergy = Mathf.Min(currentEnergy, TotalCapacity);

            RefreshState();
            EnergyChanged?.Invoke();
            ReportQuestCharge();
            return true;
        }

        public bool RegisterSolarPanel(
            string panelId,
            float outputMultiplier
        )
        {
            if (string.IsNullOrWhiteSpace(panelId))
                return false;

            if (solarPanels.TryGetValue(panelId, out SolarRecord existing))
            {
                existing.OutputMultiplier = Mathf.Max(0f, outputMultiplier);
                return true;
            }

            solarPanels.Add(
                panelId,
                new SolarRecord
                {
                    OutputMultiplier = Mathf.Max(0f, outputMultiplier)
                }
            );
            return true;
        }

        public void RegisterConsumer(
            string consumerId,
            float rate,
            bool disableInEmergency
        )
        {
            RegisterConsumer(
                consumerId,
                rate,
                disableInEmergency
                    ? Config.DefaultConsumerMinimumCharge01
                    : 0f);
        }

        public void RegisterConsumer(
            string consumerId,
            float rate,
            float minimumCharge01
        )
        {
            RegisterConsumerInternal(
                consumerId,
                rate,
                minimumCharge01,
                null,
                null);
        }

        public void RegisterConsumer(
            string consumerId,
            float rate,
            float minimumCharge01,
            StationSystemType stationSystem,
            string stationObjectId = null
        )
        {
            RegisterConsumerInternal(
                consumerId,
                rate,
                minimumCharge01,
                stationSystem,
                stationObjectId);
        }

        private void RegisterConsumerInternal(
            string consumerId,
            float rate,
            float minimumCharge01,
            StationSystemType? stationSystem,
            string stationObjectId
        )
        {
            if (string.IsNullOrWhiteSpace(consumerId))
                return;

            float clampedRate = Mathf.Max(0f, rate);
            float clampedMinimumCharge = Mathf.Clamp01(minimumCharge01);
            if (!consumers.TryGetValue(consumerId, out ConsumerRecord consumer))
            {
                consumer = new ConsumerRecord();
                consumers.Add(consumerId, consumer);
            }
            else if (Mathf.Approximately(consumer.Rate, clampedRate) &&
                     Mathf.Approximately(
                         consumer.MinimumCharge01,
                         clampedMinimumCharge) &&
                     consumer.StationSystem == stationSystem &&
                     string.Equals(
                         consumer.StationObjectId,
                         stationObjectId,
                         StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            consumer.Rate = clampedRate;
            consumer.MinimumCharge01 = clampedMinimumCharge;
            consumer.StationSystem = stationSystem;
            consumer.StationObjectId = stationObjectId?.Trim() ?? string.Empty;
            RefreshConsumers();
            EnergyChanged?.Invoke();
            ReportQuestCharge();
        }

        public void SetConsumerActive(string consumerId, bool active)
        {
            if (!consumers.TryGetValue(consumerId, out ConsumerRecord consumer))
                return;
            if (consumer.RequestedActive == active)
                return;

            consumer.RequestedActive = active;
            RefreshConsumers();
            EnergyChanged?.Invoke();
            ReportQuestCharge();
        }

        public bool IsConsumerPowered(string consumerId)
        {
            return consumers.TryGetValue(consumerId, out ConsumerRecord consumer) &&
                   consumer.Powered;
        }

        public bool CanPowerConsumer(string consumerId)
        {
            return consumers.TryGetValue(consumerId, out ConsumerRecord consumer) &&
                   HasSufficientCharge(consumer.MinimumCharge01);
        }

        public bool HasSufficientCharge(float minimumCharge01)
        {
            return HasUsablePower &&
                   Charge01 + 0.0001f >= Mathf.Clamp01(minimumCharge01);
        }

        public bool CanSpendEnergy(float amount)
        {
            return amount <= 0f ||
                (GridEnabled && CurrentEnergy >= amount);
        }

        public bool TrySpendEnergy(float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (!CanSpendEnergy(amount))
                return false;

            currentEnergy = Mathf.Max(0f, currentEnergy - amount);
            RefreshConsumers();
            RefreshState();
            EnergyChanged?.Invoke();
            ReportQuestCharge();
            return true;
        }

        public bool IsConsumerRequestedActive(string consumerId)
        {
            return consumers.TryGetValue(consumerId, out ConsumerRecord consumer) &&
                consumer.RequestedActive;
        }

        public bool IsConsumerConnected(string consumerId)
        {
            return consumers.TryGetValue(
                    consumerId,
                    out ConsumerRecord consumer) &&
                IsConsumerConnected(consumer);
        }

        public float GetConsumerRate(string consumerId)
        {
            return consumers.TryGetValue(consumerId, out ConsumerRecord consumer)
                ? consumer.Rate
                : 0f;
        }

        public void UnregisterConsumer(string consumerId)
        {
            if (string.IsNullOrWhiteSpace(consumerId) ||
                !consumers.Remove(consumerId))
            {
                return;
            }

            RefreshConsumers();
            EnergyChanged?.Invoke();
            ReportQuestCharge();
        }

        public void SetGridEnabled(bool enabled)
        {
            gridEnabled = enabled;
            RefreshState();
            RefreshConsumers();
            EnergyChanged?.Invoke();
            ReportQuestCharge();
        }

        public void RestoreState(float savedEnergy, bool savedGridEnabled)
        {
            IsRestoringState = true;
            try
            {
                restoredFromSave = true;
                currentEnergy = Mathf.Max(0f, savedEnergy);
                gridEnabled = savedGridEnabled;

                if (TotalCapacity > 0f)
                    currentEnergy = Mathf.Min(currentEnergy, TotalCapacity);

                RefreshState();
                RefreshConsumers();
                EnergyChanged?.Invoke();
                ReportQuestCharge();
            }
            finally
            {
                IsRestoringState = false;
            }
        }

        public void ResetForNewGame()
        {
            restoredFromSave = false;
            gridEnabled = false;
            currentEnergy = 0f;

            foreach (BatteryRecord battery in batteries.Values)
                currentEnergy += battery.InitialCharge;

            currentEnergy = Mathf.Min(currentEnergy, TotalCapacity);

            RefreshState();
            RefreshConsumers();
            EnergyChanged?.Invoke();
            ReportQuestCharge();
        }

        private void ReportQuestCharge()
        {
            QuestController quests = QuestController.Instance;
            if (quests == null)
                return;

            float charge = Charge01;
            if (!float.IsNaN(lastQuestReportedCharge01) &&
                Mathf.Abs(lastQuestReportedCharge01 - charge) < 0.0001f)
            {
                return;
            }

            lastQuestReportedCharge01 = charge;
            quests.Report(
                QuestSignalType.EnergyChargeChanged,
                "station_energy",
                "Station Energy",
                value: charge);
        }

        private float CalculateGeneration()
        {
            if (TotalCapacity <= 0f)
                return 0f;

            StationEnvironmentController environment =
                StationEnvironmentController.Instance;
            if (environment == null || !environment.IsDaytime)
                return 0f;

            float baseOutput;
            switch (environment.Weather)
            {
                case StationWeather.Cloudy:
                    baseOutput = Config.CloudyDayGeneration;
                    break;
                case StationWeather.Sandstorm:
                    baseOutput = Config.SandstormGeneration;
                    break;
                default:
                    baseOutput = Config.ClearDayGeneration;
                    break;
            }

            float total = 0f;
            foreach (SolarRecord panel in solarPanels.Values)
            {
                total += baseOutput *
                         panel.OutputMultiplier;
            }
            return total;
        }

        private float CalculateConsumption()
        {
            float total = 0f;
            foreach (ConsumerRecord consumer in consumers.Values)
            {
                if (consumer.Powered)
                    total += consumer.Rate;
            }
            return total;
        }

        private void RefreshConsumers()
        {
            foreach (ConsumerRecord consumer in consumers.Values)
            {
                consumer.Powered =
                    IsConsumerConnected(consumer) &&
                    consumer.RequestedActive &&
                    HasSufficientCharge(consumer.MinimumCharge01);
            }
        }

        private bool IsConsumerConnected(ConsumerRecord consumer)
        {
            if (!HasSufficientCharge(consumer.MinimumCharge01))
                return false;

            if (!consumer.StationSystem.HasValue)
                return true;

            StationSystemsController systems =
                StationSystemsController.Instance;
            if (systems == null)
                return true;

            StationSystemType type = consumer.StationSystem.Value;
            StationSystemDefinition definition = systems.GetDefinition(
                type,
                consumer.StationObjectId);
            return systems.IsRequestedActive(
                type,
                consumer.StationObjectId,
                definition?.InitialLevel ?? 0,
                definition?.InitiallyActive ?? false);
        }

        private void HandleStationSystemsInstanceChanged(
            StationSystemsController controller)
        {
            BindStationSystems(controller);
        }

        private void BindStationSystems(StationSystemsController controller)
        {
            if (stationSystems == controller)
                return;

            if (stationSystems != null)
                stationSystems.SystemsChanged -= HandleStationSystemsChanged;
            stationSystems = controller;
            if (stationSystems != null)
                stationSystems.SystemsChanged += HandleStationSystemsChanged;

            HandleStationSystemsChanged();
        }

        private void HandleStationSystemsChanged()
        {
            RefreshConsumers();
            CurrentConsumption = CalculateConsumption();
            EnergyChanged?.Invoke();
        }

        private void RefreshState()
        {
            EnergyState newState;
            if (!gridEnabled)
                newState = EnergyState.Offline;
            else if (currentEnergy <= 0.001f || TotalCapacity <= 0f)
                newState = EnergyState.Blackout;
            else if (Charge01 <= 0.25f)
                newState = EnergyState.Emergency;
            else if (Charge01 <= 0.5f)
                newState = EnergyState.Warning;
            else
                newState = EnergyState.Normal;

            if (state == newState)
                return;

            state = newState;
            StateChanged?.Invoke(state);
        }

        private void OnDestroy()
        {
            StationSystemsController.InstanceChanged -=
                HandleStationSystemsInstanceChanged;
            if (stationSystems != null)
                stationSystems.SystemsChanged -= HandleStationSystemsChanged;
            if (Instance == this)
                Instance = null;
        }
    }

    internal static class StationEnergyDeviceId
    {
        public static string Build(Component component, string prefix)
        {
            string path = component.gameObject.scene.path;
            Transform current = component.transform;

            while (current != null)
            {
                path += $"/{current.name}[{current.GetSiblingIndex()}]";
                current = current.parent;
            }

            return $"{prefix}:{path}";
        }
    }
}
