using System;
using NERA.Antenna;
using NERA.Drone;
using NERA.Energy;
using NERA.Expeditions;
using NERA.Inventory;
using NERA.Library;
using NERA.Maintenance;
using NERA.Quests;
using NERA.Research;
using NERA.Station;
using UnityEngine;

namespace NERA.Save
{
    /// <summary>
    /// Debounces dirty-state events and writes one current-state save on the
    /// main thread. This coordinator does not perform background file I/O.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AutoSaveService : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float saveDelay = 2f;
        [SerializeField, Min(0.5f)] private float maximumDirtyAge = 10f;

        private SaveGameController saveController;
        private ExpeditionDiscoveryController discovery;
        private StationPowerController stationPower;
        private EnergySystemController energySystem;
        private DroneScanController drone;
        private LaboratoryWorkstationController laboratoryWorkstation;
        private AntennaController antenna;
        private PlayerInventory inventory;
        private ResearchController research;
        private LibraryController library;
        private StationStorageController stationStorage;
        private StationSystemsController stationSystems;
        private QuestController quests;
        private WorldStateController worldState;
        private bool initialized;
        private bool subscribed;
        private bool suspended;
        private bool dirty;
        private float dirtySince;
        private float saveAt;
        private bool hasObservedEnergyState;
        private float observedStationEnergy;
        private float observedStationBackupReserve;
        private bool observedEnergyGridEnabled;

        public static AutoSaveService Instance { get; private set; }
        public bool IsDirty => dirty;
        public bool IsSuspended => suspended;
        public bool IsSaving => saveController != null &&
            saveController.IsBusy;

        public event Action<bool> BackgroundSavingStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            saveController = GetComponent<SaveGameController>();
        }

        public void InitializeSession()
        {
            if (initialized)
                return;

            initialized = true;
            CacheSystems();
            Subscribe();
        }

        public void MarkDirty()
        {
            if (!initialized || suspended)
                return;

            float now = Time.unscaledTime;
            if (!dirty)
            {
                dirty = true;
                dirtySince = now;
            }

            saveAt = Mathf.Min(
                now + saveDelay,
                dirtySince + maximumDirtyAge);
        }

        public bool Flush()
        {
            if (!initialized || suspended || saveController == null)
                return false;
            if (!dirty)
                return true;

            return SaveDirtyState();
        }

        public void CancelPending()
        {
            dirty = false;
            dirtySince = 0f;
            saveAt = 0f;
        }

        public void SetSuspended(bool value)
        {
            suspended = value;
        }

        private void Update()
        {
            if (!dirty || suspended || Time.unscaledTime < saveAt)
                return;

            SaveDirtyState();
        }

        private bool SaveDirtyState()
        {
            if (saveController == null || saveController.IsBusy)
                return false;

            BackgroundSavingStateChanged?.Invoke(true);
            bool saved = saveController.Save();
            if (saved)
                CancelPending();
            else
                saveAt = Time.unscaledTime + saveDelay;
            BackgroundSavingStateChanged?.Invoke(false);
            return saved;
        }

        private void CacheSystems()
        {
            discovery ??= GetComponent<ExpeditionDiscoveryController>();
            stationPower ??= GetComponent<StationPowerController>();
            energySystem ??= GetComponent<EnergySystemController>();
            drone ??= GetComponent<DroneScanController>() ??
                DroneScanController.Instance;
            laboratoryWorkstation ??=
                GetComponent<LaboratoryWorkstationController>();
            antenna ??= GetComponent<AntennaController>();
            inventory ??= GetComponentInChildren<PlayerInventory>(true);
            research ??= GetComponent<ResearchController>();
            library ??= GetComponent<LibraryController>();
            stationStorage ??= GetComponent<StationStorageController>();
            stationSystems ??= GetComponent<StationSystemsController>();
            quests ??= GetComponent<QuestController>();
            worldState ??= GetComponent<WorldStateController>();
        }

        private void Subscribe()
        {
            if (subscribed)
                return;

            subscribed = true;
            if (discovery != null)
                discovery.LocationDiscovered += HandleStringChanged;
            if (stationPower != null)
                stationPower.StateChanged += HandlePowerChanged;
            if (energySystem != null)
            {
                ObserveEnergyState();
                energySystem.EnergyChanged += HandleEnergyChanged;
            }
            if (drone != null)
            {
                drone.StateChanged += HandleDroneStateChanged;
                drone.BatteryChargeChanged += HandleFloatChanged;
            }
            if (antenna != null)
            {
                antenna.ConditionChanged += HandleFloatChanged;
                antenna.ActiveSignalChanged += HandleSignalChanged;
            }
            if (inventory != null)
                inventory.InventoryChanged += MarkDirty;
            if (research != null)
            {
                research.ResearchAnalyzed += HandleStringChanged;
                research.StateChanged += HandleResearchChanged;
            }
            if (laboratoryWorkstation != null)
                laboratoryWorkstation.ItemsChanged += MarkDirty;
            if (library != null)
                library.EntryUnlocked += HandleStringChanged;
            if (stationStorage != null)
                stationStorage.StorageChanged += MarkDirty;
            if (stationSystems != null)
                stationSystems.SystemsChanged += MarkDirty;
            if (quests != null)
                quests.QuestsChanged += MarkDirty;
            if (worldState != null)
                worldState.StateChanged += MarkDirty;
            MaintainableObject.AnyConditionChanged +=
                HandleMaintenanceChanged;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;

            subscribed = false;
            if (discovery != null)
                discovery.LocationDiscovered -= HandleStringChanged;
            if (stationPower != null)
                stationPower.StateChanged -= HandlePowerChanged;
            if (energySystem != null)
                energySystem.EnergyChanged -= HandleEnergyChanged;
            if (drone != null)
            {
                drone.StateChanged -= HandleDroneStateChanged;
                drone.BatteryChargeChanged -= HandleFloatChanged;
            }
            if (antenna != null)
            {
                antenna.ConditionChanged -= HandleFloatChanged;
                antenna.ActiveSignalChanged -= HandleSignalChanged;
            }
            if (inventory != null)
                inventory.InventoryChanged -= MarkDirty;
            if (research != null)
            {
                research.ResearchAnalyzed -= HandleStringChanged;
                research.StateChanged -= HandleResearchChanged;
            }
            if (laboratoryWorkstation != null)
                laboratoryWorkstation.ItemsChanged -= MarkDirty;
            if (library != null)
                library.EntryUnlocked -= HandleStringChanged;
            if (stationStorage != null)
                stationStorage.StorageChanged -= MarkDirty;
            if (stationSystems != null)
                stationSystems.SystemsChanged -= MarkDirty;
            if (quests != null)
                quests.QuestsChanged -= MarkDirty;
            if (worldState != null)
                worldState.StateChanged -= MarkDirty;
            MaintainableObject.AnyConditionChanged -=
                HandleMaintenanceChanged;
        }

        private void HandleStringChanged(string _) => MarkDirty();
        private void HandlePowerChanged(StationPowerState _) => MarkDirty();
        private void HandleEnergyChanged()
        {
            if (energySystem == null)
                return;

            float currentEnergy = energySystem.CurrentEnergy;
            float currentBackupReserve =
                energySystem.CurrentBackupReserve;
            bool gridEnabled = energySystem.GridEnabled;
            bool changed = !hasObservedEnergyState ||
                currentEnergy != observedStationEnergy ||
                currentBackupReserve != observedStationBackupReserve ||
                gridEnabled != observedEnergyGridEnabled;

            observedStationEnergy = currentEnergy;
            observedStationBackupReserve = currentBackupReserve;
            observedEnergyGridEnabled = gridEnabled;
            hasObservedEnergyState = true;

            if (changed)
                MarkDirty();
        }

        private void ObserveEnergyState()
        {
            if (energySystem == null)
            {
                hasObservedEnergyState = false;
                return;
            }

            observedStationEnergy = energySystem.CurrentEnergy;
            observedStationBackupReserve =
                energySystem.CurrentBackupReserve;
            observedEnergyGridEnabled = energySystem.GridEnabled;
            hasObservedEnergyState = true;
        }
        private void HandleDroneStateChanged(DroneState _) => MarkDirty();
        private void HandleFloatChanged(float _) => MarkDirty();
        private void HandleSignalChanged(ExpeditionLocationData _) =>
            MarkDirty();
        private void HandleResearchChanged(
            ResearchController.ResearchState _) => MarkDirty();
        private void HandleMaintenanceChanged(string _, float __) =>
            MarkDirty();

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                StationUpgradeModeController.Instance?
                    .PrepareForSessionEnd();
                Flush();
            }
        }

        private void OnApplicationQuit()
        {
            StationUpgradeModeController.Instance?.PrepareForSessionEnd();
            Flush();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this)
                Instance = null;
        }
    }
}
