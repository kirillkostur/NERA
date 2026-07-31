using System;
using NERA.Energy;
using NERA.Expeditions;
using NERA.Locations;
using NERA.Maintenance;
using NERA.Station;
using NERA.Terminal;
using UnityEngine;

namespace NERA.Antenna
{
    public sealed class AntennaController : MonoBehaviour
    {
        private const string AntennaConsumerId = "antenna_calibration";

        [SerializeField] private MaintainableObject maintenance;
        [SerializeField, Range(0f, 1f)] private float signalDiscoveryChance = 0.5f;

        public static AntennaController Instance { get; private set; }

        public event Action<AntennaState> StateChanged;
        public event Action<float> CalibrationProgressChanged;
        public event Action<float> ConditionChanged;
        public event Action<ExpeditionLocationData> SignalFound;
        public event Action SignalNotFound;
        public event Action<ExpeditionLocationData> ActiveSignalChanged;

        public AntennaState State { get; private set; } = AntennaState.Locked;
        public ExpeditionLocationData CalibrationTarget { get; private set; }
        public ExpeditionLocationData ActiveSignal { get; private set; }
        public MapSlotData ActiveSignalMapSlot { get; private set; }
        [Obsolete("Use ActiveSignalMapSlot. Kept for old save migration.")]
        public int ActiveSignalSectorIndex =>
            ActiveSignalMapSlot != null
                ? ActiveSignalMapSlot.LegacySectorIndex
                : -1;
        public float CalibrationProgress =>
            State == AntennaState.SignalFound
                ? 1f
                : Mathf.Clamp01(elapsedCalibrationTime / CalibrationDuration);
        public float Condition => MaintenanceSource != null
            ? MaintenanceSource.Condition
            : fallbackCondition;
        public bool IsOperational => Condition > 0.01f;
        public bool CanStartCalibration => CanCalibrate(FindNextSignalCandidate());
        public float CalibrationDuration =>
            EnergySystemController.Instance != null
                ? EnergySystemController.Instance.Config.AntennaCalibrationDuration
                : EnergyBalanceConfig.LoadDefault().AntennaCalibrationDuration;

        private float elapsedCalibrationTime;
        private float fallbackCondition = 1f;
        private StationPowerController stationPower;
        private ExpeditionDiscoveryController discovery;
        private MaintainableObject subscribedMaintenance;
        private readonly System.Collections.Generic.HashSet<string> consumedSignalIds =
            new System.Collections.Generic.HashSet<string>();

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

        private void Start()
        {
            CacheDependencies();
            EnsureEnergyRegistration();
            RefreshAvailability();
        }

        private void Update()
        {
            AdvanceCalibration(Time.deltaTime);
        }

        public bool StartCalibration()
        {
            CacheDependencies();
            return StartCalibration(FindNextSignalCandidate());
        }

        public bool StartCalibration(ExpeditionLocationData target)
        {
            CacheDependencies();

            if (!CanCalibrate(target))
                return false;

            EnergySystemController energy = EnergySystemController.Instance;
            if (energy != null)
            {
                EnsureEnergyRegistration();
                energy.SetConsumerActive(AntennaConsumerId, true);

                if (!energy.IsConsumerPowered(AntennaConsumerId))
                {
                    energy.SetConsumerActive(AntennaConsumerId, false);
                    return false;
                }
            }

            CalibrationTarget = target;
            elapsedCalibrationTime = 0f;
            SetState(AntennaState.Calibrating);
            CalibrationProgressChanged?.Invoke(0f);
            return true;
        }

        public bool CanCalibrate(ExpeditionLocationData target)
        {
            CacheDependencies();

            return State != AntennaState.Calibrating &&
                   stationPower != null &&
                   stationPower.IsPowered &&
                   discovery != null &&
                   target != null &&
                   target.DiscoverySource == DiscoverySource.Antenna &&
                   ActiveSignal == null &&
                   !IsConsumed(target) &&
                   HasAnyDiscoveredExpeditionSector() &&
                   IsSystemEnabled &&
                   IsOperational &&
                   HasCalibrationPower();
        }

        public void AdvanceCalibration(float deltaTime)
        {
            if (State != AntennaState.Calibrating || deltaTime <= 0f)
                return;

            if (!IsSystemEnabled)
            {
                EnergySystemController.Instance?.SetConsumerActive(
                    AntennaConsumerId,
                    false);
                SetState(AntennaState.Locked);
                return;
            }

            EnergySystemController energy = EnergySystemController.Instance;
            if (energy != null && !energy.IsConsumerPowered(AntennaConsumerId))
                return;

            elapsedCalibrationTime = Mathf.Min(
                elapsedCalibrationTime + deltaTime,
                CalibrationDuration
            );
            CalibrationProgressChanged?.Invoke(CalibrationProgress);

            if (elapsedCalibrationTime >= CalibrationDuration)
                CompleteCalibration();
        }

        public bool Repair()
        {
            if (MaintenanceSource == null)
            {
                if (fallbackCondition >= 0.999f)
                    return false;

                fallbackCondition = 1f;
                ConditionChanged?.Invoke(fallbackCondition);
                RefreshAvailability();
                return true;
            }

            if (!MaintenanceSource.RestoreCondition())
            {
                return false;
            }

            RefreshAvailability();
            return true;
        }

        public void RestoreCondition(float restoredCondition)
        {
            fallbackCondition = Mathf.Clamp01(restoredCondition);

            if (MaintenanceSource != null)
                MaintenanceSource.SetCondition(fallbackCondition);

            RefreshAvailability();
        }

        public void RefreshAvailability()
        {
            CacheDependencies();
            EnsureEnergyRegistration();

            if (State == AntennaState.Calibrating)
                return;

            if (!IsOperational)
            {
                SetState(AntennaState.Faulted);
                return;
            }

            if (!IsSystemEnabled)
            {
                SetState(AntennaState.Locked);
                return;
            }

            bool isPowered = stationPower != null && stationPower.IsPowered;
            SetState(isPowered ? AntennaState.Ready : AntennaState.Locked);
        }

        private bool HasCalibrationPower()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return stationPower != null && stationPower.IsPowered;

            EnsureEnergyRegistration();
            return energy.CanPowerConsumer(AntennaConsumerId);
        }

        private bool IsSystemEnabled
        {
            get
            {
                StationSystemsController systems = StationSystemsController.Instance;
                return systems == null ||
                    (systems.IsUnlocked(StationSystemType.Antenna) &&
                     systems.IsRequestedActive(StationSystemType.Antenna));
            }
        }

        private void CompleteCalibration()
        {
            ExpeditionLocationData target = CalibrationTarget;
            elapsedCalibrationTime = 0f;
            CalibrationTarget = null;
            EnergySystemController.Instance?.SetConsumerActive(
                AntennaConsumerId,
                false
            );

            if (target != null && UnityEngine.Random.value <= signalDiscoveryChance)
            {
                ActiveSignal = target;
                ActiveSignalMapSlot = PickRandomDiscoveredExpeditionSlot();
                SetState(AntennaState.SignalFound);
                SignalFound?.Invoke(target);
                ActiveSignalChanged?.Invoke(target);
                return;
            }

            SetState(AntennaState.Ready);
            SignalNotFound?.Invoke();
        }

        public bool ConsumeActiveSignal(ExpeditionLocationData signal)
        {
            if (signal == null ||
                ActiveSignal == null ||
                !string.Equals(
                    signal.LocationId,
                    ActiveSignal.LocationId,
                    StringComparison.Ordinal
                ))
            {
                return false;
            }

            consumedSignalIds.Add(signal.LocationId);
            ActiveSignal = null;
            ActiveSignalMapSlot = null;
            ActiveSignalChanged?.Invoke(null);
            RefreshAvailability();
            return true;
        }

        public void RestoreSignalState(
            string activeSignalId,
            string activeSignalMapSlotId,
            int legacySignalSectorIndex,
            System.Collections.Generic.IEnumerable<string> consumedSignalIds
        )
        {
            this.consumedSignalIds.Clear();

            if (consumedSignalIds != null)
            {
                foreach (string signalId in consumedSignalIds)
                {
                    if (!string.IsNullOrWhiteSpace(signalId))
                        this.consumedSignalIds.Add(signalId);
                }
            }

            ActiveSignal = FindSignalById(activeSignalId);
            ActiveSignalMapSlot = ActiveSignal != null
                ? FindMapSlot(
                    activeSignalMapSlotId,
                    legacySignalSectorIndex)
                : null;
            RefreshAvailability();
            ActiveSignalChanged?.Invoke(ActiveSignal);
        }

        [Obsolete("Use the overload with a stable map-slot ID.")]
        public void RestoreSignalState(
            string activeSignalId,
            int legacySignalSectorIndex,
            System.Collections.Generic.IEnumerable<string> consumedSignalIds)
        {
            RestoreSignalState(
                activeSignalId,
                string.Empty,
                legacySignalSectorIndex,
                consumedSignalIds);
        }

        public System.Collections.Generic.IEnumerable<string> ConsumedSignalIds =>
            consumedSignalIds;

        public string ActiveSignalId =>
            ActiveSignal != null ? ActiveSignal.LocationId : string.Empty;

        private ExpeditionLocationData FindNextSignalCandidate()
        {
            CacheDependencies();

            if (discovery == null || ActiveSignal != null)
                return null;

            foreach (ExpeditionLocationData location in discovery.KnownLocations)
            {
                if (location == null ||
                    location.DiscoverySource != DiscoverySource.Antenna ||
                    IsConsumed(location) ||
                    !HasAnyDiscoveredExpeditionSector())
                {
                    continue;
                }

                return location;
            }

            return null;
        }

        private ExpeditionLocationData FindSignalById(string signalId)
        {
            CacheDependencies();

            if (discovery == null || string.IsNullOrWhiteSpace(signalId))
                return null;

            return discovery.TryGetKnownLocation(
                       signalId,
                       out ExpeditionLocationData location) &&
                   location.DiscoverySource == DiscoverySource.Antenna &&
                   !IsConsumed(location)
                ? location
                : null;
        }

        private bool HasAnyDiscoveredExpeditionSector()
        {
            if (discovery == null)
                return false;

            foreach (ExpeditionLocationData location in discovery.KnownLocations)
            {
                if (location != null &&
                    location.LocationType == LocationType.Expedition &&
                    location.MapSlot != null &&
                    discovery.IsDiscovered(location))
                {
                    return true;
                }
            }

            return false;
        }

        private MapSlotData PickRandomDiscoveredExpeditionSlot()
        {
            if (discovery == null)
                return null;

            System.Collections.Generic.List<MapSlotData> slots =
                new System.Collections.Generic.List<MapSlotData>();

            foreach (ExpeditionLocationData location in discovery.KnownLocations)
            {
                if (location != null &&
                    location.LocationType == LocationType.Expedition &&
                    location.MapSlot != null &&
                    discovery.IsDiscovered(location) &&
                    !slots.Contains(location.MapSlot))
                {
                    slots.Add(location.MapSlot);
                }
            }

            return slots.Count > 0
                ? slots[UnityEngine.Random.Range(0, slots.Count)]
                : null;
        }

        private MapSlotData FindMapSlot(
            string mapSlotId,
            int legacySectorIndex)
        {
            if (discovery == null)
                return null;

            foreach (ExpeditionLocationData location in discovery.KnownLocations)
            {
                MapSlotData slot = location != null
                    ? location.MapSlot
                    : null;
                if (slot == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(mapSlotId) &&
                    string.Equals(
                        slot.SlotId,
                        mapSlotId.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return slot;
                }

                if (string.IsNullOrWhiteSpace(mapSlotId) &&
                    slot.LegacySectorIndex == legacySectorIndex)
                {
                    return slot;
                }
            }

            return null;
        }

        private bool IsConsumed(ExpeditionLocationData signal)
        {
            return signal != null && consumedSignalIds.Contains(signal.LocationId);
        }

        private void CacheDependencies()
        {
            if (stationPower == null)
                stationPower = StationPowerController.Instance;

            if (discovery == null)
                discovery = ExpeditionDiscoveryController.Instance;

            CacheMaintenanceSource();
        }

        private void CacheMaintenanceSource()
        {
            if (maintenance == null)
            {
                MaintainableObject[] candidates =
                    FindObjectsByType<MaintainableObject>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None
                    );

                foreach (MaintainableObject candidate in candidates)
                {
                    if (candidate != null &&
                        candidate.Role == MaintenanceRole.Antenna)
                    {
                        maintenance = candidate;
                        break;
                    }
                }
            }

            if (subscribedMaintenance == maintenance)
                return;

            if (subscribedMaintenance != null)
                subscribedMaintenance.ConditionChanged -= HandleConditionChanged;

            subscribedMaintenance = maintenance;

            if (subscribedMaintenance != null)
            {
                subscribedMaintenance.ConditionChanged += HandleConditionChanged;
                subscribedMaintenance.SetCondition(fallbackCondition);
            }
        }

        private MaintainableObject MaintenanceSource => maintenance;

        private void HandleConditionChanged(float _)
        {
            fallbackCondition = Condition;
            ConditionChanged?.Invoke(Condition);

            if (State == AntennaState.Calibrating && !IsOperational)
            {
                EnergySystemController.Instance?.SetConsumerActive(
                    AntennaConsumerId,
                    false
                );
                SetState(AntennaState.Faulted);
                return;
            }

            RefreshAvailability();
        }

        private void EnsureEnergyRegistration()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return;

            energy.RegisterConsumer(
                AntennaConsumerId,
                energy.Config.AntennaCalibrationConsumption,
                energy.Config.GetMinimumCharge01(
                    StationSystemType.Antenna)
            );
            energy.SetConsumerActive(
                AntennaConsumerId,
                State == AntennaState.Calibrating
            );
        }

        private void SetState(AntennaState newState)
        {
            if (State == newState)
                return;

            State = newState;
            StateChanged?.Invoke(State);
        }

        private void OnDestroy()
        {
            EnergySystemController.Instance?.SetConsumerActive(
                AntennaConsumerId,
                false
            );
            if (subscribedMaintenance != null)
                subscribedMaintenance.ConditionChanged -= HandleConditionChanged;

            if (Instance == this)
                Instance = null;
        }
    }
}
