using System;
using System.Collections.Generic;
using NERA.Development;
using NERA.Energy;
using NERA.Expeditions;
using NERA.Items;
using NERA.Locations;
using NERA.Maintenance;
using NERA.Quests;
using NERA.Save;
using NERA.Station;
using NERA.Terminal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NERA.Antenna
{
    public sealed class AntennaController : MonoBehaviour,
        IDeveloperProgressSkippable
    {
        private const string AntennaConsumerId = "antenna_calibration";
        private const string AntennaObjectId = "station_antenna";

        [SerializeField] private MaintainableObject maintenance;
        [SerializeField, Range(0f, 1f)] private float signalDiscoveryChance = 0.5f;

        public static AntennaController Instance { get; private set; }

        public event Action<AntennaState> StateChanged;
        public event Action<float> CalibrationProgressChanged;
        public event Action<float> ConditionChanged;
        public event Action<ExpeditionLocationData> SignalFound;
        public event Action SignalNotFound;
        public event Action<ExpeditionLocationData> ActiveSignalChanged;
        public event Action ActiveSignalLifecycleChanged;

        public AntennaState State { get; private set; } = AntennaState.Locked;
        public ExpeditionLocationData CalibrationTarget { get; private set; }
        public ExpeditionLocationData ActiveSignal { get; private set; }
        public MapSlotData ActiveSignalMapSlot { get; private set; }
        public bool ActiveSignalExpiryStarted =>
            activeSignalExpiryStarted;
        public long ActiveSignalExpiryUtcTicks =>
            activeSignalExpiryStarted
                ? activeSignalExpiryUtcTicks
                : 0L;
        public float ActiveSignalExpiryRemaining
        {
            get
            {
                if (!activeSignalExpiryStarted ||
                    activeSignalExpiryUtcTicks <= 0L)
                {
                    return 0f;
                }

                double seconds =
                    (activeSignalExpiryUtcTicks - DateTime.UtcNow.Ticks) /
                    (double)TimeSpan.TicksPerSecond;
                return Mathf.Max(0f, (float)seconds);
            }
        }
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
        public float CalibrationDuration
        {
            get
            {
                return StationSystemsConfig.GetEffectiveStat(
                    StationSystemType.Antenna,
                    "station_antenna",
                    StationObjectStat.CalibrationDuration,
                    8f);
            }
        }

        private float elapsedCalibrationTime;
        private float fallbackCondition = 1f;
        private StationPowerController stationPower;
        private ExpeditionDiscoveryController discovery;
        private MaintainableObject subscribedMaintenance;
        private readonly HashSet<string> consumedSignalIds =
            new HashSet<string>();
        private readonly HashSet<string> activeSignalWorldItemKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private WorldStateController subscribedWorldState;
        private bool activeSignalExpiryStarted;
        private long activeSignalExpiryUtcTicks;

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
            MaintainableObject.Registered += HandleMaintenanceRegistered;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            CacheMaintenanceSource();
            BindWorldState();
        }

        private void OnDisable()
        {
            MaintainableObject.Registered -= HandleMaintenanceRegistered;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnbindWorldState();
        }

        private void Start()
        {
            CacheDependencies();
            EnsureEnergyRegistration();
            RefreshAvailability();
            TrackActiveSignalSceneIfLoaded();
        }

        private void Update()
        {
            AdvanceCalibration(Time.deltaTime);
            UpdateActiveSignalExpiry();

            if (subscribedWorldState != WorldStateController.Instance)
                BindWorldState();
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
                   HasRequiredAntennaRange(target) &&
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

        public bool CompleteActiveProgressForDebug()
        {
            if (State == AntennaState.Calibrating)
            {
                elapsedCalibrationTime = CalibrationDuration;
                CalibrationProgressChanged?.Invoke(1f);
                CompleteCalibration();
                return true;
            }

            if (!activeSignalExpiryStarted || ActiveSignal == null)
                return false;

            activeSignalExpiryUtcTicks = DateTime.UtcNow.Ticks;
            UpdateActiveSignalExpiry();
            return true;
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
                    systems.IsRequestedActive(StationSystemType.Antenna);
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
                ResetActiveSignalLifecycle();
                SetState(AntennaState.SignalFound);
                SignalFound?.Invoke(target);
                QuestController.Instance?.Report(
                    QuestSignalType.AntennaSignalFound,
                    target.LocationId,
                    target.DisplayName);
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
            ResetActiveSignalLifecycle();
            ActiveSignalChanged?.Invoke(null);
            ActiveSignalLifecycleChanged?.Invoke();
            RefreshAvailability();
            return true;
        }

        /// <summary>
        /// Developer-tool entry point. Reveals a configured antenna location
        /// without range, calibration, power, condition, or consumed-state
        /// checks. A discovered expedition map slot is still required so the
        /// terminal has a valid anchor for the signal marker.
        /// </summary>
        public bool ForceRevealSignalForDebug(ExpeditionLocationData signal)
        {
            CacheDependencies();
            if (discovery == null ||
                signal == null ||
                signal.DiscoverySource != DiscoverySource.Antenna)
            {
                return false;
            }

            MapSlotData mapSlot = PickRandomDiscoveredExpeditionSlot();
            if (mapSlot == null)
                return false;

            EnergySystemController.Instance?.SetConsumerActive(
                AntennaConsumerId,
                false);
            elapsedCalibrationTime = 0f;
            CalibrationTarget = null;
            consumedSignalIds.Remove(signal.LocationId);
            ActiveSignal = signal;
            ActiveSignalMapSlot = mapSlot;
            ResetActiveSignalLifecycle();
            SetState(AntennaState.SignalFound);
            SignalFound?.Invoke(signal);
            QuestController.Instance?.Report(
                QuestSignalType.AntennaSignalFound,
                signal.LocationId,
                signal.DisplayName,
                cause: "developer_cheat");
            ActiveSignalChanged?.Invoke(signal);
            return true;
        }

        public void RestoreSignalState(
            string activeSignalId,
            string activeSignalMapSlotId,
            int legacySignalSectorIndex,
            System.Collections.Generic.IEnumerable<string> consumedSignalIds
        )
        {
            RestoreSignalState(
                activeSignalId,
                activeSignalMapSlotId,
                legacySignalSectorIndex,
                consumedSignalIds,
                false,
                0L);
        }

        public void RestoreSignalState(
            string activeSignalId,
            string activeSignalMapSlotId,
            int legacySignalSectorIndex,
            System.Collections.Generic.IEnumerable<string> consumedSignalIds,
            bool expiryStarted,
            long expiryUtcTicks
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
            if (ActiveSignal != null && ActiveSignalMapSlot == null)
                ActiveSignalMapSlot = PickRandomDiscoveredExpeditionSlot();
            activeSignalWorldItemKeys.Clear();
            activeSignalExpiryStarted =
                ActiveSignal != null && expiryStarted;
            activeSignalExpiryUtcTicks =
                activeSignalExpiryStarted ? expiryUtcTicks : 0L;

            if (activeSignalExpiryStarted &&
                (activeSignalExpiryUtcTicks <= 0L ||
                 ActiveSignalExpiryRemaining <= 0f))
            {
                ConsumeActiveSignal(ActiveSignal);
                return;
            }

            if (ActiveSignal != null)
            {
                SetState(AntennaState.SignalFound);
                TrackActiveSignalSceneIfLoaded();
            }
            else
            {
                RefreshAvailability();
            }

            ActiveSignalChanged?.Invoke(ActiveSignal);
            ActiveSignalLifecycleChanged?.Invoke();
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
                    !HasRequiredAntennaRange(location) ||
                    IsConsumed(location) ||
                    !HasAnyDiscoveredExpeditionSector())
                {
                    continue;
                }

                return location;
            }

            return null;
        }

        private static bool HasRequiredAntennaRange(
            ExpeditionLocationData location)
        {
            StationSystemsController systems = StationSystemsController.Instance;
            return systems == null || systems.CanAntennaReach(location);
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

        private void HandleSceneLoaded(
            Scene scene,
            LoadSceneMode _)
        {
            TrackActiveSignalScene(scene);
        }

        private void TrackActiveSignalSceneIfLoaded()
        {
            if (ActiveSignal == null ||
                activeSignalExpiryStarted ||
                string.IsNullOrWhiteSpace(ActiveSignal.SceneName))
            {
                return;
            }

            Scene scene = SceneManager.GetSceneByName(ActiveSignal.SceneName);
            if (scene.IsValid() && scene.isLoaded)
                TrackActiveSignalScene(scene);
        }

        private void TrackActiveSignalScene(Scene scene)
        {
            if (ActiveSignal == null ||
                !ActiveSignal.UsesPostCollectionLifetime ||
                activeSignalExpiryStarted ||
                !scene.IsValid() ||
                !scene.isLoaded ||
                !string.Equals(
                    scene.name,
                    ActiveSignal.SceneName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            activeSignalWorldItemKeys.Clear();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (WorldItem worldItem in
                         root.GetComponentsInChildren<WorldItem>(true))
                {
                    if (worldItem == null || !worldItem.TracksWorldState)
                        continue;

                    string key = PersistentSceneIdentity.Normalize(
                        worldItem.PersistentKey);
                    if (!string.IsNullOrEmpty(key))
                        activeSignalWorldItemKeys.Add(key);
                }
            }

            if (activeSignalWorldItemKeys.Count == 0)
            {
                Debug.LogWarning(
                    $"Antenna: Unknown Signal '{ActiveSignal.LocationId}' " +
                    "has no persistent WorldItem objects, so it will remain " +
                    "available instead of closing automatically.",
                    this);
                return;
            }

            EvaluateActiveSignalCollection();
        }

        private void BindWorldState()
        {
            WorldStateController current = WorldStateController.Instance;
            if (subscribedWorldState == current)
                return;

            UnbindWorldState();
            subscribedWorldState = current;
            if (subscribedWorldState != null)
            {
                subscribedWorldState.StateChanged +=
                    HandleWorldStateChanged;
                subscribedWorldState.StateRestored +=
                    HandleWorldStateChanged;
            }

            EvaluateActiveSignalCollection();
        }

        private void UnbindWorldState()
        {
            if (subscribedWorldState == null)
                return;

            subscribedWorldState.StateChanged -= HandleWorldStateChanged;
            subscribedWorldState.StateRestored -= HandleWorldStateChanged;
            subscribedWorldState = null;
        }

        private void HandleWorldStateChanged()
        {
            EvaluateActiveSignalCollection();
        }

        private void EvaluateActiveSignalCollection()
        {
            if (ActiveSignal == null ||
                !ActiveSignal.UsesPostCollectionLifetime ||
                activeSignalExpiryStarted ||
                subscribedWorldState == null ||
                activeSignalWorldItemKeys.Count == 0)
            {
                return;
            }

            foreach (string persistentKey in activeSignalWorldItemKeys)
            {
                if (!subscribedWorldState.IsConsumed(persistentKey))
                    return;
            }

            StartActiveSignalExpiry();
        }

        private void StartActiveSignalExpiry()
        {
            if (ActiveSignal == null ||
                !ActiveSignal.UsesPostCollectionLifetime ||
                activeSignalExpiryStarted)
            {
                return;
            }

            activeSignalExpiryStarted = true;
            long lifetimeTicks = (long)Math.Ceiling(
                ActiveSignal.PostCollectionLifetime *
                TimeSpan.TicksPerSecond);
            activeSignalExpiryUtcTicks =
                DateTime.UtcNow.Ticks +
                Math.Max(TimeSpan.TicksPerSecond, lifetimeTicks);
            ActiveSignalLifecycleChanged?.Invoke();
            Debug.Log(
                $"Antenna: All items from '{ActiveSignal.LocationId}' were " +
                $"collected. Location closes in " +
                $"{ActiveSignal.PostCollectionLifetime:0.#} seconds.",
                this);
        }

        private void UpdateActiveSignalExpiry()
        {
            if (!activeSignalExpiryStarted ||
                ActiveSignal == null ||
                ActiveSignalExpiryRemaining > 0f)
            {
                return;
            }

            ExpeditionLocationData expiredSignal = ActiveSignal;
            Debug.Log(
                $"Antenna: Unknown Signal '{expiredSignal.LocationId}' " +
                "expired and was removed from the terminal map.",
                this);
            ConsumeActiveSignal(expiredSignal);
        }

        private void ResetActiveSignalLifecycle()
        {
            activeSignalWorldItemKeys.Clear();
            activeSignalExpiryStarted = false;
            activeSignalExpiryUtcTicks = 0L;
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
            if (maintenance == null ||
                !maintenance.isActiveAndEnabled ||
                !string.Equals(
                    maintenance.ObjectId,
                    AntennaObjectId,
                    StringComparison.OrdinalIgnoreCase))
            {
                MaintainableObject local =
                    GetComponentInChildren<MaintainableObject>(true);
                if (local != null &&
                    string.Equals(
                        local.ObjectId,
                        AntennaObjectId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    maintenance = local;
                }
                else
                {
                    maintenance = MaintainableObject.TryFind(
                        AntennaObjectId,
                        out MaintainableObject identified)
                        ? identified
                        : null;
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
                fallbackCondition = subscribedMaintenance.Condition;
            }
        }

        private void HandleMaintenanceRegistered(
            MaintainableObject registered)
        {
            if (registered == null ||
                !string.Equals(
                    registered.ObjectId,
                    AntennaObjectId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            maintenance = registered;
            CacheMaintenanceSource();
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
                StationSystemsConfig.GetEffectiveStat(
                    StationSystemType.Antenna,
                    "station_antenna",
                    StationObjectStat.CalibrationEnergyConsumption,
                    3f),
                energy.Config.GetMinimumCharge01(
                    StationSystemType.Antenna),
                StationSystemType.Antenna,
                "station_antenna"
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
            MaintainableObject.Registered -= HandleMaintenanceRegistered;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnbindWorldState();
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
