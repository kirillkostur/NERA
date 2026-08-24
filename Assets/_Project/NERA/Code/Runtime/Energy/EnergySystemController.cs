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
            public float BackupReserve;
            public float PowerOutput;
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
            public int PowerPriority;
            public long ActivationSequence;
        }

        [SerializeField] private EnergyBalanceConfig config;
        [SerializeField] private bool gridEnabled;
        [SerializeField] private float currentEnergy;
        [SerializeField] private float currentBackupReserve;
        [SerializeField, Min(0.02f)] private float simulationInterval = 0.1f;

        private readonly Dictionary<string, BatteryRecord> batteries =
            new Dictionary<string, BatteryRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, SolarRecord> solarPanels =
            new Dictionary<string, SolarRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, ConsumerRecord> consumers =
            new Dictionary<string, ConsumerRecord>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<string, ConsumerRecord>>
            eligibleConsumers = new List<KeyValuePair<string, ConsumerRecord>>();
        private readonly List<KeyValuePair<string, ConsumerRecord>>
            rejectedConsumers = new List<KeyValuePair<string, ConsumerRecord>>();
        private readonly HashSet<string> disabledObjectIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> connectionIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool restoredFromSave;
        private bool hasPendingRestoredEnergy;
        private float pendingRestoredEnergy;
        private bool hasPendingRestoredBackupReserve;
        private float pendingRestoredBackupReserve;
        private bool restoreBackupReserveToFull;
        private EnergyState state = EnergyState.Offline;
        private float lastQuestReportedCharge01 = float.NaN;
        private StationSystemsController stationSystems;
        private long nextActivationSequence;
        private bool resolvingPowerOutput;
        private float simulationAccumulator;

        public static EnergySystemController Instance { get; private set; }
        public static event Action<EnergySystemController> InstanceChanged;

        public event Action EnergyChanged;
        public event Action<EnergyState> StateChanged;

        public EnergyBalanceConfig Config =>
            config != null ? config : config = EnergyBalanceConfig.LoadDefault();
        public float CurrentEnergy => currentEnergy;
        public float TotalCapacity { get; private set; }
        public float CurrentBackupReserve => currentBackupReserve;
        public float TotalBackupReserve { get; private set; }
        public float TotalPowerOutput { get; private set; }
        public float CurrentGeneration { get; private set; }
        public float CurrentConsumption { get; private set; }
        public float AvailablePowerOutput =>
            Mathf.Max(0f, TotalPowerOutput - CurrentConsumption);
        public float Charge01 =>
            TotalCapacity > 0f ? Mathf.Clamp01(currentEnergy / TotalCapacity) : 0f;
        public bool GridEnabled => gridEnabled;
        public bool IsRestoringState { get; private set; }
        public bool HasUsablePower =>
            gridEnabled && TotalCapacity > 0f &&
            (currentEnergy > 0.001f || currentBackupReserve > 0.001f);
        public EnergyState State => state;
        public int ConnectedConsumerCount
        {
            get
            {
                connectionIds.Clear();
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
                    connectionIds.Add(connectionId);
                }
                return connectionIds.Count;
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
            InstanceChanged?.Invoke(this);
            StationSystemsController.InstanceChanged +=
                HandleStationSystemsInstanceChanged;
            BindStationSystems(StationSystemsController.Instance);
            RefreshState();
        }

        private void Update()
        {
            simulationAccumulator += Time.deltaTime;
            if (simulationAccumulator < simulationInterval)
                return;

            float elapsed = simulationAccumulator;
            simulationAccumulator = 0f;
            AdvanceSimulation(elapsed);
        }

        public void AdvanceSimulation(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            ApplyPendingRestoredEnergy();

            CurrentGeneration = CalculateGeneration();
            RefreshState();
            RefreshConsumers();
            CurrentConsumption = CalculateConsumption();

            if (TotalCapacity > 0f)
            {
                ApplyEnergyBalance(deltaTime);
            }
            else if (!hasPendingRestoredEnergy)
            {
                currentEnergy = 0f;
            }

            RefreshState();
            RefreshConsumers();
            if (batteries.Count > 0)
                restoreBackupReserveToFull = false;
            EnergyChanged?.Invoke();
            ReportQuestCharge();
        }

        public bool RegisterBattery(
            string batteryId,
            float capacity,
            float initialCharge
        )
        {
            return RegisterBattery(
                batteryId,
                capacity,
                initialCharge,
                0f,
                capacity);
        }

        public bool RegisterBattery(
            string batteryId,
            float capacity,
            float initialCharge,
            float backupReserve,
            float powerOutput
        )
        {
            if (string.IsNullOrWhiteSpace(batteryId) || capacity <= 0f)
                return false;

            backupReserve = Mathf.Max(0f, backupReserve);
            powerOutput = Mathf.Max(0f, powerOutput);
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
                        clampedInitialCharge) &&
                    Mathf.Approximately(
                        existing.BackupReserve,
                        backupReserve) &&
                    Mathf.Approximately(existing.PowerOutput, powerOutput))
                {
                    return true;
                }

                TotalCapacity = Mathf.Max(
                    0f,
                    TotalCapacity - existing.Capacity + capacity);
                TotalBackupReserve = Mathf.Max(
                    0f,
                    TotalBackupReserve - existing.BackupReserve +
                    backupReserve);
                TotalPowerOutput = Mathf.Max(
                    0f,
                    TotalPowerOutput - existing.PowerOutput + powerOutput);
                existing.Capacity = capacity;
                existing.InitialCharge = clampedInitialCharge;
                existing.BackupReserve = backupReserve;
                existing.PowerOutput = powerOutput;
                float energyToPreserve = hasPendingRestoredEnergy
                    ? pendingRestoredEnergy
                    : currentEnergy;
                currentEnergy = TotalCapacity > 0f
                    ? Mathf.Min(energyToPreserve, TotalCapacity)
                    : energyToPreserve;
                float reserveToPreserve = restoreBackupReserveToFull
                    ? TotalBackupReserve
                    : hasPendingRestoredBackupReserve
                        ? pendingRestoredBackupReserve
                        : currentBackupReserve;
                currentBackupReserve = Mathf.Min(
                    reserveToPreserve,
                    TotalBackupReserve);

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
                    InitialCharge = clampedInitialCharge,
                    BackupReserve = backupReserve,
                    PowerOutput = powerOutput
                }
            );
            TotalCapacity += capacity;
            TotalBackupReserve += backupReserve;
            TotalPowerOutput += powerOutput;

            if (hasPendingRestoredEnergy)
                currentEnergy = Mathf.Min(
                    pendingRestoredEnergy,
                    TotalCapacity);
            else if (!restoredFromSave)
                currentEnergy = Mathf.Min(
                    TotalCapacity,
                    currentEnergy + clampedInitialCharge);
            else
                currentEnergy = Mathf.Min(currentEnergy, TotalCapacity);

            if (restoreBackupReserveToFull)
            {
                currentBackupReserve = TotalBackupReserve;
            }
            else if (hasPendingRestoredBackupReserve)
            {
                currentBackupReserve = Mathf.Min(
                    pendingRestoredBackupReserve,
                    TotalBackupReserve);
            }
            else if (!restoredFromSave)
            {
                currentBackupReserve = Mathf.Min(
                    TotalBackupReserve,
                    currentBackupReserve + backupReserve);
            }
            else
            {
                currentBackupReserve = Mathf.Min(
                    currentBackupReserve,
                    TotalBackupReserve);
            }

            RefreshState();
            RefreshConsumers();
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
                null,
                0);
        }

        public void RegisterConsumer(
            string consumerId,
            float rate,
            float minimumCharge01,
            int powerPriority
        )
        {
            RegisterConsumerInternal(
                consumerId,
                rate,
                minimumCharge01,
                null,
                null,
                powerPriority);
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
                stationObjectId,
                ResolvePowerPriority(stationSystem, stationObjectId));
        }

        private void RegisterConsumerInternal(
            string consumerId,
            float rate,
            float minimumCharge01,
            StationSystemType? stationSystem,
            string stationObjectId,
            int powerPriority
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
                         StringComparison.OrdinalIgnoreCase) &&
                     consumer.PowerPriority == Mathf.Max(0, powerPriority))
            {
                return;
            }

            consumer.Rate = clampedRate;
            consumer.MinimumCharge01 = clampedMinimumCharge;
            consumer.StationSystem = stationSystem;
            consumer.StationObjectId = stationObjectId?.Trim() ?? string.Empty;
            consumer.PowerPriority = Mathf.Max(0, powerPriority);
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
            if (active)
                consumer.ActivationSequence = ++nextActivationSequence;
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
            if (!consumers.TryGetValue(
                    consumerId,
                    out ConsumerRecord consumer) ||
                !IsConsumerConnected(consumer))
            {
                return false;
            }

            if (consumer.RequestedActive)
                return consumer.Powered;

            var eligible =
                new List<KeyValuePair<string, ConsumerRecord>>();
            foreach (KeyValuePair<string, ConsumerRecord> pair in consumers)
            {
                ConsumerRecord other = pair.Value;
                if (!ReferenceEquals(other, consumer) &&
                    (!other.RequestedActive ||
                     !IsConsumerConnected(other)))
                {
                    continue;
                }
                eligible.Add(pair);
            }

            eligible.Sort((left, right) =>
            {
                int priority = right.Value.PowerPriority.CompareTo(
                    left.Value.PowerPriority);
                if (priority != 0)
                    return priority;

                long leftSequence = ReferenceEquals(left.Value, consumer)
                    ? long.MaxValue
                    : left.Value.ActivationSequence;
                long rightSequence = ReferenceEquals(right.Value, consumer)
                    ? long.MaxValue
                    : right.Value.ActivationSequence;
                int activation = rightSequence.CompareTo(leftSequence);
                return activation != 0
                    ? activation
                    : string.Compare(
                        left.Key,
                        right.Key,
                        StringComparison.Ordinal);
            });

            float remainingOutput = Mathf.Max(0f, TotalPowerOutput);
            foreach (KeyValuePair<string, ConsumerRecord> pair in eligible)
            {
                bool fits = pair.Value.Rate <=
                    remainingOutput + 0.0001f;
                if (ReferenceEquals(pair.Value, consumer))
                    return fits;
                if (fits)
                {
                    remainingOutput = Mathf.Max(
                        0f,
                        remainingOutput - pair.Value.Rate);
                }
            }
            return false;
        }

        public bool HasSufficientCharge(float minimumCharge01)
        {
            return gridEnabled && TotalCapacity > 0f &&
                   currentEnergy > 0.001f &&
                   Charge01 + 0.0001f >= Mathf.Clamp01(minimumCharge01);
        }

        public bool HasSufficientCharge(
            float minimumCharge01,
            int powerPriority)
        {
            if (powerPriority < Config.BackupReserveMinimumPriority)
                return HasSufficientCharge(minimumCharge01);

            return gridEnabled && TotalCapacity > 0f &&
                (currentEnergy > 0.001f ||
                 currentBackupReserve > 0.001f);
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

        public bool CanSpendConsumerEnergy(
            string consumerId,
            float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (amount <= 0f)
                return true;

            if (!gridEnabled ||
                !consumers.TryGetValue(
                    consumerId,
                    out ConsumerRecord consumer) ||
                !consumer.Powered)
            {
                return false;
            }

            if (currentEnergy >= amount)
                return true;

            return IsBackupReserveEligible(consumer) &&
                currentEnergy + currentBackupReserve >= amount;
        }

        public bool TrySpendConsumerEnergy(
            string consumerId,
            float amount)
        {
            amount = Mathf.Max(0f, amount);
            if (!CanSpendConsumerEnergy(consumerId, amount))
                return false;

            float mainBatterySpend = Mathf.Min(currentEnergy, amount);
            currentEnergy = Mathf.Max(
                0f,
                currentEnergy - mainBatterySpend);
            currentBackupReserve = Mathf.Max(
                0f,
                currentBackupReserve - (amount - mainBatterySpend));
            RefreshState();
            RefreshConsumers();
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
            RestoreStateInternal(
                savedEnergy,
                0f,
                savedGridEnabled,
                true);
        }

        public void RestoreState(
            float savedEnergy,
            float savedBackupReserve,
            bool savedGridEnabled)
        {
            RestoreStateInternal(
                savedEnergy,
                savedBackupReserve,
                savedGridEnabled,
                false);
        }

        private void RestoreStateInternal(
            float savedEnergy,
            float savedBackupReserve,
            bool savedGridEnabled,
            bool fillBackupReserve)
        {
            IsRestoringState = true;
            try
            {
                restoredFromSave = true;
                pendingRestoredEnergy = Mathf.Max(0f, savedEnergy);
                hasPendingRestoredEnergy = TotalCapacity <= 0f;
                currentEnergy = pendingRestoredEnergy;
                gridEnabled = savedGridEnabled;

                if (TotalCapacity > 0f)
                    currentEnergy = Mathf.Min(currentEnergy, TotalCapacity);

                restoreBackupReserveToFull = fillBackupReserve;
                pendingRestoredBackupReserve = Mathf.Max(
                    0f,
                    savedBackupReserve);
                hasPendingRestoredBackupReserve =
                    !fillBackupReserve && batteries.Count == 0;
                currentBackupReserve = fillBackupReserve
                    ? TotalBackupReserve
                    : Mathf.Min(
                        pendingRestoredBackupReserve,
                        TotalBackupReserve);

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
            hasPendingRestoredEnergy = false;
            pendingRestoredEnergy = 0f;
            hasPendingRestoredBackupReserve = false;
            pendingRestoredBackupReserve = 0f;
            restoreBackupReserveToFull = false;
            gridEnabled = false;
            currentEnergy = 0f;
            currentBackupReserve = 0f;

            foreach (BatteryRecord battery in batteries.Values)
            {
                currentEnergy += battery.InitialCharge;
                currentBackupReserve += battery.BackupReserve;
            }

            currentEnergy = Mathf.Min(currentEnergy, TotalCapacity);
            currentBackupReserve = Mathf.Min(
                currentBackupReserve,
                TotalBackupReserve);

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

        private void ApplyPendingRestoredEnergy()
        {
            if (TotalCapacity > 0f && hasPendingRestoredEnergy)
            {
                currentEnergy = Mathf.Min(
                    pendingRestoredEnergy,
                    TotalCapacity);
                hasPendingRestoredEnergy = false;
            }

            if (batteries.Count > 0 && hasPendingRestoredBackupReserve)
            {
                currentBackupReserve = Mathf.Min(
                    pendingRestoredBackupReserve,
                    TotalBackupReserve);
                hasPendingRestoredBackupReserve = false;
            }
        }

        private void ApplyEnergyBalance(float deltaTime)
        {
            float netPower = CurrentGeneration - CurrentConsumption;
            if (netPower >= 0f)
            {
                StoreGeneratedEnergy(netPower * deltaTime);
                return;
            }

            float mainDrainRate = -netPower;
            float requestedMainEnergy = mainDrainRate * deltaTime;
            if (currentEnergy + 0.0001f >= requestedMainEnergy)
            {
                currentEnergy = Mathf.Max(
                    0f,
                    currentEnergy - requestedMainEnergy);
                return;
            }

            float secondsPoweredByMain = mainDrainRate > 0.0001f
                ? currentEnergy / mainDrainRate
                : 0f;
            currentEnergy = 0f;

            float reserveDuration = Mathf.Max(
                0f,
                deltaTime - secondsPoweredByMain);
            float reserveNetPower = CurrentGeneration -
                CalculateBackupReserveConsumption();
            if (reserveNetPower >= 0f)
            {
                StoreGeneratedEnergy(reserveNetPower * reserveDuration);
                return;
            }

            currentBackupReserve = Mathf.Max(
                0f,
                currentBackupReserve + reserveNetPower * reserveDuration);
        }

        private void StoreGeneratedEnergy(float generatedEnergy)
        {
            float remainingEnergy = Mathf.Max(0f, generatedEnergy);
            float mainSpace = Mathf.Max(0f, TotalCapacity - currentEnergy);
            float storedInMain = Mathf.Min(remainingEnergy, mainSpace);
            currentEnergy += storedInMain;
            remainingEnergy -= storedInMain;

            currentBackupReserve = Mathf.Min(
                TotalBackupReserve,
                currentBackupReserve + remainingEnergy);
        }

        private float CalculateBackupReserveConsumption()
        {
            float total = 0f;
            foreach (ConsumerRecord consumer in consumers.Values)
            {
                if (consumer.Powered && IsBackupReserveEligible(consumer))
                    total += consumer.Rate;
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
            if (resolvingPowerOutput)
                return;

            List<KeyValuePair<string, ConsumerRecord>> rejected =
                AllocateConsumerPower();
            StationSystemsController systems =
                stationSystems ?? StationSystemsController.Instance;
            if (systems == null || rejected.Count == 0)
            {
                return;
            }

            resolvingPowerOutput = true;
            try
            {
                disabledObjectIds.Clear();
                foreach (KeyValuePair<string, ConsumerRecord> pair in rejected)
                {
                    ConsumerRecord consumer = pair.Value;
                    if (!consumer.StationSystem.HasValue)
                        continue;

                    string objectKey =
                        $"{(int)consumer.StationSystem.Value}:" +
                        consumer.StationObjectId;
                    if (!disabledObjectIds.Add(objectKey))
                        continue;

                    systems.DisableFromPowerLimit(
                        consumer.StationSystem.Value,
                        consumer.StationObjectId);
                }
            }
            finally
            {
                resolvingPowerOutput = false;
            }

            AllocateConsumerPower();
        }

        private List<KeyValuePair<string, ConsumerRecord>>
            AllocateConsumerPower()
        {
            eligibleConsumers.Clear();
            rejectedConsumers.Clear();
            foreach (KeyValuePair<string, ConsumerRecord> pair in consumers)
            {
                ConsumerRecord consumer = pair.Value;
                consumer.Powered = false;
                if (consumer.RequestedActive &&
                    IsConsumerConnected(consumer))
                {
                    eligibleConsumers.Add(pair);
                }
            }

            eligibleConsumers.Sort((left, right) =>
            {
                int priority = right.Value.PowerPriority.CompareTo(
                    left.Value.PowerPriority);
                if (priority != 0)
                    return priority;
                int activation = right.Value.ActivationSequence.CompareTo(
                    left.Value.ActivationSequence);
                return activation != 0
                    ? activation
                    : string.Compare(
                        left.Key,
                        right.Key,
                        StringComparison.Ordinal);
            });

            float remainingOutput = Mathf.Max(0f, TotalPowerOutput);
            foreach (KeyValuePair<string, ConsumerRecord> pair in
                     eligibleConsumers)
            {
                ConsumerRecord consumer = pair.Value;
                if (consumer.Rate <= remainingOutput + 0.0001f)
                {
                    consumer.Powered = true;
                    remainingOutput = Mathf.Max(
                        0f,
                        remainingOutput - consumer.Rate);
                }
                else
                {
                    rejectedConsumers.Add(pair);
                }
            }

            CurrentConsumption = CalculateConsumption();
            return rejectedConsumers;
        }

        private int ResolvePowerPriority(
            StationSystemType stationSystem,
            string stationObjectId)
        {
            StationSystemDefinition definition =
                stationSystems?.GetDefinition(
                    stationSystem,
                    stationObjectId) ??
                StationSystemsController.Instance?.GetDefinition(
                    stationSystem,
                    stationObjectId) ??
                StationSystemsConfig.LoadDefault()?.Find(
                    stationSystem,
                    stationObjectId);
            return definition?.PowerPriority ?? 0;
        }

        private bool IsConsumerConnected(ConsumerRecord consumer)
        {
            if (!HasSufficientCharge(
                    consumer.MinimumCharge01,
                    consumer.PowerPriority))
                return false;

            if (!consumer.StationSystem.HasValue)
                return true;

            StationSystemsController systems =
                StationSystemsController.Instance;
            if (systems == null)
                return true;

            StationSystemType type = consumer.StationSystem.Value;
            return systems.IsRequestedActive(
                type,
                consumer.StationObjectId);
        }

        private bool IsBackupReserveEligible(ConsumerRecord consumer)
        {
            return consumer != null &&
                consumer.PowerPriority >= Config.BackupReserveMinimumPriority;
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
            foreach (ConsumerRecord consumer in consumers.Values)
            {
                if (consumer.StationSystem.HasValue)
                {
                    consumer.PowerPriority = ResolvePowerPriority(
                        consumer.StationSystem.Value,
                        consumer.StationObjectId);
                }
            }
            RefreshConsumers();
            EnergyChanged?.Invoke();
        }

        private void RefreshState()
        {
            EnergyState newState;
            if (!gridEnabled)
                newState = EnergyState.Offline;
            else if (TotalCapacity <= 0f)
                newState = EnergyState.Blackout;
            else if (currentEnergy <= 0.001f)
                newState = currentBackupReserve > 0.001f
                    ? EnergyState.Emergency
                    : EnergyState.Blackout;
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
            {
                Instance = null;
                InstanceChanged?.Invoke(null);
            }
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
