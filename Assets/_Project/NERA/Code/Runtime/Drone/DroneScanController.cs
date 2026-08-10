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
        [SerializeField, Min(0.1f)] private float fallbackScanDuration = 3f;
        private ExpeditionLocationData scanLocation;

        public static DroneScanController Instance { get; private set; }

        public event Action<DroneState> StateChanged;
        public event Action<float> ScanProgressChanged;
        public event Action<float> RechargeProgressChanged;
        public event Action<DroneScanResult> ScanCompleted;

        public DroneState State { get; private set; } = DroneState.Locked;
        public float ScanProgress =>
            State == DroneState.ScanComplete
                ? 1f
                : Mathf.Clamp01(elapsedScanTime / CurrentScanDuration);
        public ExpeditionLocationData ScanLocation => scanLocation;
        public float CurrentScanDuration =>
            scanLocation != null
                ? scanLocation.DroneScanDuration
                : fallbackScanDuration;
        public float RechargeRemaining { get; private set; }
        public bool IsCharging => RechargeRemaining > 0f;

        private float elapsedScanTime;
        private StationPowerController stationPower;
        private ExpeditionDiscoveryController discovery;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

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
                !IsCharging &&
                IsSystemEnabled &&
                stationPower != null &&
                stationPower.IsPowered &&
                discovery != null &&
                location != null &&
                (StationSystemsController.Instance == null ||
                    StationSystemsController.Instance.CanDroneReach(location)) &&
                location.DiscoverySource == Locations.DiscoverySource.Drone &&
                !discovery.IsDiscovered(location);
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

            RechargeRemaining = Mathf.Max(0f, RechargeRemaining - deltaTime);
            RechargeProgressChanged?.Invoke(RechargeRemaining);
            if (RechargeRemaining <= 0f)
            {
                energy?.SetConsumerActive(DroneChargerConsumerId, false);
                scanLocation = null;
                elapsedScanTime = 0f;
                bool isPowered =
                    stationPower != null && stationPower.IsPowered;
                SetState(isPowered ? DroneState.Ready : DroneState.Locked);
            }
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
            if (energy != null)
            {
                EnsureEnergyRegistration();
                RechargeRemaining = energy.Config.DroneRechargeDuration;
                energy.SetConsumerActive(DroneChargerConsumerId, true);
            }

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
                energy.Config.DroneChargingConsumption,
                energy.Config.GetMinimumCharge01(
                    StationSystemType.Drone),
                StationSystemType.Drone,
                "station_drone"
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
