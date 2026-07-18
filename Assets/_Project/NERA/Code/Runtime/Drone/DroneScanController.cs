using System;
using NERA.Expeditions;
using NERA.Station;
using UnityEngine;

namespace NERA.Drone
{
    public sealed class DroneScanController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float fallbackScanDuration = 3f;
        [SerializeField] private ExpeditionLocationData scanLocation;

        public static DroneScanController Instance { get; private set; }

        public event Action<DroneState> StateChanged;
        public event Action<float> ScanProgressChanged;
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
        }

        private void OnEnable()
        {
            CacheDependencies();
            Subscribe();
        }

        private void Start()
        {
            RefreshAvailability();
        }

        private void Update()
        {
            AdvanceScan(Time.deltaTime);
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
                stationPower != null &&
                stationPower.IsPowered &&
                discovery != null &&
                location != null &&
                location.DiscoverySource == Locations.DiscoverySource.Drone &&
                !discovery.IsDiscovered(location);
        }

        public void AdvanceScan(float deltaTime)
        {
            if (State != DroneState.Scanning || deltaTime <= 0f)
                return;

            elapsedScanTime = Mathf.Min(
                elapsedScanTime + deltaTime,
                CurrentScanDuration
            );
            ScanProgressChanged?.Invoke(ScanProgress);

            if (elapsedScanTime >= CurrentScanDuration)
                CompleteScan();
        }

        public void RefreshAvailability()
        {
            CacheDependencies();

            if (scanLocation != null &&
                discovery != null &&
                discovery.IsDiscovered(scanLocation))
            {
                elapsedScanTime = CurrentScanDuration;
                SetState(DroneState.ScanComplete);
                return;
            }

            if (State == DroneState.Scanning)
                return;

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
            ScanCompleted?.Invoke(
                new DroneScanResult(scanLocation, newlyDiscovered)
            );
            Debug.Log("DroneScanController: Scan complete.", this);
        }

        private void CacheDependencies()
        {
            if (stationPower == null)
                stationPower = StationPowerController.Instance;

            if (discovery == null)
                discovery = ExpeditionDiscoveryController.Instance;
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

            if (Instance == this)
                Instance = null;
        }
    }
}
