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

        public bool IsConsumerRequestedActive(string consumerId)
        {
            return consumers.TryGetValue(consumerId, out ConsumerRecord consumer) &&
                consumer.RequestedActive;
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
