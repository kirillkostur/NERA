using System;
using System.Collections.Generic;
using NERA.Drone;
using NERA.Inventory;
using NERA.Items;
using NERA.Maintenance;
using UnityEngine;

namespace NERA.Station
{
    public sealed class StationSystemsController : MonoBehaviour
    {
        [SerializeField] private StationSystemsConfig config;

        private readonly Dictionary<StationSystemType, int> upgradeLevels =
            new Dictionary<StationSystemType, int>();
        private readonly Dictionary<StationSystemType, bool> requestedStates =
            new Dictionary<StationSystemType, bool>();

        public static StationSystemsController Instance { get; private set; }
        public event Action SystemsChanged;

        public StationSystemsConfig Config =>
            config != null ? config : config = StationSystemsConfig.LoadDefault();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            InitializeDefaults();
        }

        public StationSystemDefinition GetDefinition(StationSystemType type)
        {
            return Config.Find(type);
        }

        public int GetUpgradeLevel(StationSystemType type)
        {
            return upgradeLevels.TryGetValue(type, out int level) ? level : 0;
        }

        public bool IsUnlocked(StationSystemType type)
        {
            StationSystemDefinition definition = GetDefinition(type);
            return definition == null ||
                !definition.RequiresUpgradeToOperate ||
                GetUpgradeLevel(type) > 0;
        }

        public bool IsRequestedActive(StationSystemType type)
        {
            StationSystemDefinition definition = GetDefinition(type);
            if (definition == null || !definition.Controllable)
                return true;

            return requestedStates.TryGetValue(type, out bool active) && active;
        }

        public bool IsMaintenanceReady(StationSystemType type)
        {
            MaintenanceRole role = type switch
            {
                StationSystemType.SolarPanel => MaintenanceRole.SolarPanel,
                StationSystemType.Antenna => MaintenanceRole.Antenna,
                StationSystemType.Turret => MaintenanceRole.Turret,
                _ => MaintenanceRole.Generic
            };

            if (role == MaintenanceRole.Generic)
                return true;

            MaintainableObject maintenance = FindMaintenance(role);
            return maintenance == null || maintenance.IsOperational;
        }

        public float GetCondition(StationSystemType type)
        {
            MaintenanceRole role = type switch
            {
                StationSystemType.SolarPanel => MaintenanceRole.SolarPanel,
                StationSystemType.Antenna => MaintenanceRole.Antenna,
                StationSystemType.Turret => MaintenanceRole.Turret,
                _ => MaintenanceRole.Generic
            };

            MaintainableObject maintenance = role != MaintenanceRole.Generic
                ? FindMaintenance(role)
                : null;
            return maintenance != null ? maintenance.Condition : 1f;
        }

        public bool CanStart(StationSystemType type, out string reason)
        {
            StationSystemDefinition definition = GetDefinition(type);
            if (definition == null || !definition.Controllable)
            {
                reason = "This system cannot be controlled from the computer.";
                return false;
            }

            if (!IsUnlocked(type))
            {
                reason = "Upgrade required.";
                return false;
            }

            if (!IsMaintenanceReady(type))
            {
                reason = "Cleaning or repair required.";
                return false;
            }

            Energy.EnergySystemController energy = Energy.EnergySystemController.Instance;
            if (energy == null || !energy.HasUsablePower)
            {
                reason = "Station power is unavailable.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool SetRequestedActive(StationSystemType type, bool active)
        {
            StationSystemDefinition definition = GetDefinition(type);
            if (definition == null || !definition.Controllable)
                return false;

            if (type == StationSystemType.Drone &&
                !active &&
                DroneScanController.Instance != null &&
                DroneScanController.Instance.State == DroneState.Scanning)
            {
                return false;
            }

            if (active && !CanStart(type, out _))
                return false;

            if (requestedStates.TryGetValue(type, out bool current) && current == active)
                return true;

            requestedStates[type] = active;
            SystemsChanged?.Invoke();
            return true;
        }

        public bool CanUpgrade(
            StationSystemType type,
            PlayerInventory inventory,
            StationStorageController storage)
        {
            StationSystemDefinition definition = GetDefinition(type);
            if (definition == null || !definition.Upgradeable ||
                GetUpgradeLevel(type) >= definition.MaxLevel ||
                string.IsNullOrWhiteSpace(definition.RequiredItemId))
            {
                return false;
            }

            int available = (inventory != null
                    ? inventory.CountItem(definition.RequiredItemId)
                    : 0) +
                (storage != null
                    ? storage.CountItem(definition.RequiredItemId)
                    : 0);
            return available >= definition.RequiredItemCount;
        }

        public bool TryUpgrade(
            StationSystemType type,
            PlayerInventory inventory,
            StationStorageController storage)
        {
            if (!CanUpgrade(type, inventory, storage))
                return false;

            StationSystemDefinition definition = GetDefinition(type);
            int remaining = definition.RequiredItemCount;

            while (remaining > 0 && storage != null &&
                storage.TryRemoveOne(definition.RequiredItemId, out _))
            {
                remaining--;
            }

            while (remaining > 0 && inventory != null &&
                inventory.TryRemoveOne(definition.RequiredItemId, out _))
            {
                remaining--;
            }

            if (remaining > 0)
            {
                Debug.LogError(
                    $"Station upgrade '{type}' lost its validated item source.",
                    this);
                return false;
            }

            upgradeLevels[type] = GetUpgradeLevel(type) + 1;
            if (definition.RequiresUpgradeToOperate)
                requestedStates[type] = true;

            SystemsChanged?.Invoke();
            return true;
        }

        public bool CanDroneReach(Expeditions.ExpeditionLocationData location)
        {
            return location != null &&
                GetUpgradeLevel(StationSystemType.Drone) >=
                location.RequiredDroneUpgradeLevel;
        }

        public IEnumerable<KeyValuePair<StationSystemType, int>> UpgradeLevels =>
            upgradeLevels;
        public IEnumerable<KeyValuePair<StationSystemType, bool>> RequestedStates =>
            requestedStates;

        public void Restore(
            IEnumerable<KeyValuePair<StationSystemType, int>> levels,
            IEnumerable<KeyValuePair<StationSystemType, bool>> states)
        {
            InitializeDefaults();

            if (levels != null)
            {
                foreach (KeyValuePair<StationSystemType, int> pair in levels)
                {
                    StationSystemDefinition definition = GetDefinition(pair.Key);
                    if (definition != null)
                    {
                        upgradeLevels[pair.Key] = Mathf.Clamp(
                            pair.Value, 0, definition.MaxLevel);
                    }
                }
            }

            if (states != null)
            {
                foreach (KeyValuePair<StationSystemType, bool> pair in states)
                {
                    StationSystemDefinition definition = GetDefinition(pair.Key);
                    if (definition != null && definition.Controllable)
                        requestedStates[pair.Key] = pair.Value;
                }
            }

            SystemsChanged?.Invoke();
        }

        public void ResetSystems()
        {
            InitializeDefaults();
            SystemsChanged?.Invoke();
        }

        private void InitializeDefaults()
        {
            upgradeLevels.Clear();
            requestedStates.Clear();
            foreach (StationSystemDefinition definition in Config.Systems)
            {
                if (definition == null)
                    continue;

                upgradeLevels[definition.SystemType] = 0;
                requestedStates[definition.SystemType] =
                    definition.InitiallyActive &&
                    !definition.RequiresUpgradeToOperate;
            }
        }

        private static MaintainableObject FindMaintenance(MaintenanceRole role)
        {
            MaintainableObject[] candidates = FindObjectsByType<MaintainableObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (MaintainableObject candidate in candidates)
            {
                if (candidate != null && candidate.Role == role)
                    return candidate;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
