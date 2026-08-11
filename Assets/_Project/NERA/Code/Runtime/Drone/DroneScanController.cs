using System;
using NERA.Expeditions;
using NERA.Energy;
using NERA.Quests;
using NERA.Station;
using UnityEngine;

namespace NERA.Drone
{
    public sealed class DroneScanController : MonoBehaviour
    {
        private const string DroneChargerConsumerId = "drone_charger";
        private const string DroneObjectId = "station_drone";
        private const float DefaultBatteryCharge = 100f;
        private const float DefaultEnergyConsumption = 4f;
        private const float DefaultFlightEnergyConsumption = 4f;
        private const float ChargeEpsilon = 0.001f;
        [SerializeField, Min(0.1f)] private float fallbackScanDuration = 3f;
        private ExpeditionLocationData scanLocation;

        public static DroneScanController Instance { get; private set; }

        public event Action<DroneState> StateChanged;
        public event Action<float> ScanProgressChanged;
        public event Action<float> RechargeProgressChanged;
        public event Action<float> BatteryChargeChanged;
        public event Action<DroneScanResult> ScanCompleted;

        public DroneState State { get; private set; } = DroneState.Locked;
        public float ScanProgress =>
            State == DroneState.ScanComplete
                ? 1f
                : Mathf.Clamp01(elapsedScanTime / CurrentScanDuration);
        public ExpeditionLocationData ScanLocation => scanLocation;
        public float CurrentScanDuration =>
            scanLocation != null
                ? scanLocation.DroneFlightDuration
                : fallbackScanDuration;
        public float BatteryCapacity => Mathf.Max(0f,
            StationSystemsConfig.GetEffectiveStat(
                StationSystemType.Drone,
                DroneObjectId,
                StationObjectStat.BatteryCharge,
                DefaultBatteryCharge));
        public float CurrentBatteryCharge
        {
            get
            {
                EnsureBatteryInitialized();
                return Mathf.Clamp(
                    currentBatteryCharge,
                    0f,
                    BatteryCapacity);
            }
        }
        public float EnergyConsumption => Mathf.Max(0f,
            StationSystemsConfig.GetEffectiveStat(
                StationSystemType.Drone,
                DroneObjectId,
                StationObjectStat.EnergyConsumption,
                DefaultEnergyConsumption));
        public float FlightEnergyConsumption => Mathf.Max(0f,
            StationSystemsConfig.GetEffectiveStat(
                StationSystemType.Drone,
                DroneObjectId,
                StationObjectStat.FlightEnergyConsumption,
                DefaultFlightEnergyConsumption));
        public float RechargeRemaining => MissingBatteryCharge /
            Mathf.Max(EnergyConsumption, 0.01f);
        public bool IsCharging => State != DroneState.Scanning &&
            MissingBatteryCharge > ChargeEpsilon;

        private float elapsedScanTime;
        private float currentBatteryCharge;
        private bool batteryInitialized;
        private StationPowerController stationPower;
        private ExpeditionDiscoveryController discovery;

        private float MissingBatteryCharge => Mathf.Max(
            0f,
            BatteryCapacity - CurrentBatteryCharge);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            EnsureBatteryInitialized();
            EnsureEnergyRegistration();
        }

        private void OnEnable()
        {
            CacheDependencies();
            Subscribe();
        }

        private void Start()
        {
            EnsureEnergyRegistration();
            RefreshAvailability();
        }

        private void Update()
        {
            AdvanceScan(Time.deltaTime);
            AdvanceRecharge(Time.deltaTime);
        }

        public bool LaunchScan()
        {
            return LaunchScan(scanLocation);
        }

        public bool LaunchScan(ExpeditionLocationData location)
        {
            CacheDependencies();

            if (!CanLaunchScan(location))
                return false;

            scanLocation = location;
            elapsedScanTime = 0f;
            SetState(DroneState.Scanning);
            SetCurrentBatteryCharge(
                CurrentBatteryCharge - GetBatteryConsumption(location));
            EnsureEnergyRegistration();
            ScanProgressChanged?.Invoke(0f);
            Debug.Log(
                $"DroneScanController: Scan started for '{location.LocationId}'.",
                this
            );
            return true;
        }

        public bool CanLaunchScan(ExpeditionLocationData location)
        {
            CacheDependencies();

            return State != DroneState.Scanning &&
                IsSystemEnabled &&
                stationPower != null &&
                stationPower.IsPowered &&
                discovery != null &&
                location != null &&
                (StationSystemsController.Instance == null ||
                    StationSystemsController.Instance.CanDroneReach(location)) &&
                HasEnoughBatteryFor(location) &&
                location.DiscoverySource == Locations.DiscoverySource.Drone &&
                !discovery.IsDiscovered(location);
        }

        public float GetBatteryConsumption(ExpeditionLocationData location)
        {
            if (location == null)
                return 0f;

            return Mathf.Max(
                0f,
                location.DroneFlightDuration * FlightEnergyConsumption);
        }

        public bool HasEnoughBatteryFor(ExpeditionLocationData location)
        {
            return location != null &&
                CurrentBatteryCharge + ChargeEpsilon >=
                GetBatteryConsumption(location);
        }

        public void RestoreBatteryCharge(float charge)
        {
            batteryInitialized = true;
            SetCurrentBatteryCharge(charge);
            EnsureEnergyRegistration();
            RefreshAvailability();
        }

        public void ResetBatteryCharge()
        {
            batteryInitialized = true;
            SetCurrentBatteryCharge(BatteryCapacity);
            EnsureEnergyRegistration();
            RefreshAvailability();
        }

        public void AdvanceScan(float deltaTime)
        {
            if (State != DroneState.Scanning || deltaTime <= 0f)
                return;

            if (!IsSystemEnabled)
                return;

            elapsedScanTime = Mathf.Min(
                elapsedScanTime + deltaTime,
                CurrentScanDuration
            );
            ScanProgressChanged?.Invoke(ScanProgress);

            if (elapsedScanTime >= CurrentScanDuration)
                CompleteScan();
        }

        public void AdvanceRecharge(float deltaTime)
        {
            EnsureBatteryInitialized();
            ClampBatteryToCapacity();
            if (!IsCharging || deltaTime <= 0f)
                return;

            if (!IsSystemEnabled)
            {
                EnergySystemController.Instance?.SetConsumerActive(
                    DroneChargerConsumerId,
                    false);
                return;
            }

            EnergySystemController energy = EnergySystemController.Instance;
            if (energy != null)
            {
                EnsureEnergyRegistration();
                if (!energy.IsConsumerPowered(DroneChargerConsumerId))
                    return;
            }

            SetCurrentBatteryCharge(
                CurrentBatteryCharge + EnergyConsumption * deltaTime);
            RechargeProgressChanged?.Invoke(RechargeRemaining);
            if (!IsCharging)
                FinishRecharge(energy);
        }

        public void RefreshAvailability()
        {
            CacheDependencies();

            if (State == DroneState.Scanning)
                return;

            if (IsCharging)
            {
                SetState(DroneState.ScanComplete);
                return;
            }

            bool isPowered = stationPower != null && stationPower.IsPowered;
            SetState(isPowered ? DroneState.Ready : DroneState.Locked);
        }

        private void CompleteScan()
        {
            bool newlyDiscovered =
                discovery != null &&
                scanLocation != null &&
                discovery.Discover(scanLocation);

            SetState(DroneState.ScanComplete);

            EnergySystemController energy = EnergySystemController.Instance;
            EnsureEnergyRegistration();
            energy?.SetConsumerActive(DroneChargerConsumerId, IsCharging);

            ScanCompleted?.Invoke(
                new DroneScanResult(scanLocation, newlyDiscovered)
            );
            QuestController.Instance?.Report(
                QuestSignalType.DroneScanCompleted,
                scanLocation != null
                    ? scanLocation.LocationId
                    : "drone_scan",
                scanLocation != null
                    ? scanLocation.DisplayName
                    : "Drone Scan",
                value: newlyDiscovered ? 1f : 0f,
                cause: newlyDiscovered
                    ? "new_location"
                    : "known_location");
            Debug.Log("DroneScanController: Scan complete.", this);

            if (!IsCharging)
                FinishRecharge(energy);
        }

        private void CacheDependencies()
        {
            if (stationPower == null)
                stationPower = StationPowerController.Instance;

            if (discovery == null)
                discovery = ExpeditionDiscoveryController.Instance;
        }

        private void EnsureEnergyRegistration()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return;

            energy.RegisterConsumer(
                DroneChargerConsumerId,
                StationSystemsConfig.GetEffectiveStat(
                    StationSystemType.Drone,
                    DroneObjectId,
                    StationObjectStat.EnergyConsumption,
                    DefaultEnergyConsumption),
                energy.Config.GetMinimumCharge01(
                    StationSystemType.Drone),
                StationSystemType.Drone,
                DroneObjectId
            );
            energy.SetConsumerActive(
                DroneChargerConsumerId,
                IsCharging
            );
        }

        private bool IsSystemEnabled =>
            StationSystemsController.Instance == null ||
            StationSystemsController.Instance.IsRequestedActive(
                StationSystemType.Drone);

        private void EnsureBatteryInitialized()
        {
            if (batteryInitialized)
                return;

            batteryInitialized = true;
            currentBatteryCharge = BatteryCapacity;
        }

        private void ClampBatteryToCapacity()
        {
            float clamped = CurrentBatteryCharge;
            if (!Mathf.Approximately(currentBatteryCharge, clamped))
                SetCurrentBatteryCharge(clamped);
        }

        private void SetCurrentBatteryCharge(float charge)
        {
            float clamped = Mathf.Clamp(charge, 0f, BatteryCapacity);
            if (Mathf.Approximately(currentBatteryCharge, clamped))
                return;

            currentBatteryCharge = clamped;
            BatteryChargeChanged?.Invoke(CurrentBatteryCharge);
        }

        private void FinishRecharge(EnergySystemController energy)
        {
            energy?.SetConsumerActive(DroneChargerConsumerId, false);
            scanLocation = null;
            elapsedScanTime = 0f;
            bool isPowered = stationPower != null && stationPower.IsPowered;
            SetState(isPowered ? DroneState.Ready : DroneState.Locked);
        }

        private void Subscribe()
        {
            if (stationPower != null)
                stationPower.StateChanged += HandlePowerStateChanged;
        }

        private void Unsubscribe()
        {
            if (stationPower != null)
                stationPower.StateChanged -= HandlePowerStateChanged;
        }

        private void HandlePowerStateChanged(StationPowerState _)
        {
            RefreshAvailability();
        }

        private void SetState(DroneState newState)
        {
            if (State == newState)
                return;

            State = newState;
            StateChanged?.Invoke(State);
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            EnergySystemController.Instance?.SetConsumerActive(
                DroneChargerConsumerId,
                false
            );

            if (Instance == this)
                Instance = null;
        }
    }
}
