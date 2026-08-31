using System;
using System.Collections.Generic;
using NERA.Expeditions;
using NERA.Energy;
using NERA.Maintenance;
using NERA.Quests;
using NERA.Station;
using NERA.World;
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
        public static event Action<DroneScanController> InstanceChanged;

        public event Action<DroneState> StateChanged;
        public event Action<float> ScanProgressChanged;
        public event Action<float> RechargeProgressChanged;
        public event Action<float> BatteryChargeChanged;
        public event Action<DroneScanResult> ScanCompleted;
        public event Action<bool> StationPresenceChanged;

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
            !waitingForReturnAnimationEvent &&
            MissingBatteryCharge > ChargeEpsilon;
        public bool IsExpeditionInProgress =>
            State == DroneState.Scanning || waitingForReturnAnimationEvent;
        public bool IsAtStation =>
            !scanTimerRunning && !waitingForReturnAnimationEvent;
        public bool IsFlightReady
        {
            get
            {
                CacheDependencies();
                if (StationWeatherController.Instance?.IsSandstormActive == true)
                    return false;

                if (stationPower == null || !stationPower.IsPowered)
                    return false;

                if (stationSystems != null)
                {
                    if (!stationSystems.IsRequestedActive(
                            StationSystemType.Drone,
                            DroneObjectId) ||
                        !stationSystems.IsMaintenanceReady(
                            StationSystemType.Drone,
                            DroneObjectId))
                    {
                        return false;
                    }
                }

                EnergySystemController energy =
                    EnergySystemController.Instance;
                return energy == null ||
                    (energy.HasUsablePower &&
                     (stationSystems == null ||
                      stationSystems.HasRequiredCharge(
                          StationSystemType.Drone,
                          DroneObjectId)));
            }
        }

        private float elapsedScanTime;
        private float currentBatteryCharge;
        private bool batteryInitialized;
        private bool scanTimerRunning;
        private bool waitingForReturnAnimationEvent;
        private readonly HashSet<DroneAnimationView> animationDrivers =
            new HashSet<DroneAnimationView>();
        private StationPowerController stationPower;
        private StationSystemsController stationSystems;
        private ExpeditionDiscoveryController discovery;

        private float MissingBatteryCharge => Mathf.Max(
            0f,
            BatteryCapacity - CurrentBatteryCharge);

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            InstanceChanged = null;
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
            scanTimerRunning = false;
            waitingForReturnAnimationEvent = false;
            SetState(DroneState.Scanning);
            ScanProgressChanged?.Invoke(0f);
            Debug.Log(
                $"DroneScanController: Launch started for '{location.LocationId}'.",
                this
            );

            if (animationDrivers.Count == 0)
                NotifyLaunchAnimationEvent();
            return true;
        }

        public bool CanLaunchScan(ExpeditionLocationData location)
        {
            CacheDependencies();

            return State == DroneState.Ready &&
                IsFlightReady &&
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
            if (State != DroneState.Scanning ||
                !scanTimerRunning ||
                deltaTime <= 0f)
                return;

            if (!IsSystemEnabled)
                return;

            elapsedScanTime = Mathf.Min(
                elapsedScanTime + deltaTime,
                CurrentScanDuration
            );
            ScanProgressChanged?.Invoke(ScanProgress);

            if (elapsedScanTime >= CurrentScanDuration)
                BeginReturn();
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

            if (State == DroneState.Scanning ||
                waitingForReturnAnimationEvent)
                return;

            if (IsCharging)
            {
                SetState(DroneState.ScanComplete);
                return;
            }

            SetState(IsFlightReady ? DroneState.Ready : DroneState.Locked);
        }

        internal void RegisterAnimationDriver(DroneAnimationView driver)
        {
            if (driver != null)
                animationDrivers.Add(driver);
        }

        internal void UnregisterAnimationDriver(DroneAnimationView driver)
        {
            if (driver == null || !animationDrivers.Remove(driver) ||
                animationDrivers.Count != 0)
            {
                return;
            }

            if (State == DroneState.Scanning && !scanTimerRunning)
                NotifyLaunchAnimationEvent();
            else if (waitingForReturnAnimationEvent)
                NotifyReturnAnimationEvent();
        }

        internal void NotifyLaunchAnimationEvent()
        {
            if (State != DroneState.Scanning ||
                scanTimerRunning ||
                scanLocation == null)
            {
                return;
            }

            scanTimerRunning = true;
            StationPresenceChanged?.Invoke(false);
            SetCurrentBatteryCharge(
                CurrentBatteryCharge - GetBatteryConsumption(scanLocation));
            EnsureEnergyRegistration();
            ScanProgressChanged?.Invoke(0f);
            Debug.Log(
                $"DroneScanController: Scan started for " +
                $"'{scanLocation.LocationId}'.",
                this);
        }

        internal void NotifyReturnAnimationEvent()
        {
            if (State != DroneState.ScanComplete ||
                !waitingForReturnAnimationEvent)
            {
                return;
            }

            waitingForReturnAnimationEvent = false;
            StationPresenceChanged?.Invoke(true);
            CompleteScan();
        }

        private void BeginReturn()
        {
            scanTimerRunning = false;
            waitingForReturnAnimationEvent = true;
            SetState(DroneState.ScanComplete);
            Debug.Log("DroneScanController: Return started.", this);

            if (animationDrivers.Count == 0)
                NotifyReturnAnimationEvent();
        }

        private void CompleteScan()
        {
            bool newlyDiscovered =
                discovery != null &&
                scanLocation != null &&
                discovery.Discover(scanLocation);

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

            BindStationSystems(StationSystemsController.Instance);
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
            stationSystems == null ||
            stationSystems.IsRequestedActive(
                StationSystemType.Drone,
                DroneObjectId);

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
            scanTimerRunning = false;
            waitingForReturnAnimationEvent = false;
            SetState(IsFlightReady ? DroneState.Ready : DroneState.Locked);
        }

        private void Subscribe()
        {
            if (stationPower != null)
                stationPower.StateChanged += HandlePowerStateChanged;
            StationSystemsController.InstanceChanged +=
                HandleStationSystemsInstanceChanged;
            MaintainableObject.AnyConditionChanged +=
                HandleMaintenanceConditionChanged;
            StationWeatherController.AnySandstormStarted +=
                HandleSandstormStarted;
            StationWeatherController.AnySandstormEnded +=
                HandleSandstormEnded;
            BindStationSystems(StationSystemsController.Instance);
        }

        private void Unsubscribe()
        {
            if (stationPower != null)
                stationPower.StateChanged -= HandlePowerStateChanged;
            StationSystemsController.InstanceChanged -=
                HandleStationSystemsInstanceChanged;
            MaintainableObject.AnyConditionChanged -=
                HandleMaintenanceConditionChanged;
            StationWeatherController.AnySandstormStarted -=
                HandleSandstormStarted;
            StationWeatherController.AnySandstormEnded -=
                HandleSandstormEnded;
            BindStationSystems(null);
        }

        private void BindStationSystems(StationSystemsController systems)
        {
            if (stationSystems == systems)
                return;

            if (stationSystems != null)
                stationSystems.SystemsChanged -= HandleSystemsChanged;
            stationSystems = systems;
            if (stationSystems != null)
                stationSystems.SystemsChanged += HandleSystemsChanged;
        }

        private void HandlePowerStateChanged(StationPowerState _)
        {
            RefreshAvailability();
        }

        private void HandleStationSystemsInstanceChanged(
            StationSystemsController systems)
        {
            BindStationSystems(systems);
            RefreshAvailability();
        }

        private void HandleSystemsChanged()
        {
            RefreshAvailability();
        }

        private void HandleMaintenanceConditionChanged(
            string objectId,
            float _)
        {
            if (string.Equals(
                    objectId,
                    DroneObjectId,
                    StringComparison.OrdinalIgnoreCase))
            {
                RefreshAvailability();
            }
        }

        private void HandleSandstormStarted(float _)
        {
            RefreshAvailability();
        }

        private void HandleSandstormEnded(bool _)
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
            {
                Instance = null;
                InstanceChanged?.Invoke(null);
            }
        }
    }
}
