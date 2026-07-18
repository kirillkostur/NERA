using System;
using System.Collections.Generic;
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
            public float Contamination;
        }

        private sealed class ConsumerRecord
        {
            public float Rate;
            public bool DisableInEmergency;
            public bool RequestedActive;
            public bool Powered;
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
        public bool HasUsablePower =>
            gridEnabled && currentEnergy > 0.001f && TotalCapacity > 0f;
        public EnergyState State => state;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
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

            UpdateSolarContamination(deltaTime);
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
        }

        public bool RegisterBattery(
            string batteryId,
            float capacity,
            float initialCharge
        )
        {
            if (string.IsNullOrWhiteSpace(batteryId) || capacity <= 0f)
                return false;

            if (batteries.ContainsKey(batteryId))
                return true;

            batteries.Add(
                batteryId,
                new BatteryRecord
                {
                    Capacity = capacity,
                    InitialCharge = Mathf.Clamp(initialCharge, 0f, capacity)
                }
            );
            TotalCapacity += capacity;

            if (!restoredFromSave)
                currentEnergy = Mathf.Min(TotalCapacity, currentEnergy + initialCharge);
            else
                currentEnergy = Mathf.Min(currentEnergy, TotalCapacity);

            RefreshState();
            EnergyChanged?.Invoke();
            return true;
        }

        public bool RegisterSolarPanel(
            string panelId,
            float outputMultiplier,
            float initialContamination
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
                    OutputMultiplier = Mathf.Max(0f, outputMultiplier),
                    Contamination = Mathf.Clamp01(initialContamination)
                }
            );
            return true;
        }

        public float GetSolarContamination(string panelId)
        {
            return solarPanels.TryGetValue(panelId, out SolarRecord panel)
                ? panel.Contamination
                : 0f;
        }

        public bool CleanSolarPanel(string panelId)
        {
            if (!solarPanels.TryGetValue(panelId, out SolarRecord panel) ||
                panel.Contamination <= 0f)
            {
                return false;
            }

            panel.Contamination = 0f;
            EnergyChanged?.Invoke();
            return true;
        }

        public void RegisterConsumer(
            string consumerId,
            float rate,
            bool disableInEmergency
        )
        {
            if (string.IsNullOrWhiteSpace(consumerId))
                return;

            if (!consumers.TryGetValue(consumerId, out ConsumerRecord consumer))
            {
                consumer = new ConsumerRecord();
                consumers.Add(consumerId, consumer);
            }

            consumer.Rate = Mathf.Max(0f, rate);
            consumer.DisableInEmergency = disableInEmergency;
            RefreshConsumers();
        }

        public void SetConsumerActive(string consumerId, bool active)
        {
            if (!consumers.TryGetValue(consumerId, out ConsumerRecord consumer))
                return;

            consumer.RequestedActive = active;
            RefreshConsumers();
            EnergyChanged?.Invoke();
        }

        public bool IsConsumerPowered(string consumerId)
        {
            return consumers.TryGetValue(consumerId, out ConsumerRecord consumer) &&
                   consumer.Powered;
        }

        public bool CanPowerConsumer(string consumerId)
        {
            return consumers.TryGetValue(consumerId, out ConsumerRecord consumer) &&
                   HasUsablePower &&
                   !(state == EnergyState.Emergency &&
                     consumer.DisableInEmergency);
        }

        public void SetGridEnabled(bool enabled)
        {
            gridEnabled = enabled;
            RefreshState();
            RefreshConsumers();
            EnergyChanged?.Invoke();
        }

        public void RestoreState(float savedEnergy, bool savedGridEnabled)
        {
            restoredFromSave = true;
            currentEnergy = Mathf.Max(0f, savedEnergy);
            gridEnabled = savedGridEnabled;

            if (TotalCapacity > 0f)
                currentEnergy = Mathf.Min(currentEnergy, TotalCapacity);

            RefreshState();
            RefreshConsumers();
            EnergyChanged?.Invoke();
        }

        public void ResetForNewGame()
        {
            restoredFromSave = false;
            gridEnabled = false;
            currentEnergy = 0f;

            foreach (BatteryRecord battery in batteries.Values)
                currentEnergy += battery.InitialCharge;

            currentEnergy = Mathf.Min(currentEnergy, TotalCapacity);

            foreach (SolarRecord panel in solarPanels.Values)
                panel.Contamination = 0f;

            RefreshState();
            RefreshConsumers();
            EnergyChanged?.Invoke();
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
                         panel.OutputMultiplier *
                         (1f - panel.Contamination);
            }
            return total;
        }

        private void UpdateSolarContamination(float deltaTime)
        {
            StationEnvironmentController environment =
                StationEnvironmentController.Instance;
            if (environment == null ||
                environment.Weather != StationWeather.Sandstorm)
            {
                return;
            }

            foreach (SolarRecord panel in solarPanels.Values)
            {
                panel.Contamination = Mathf.Clamp01(
                    panel.Contamination +
                    Config.SandstormContaminationPerSecond * deltaTime
                );
            }
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
                    consumer.RequestedActive &&
                    HasUsablePower &&
                    !(state == EnergyState.Emergency && consumer.DisableInEmergency);
            }
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
            if (Instance == this)
                Instance = null;
        }
    }
}
