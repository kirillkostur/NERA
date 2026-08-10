using System;
using System.Collections.Generic;
using NERA.Drone;
using NERA.Inventory;
using NERA.Items;
using NERA.Maintenance;
using NERA.Quests;
using UnityEngine;

namespace NERA.Station
{
    public readonly struct StationObjectSystemState
    {
        public StationObjectSystemState(
            StationSystemType systemType,
            string objectId,
            int upgradeLevel,
            bool requestedActive)
        {
            SystemType = systemType;
            ObjectId = objectId;
            UpgradeLevel = upgradeLevel;
            RequestedActive = requestedActive;
        }

        public StationSystemType SystemType { get; }
        public string ObjectId { get; }
        public int UpgradeLevel { get; }
        public bool RequestedActive { get; }
    }

    public sealed class StationSystemsController : MonoBehaviour
    {
        private sealed class ObjectRuntimeState
        {
            public StationSystemType SystemType;
            public int UpgradeLevel;
            public bool RequestedActive;
        }

        [SerializeField] private StationSystemsConfig config;

        private readonly Dictionary<StationSystemType, int> upgradeLevels =
            new Dictionary<StationSystemType, int>();
        private readonly Dictionary<StationSystemType, bool> requestedStates =
            new Dictionary<StationSystemType, bool>();
        private readonly Dictionary<string, ObjectRuntimeState> objectStates =
            new Dictionary<string, ObjectRuntimeState>(
                StringComparer.OrdinalIgnoreCase);

        public static StationSystemsController Instance { get; private set; }
        public static event Action<StationSystemsController> InstanceChanged;
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
            InstanceChanged?.Invoke(this);
        }

        private void Start()
        {
            SynchronizeQuestStates();
        }

        public StationSystemDefinition GetDefinition(
            StationSystemType type,
            string objectId = null)
        {
            return Config.Find(type, objectId);
        }

        public bool HasRequiredCharge(
            StationSystemType type,
            string objectId = null)
        {
            StationSystemDefinition definition = GetDefinition(type, objectId);
            Energy.EnergySystemController energy =
                Energy.EnergySystemController.Instance;
            return definition != null &&
                   energy != null &&
                   energy.HasSufficientCharge(
                       energy.Config.GetMinimumCharge01(type, objectId));
        }

        public int GetUpgradeLevel(StationSystemType type)
        {
            StationSystemDefinition definition = GetDefinition(type);
            if (definition != null &&
                !string.IsNullOrWhiteSpace(definition.ObjectId))
            {
                return GetUpgradeLevel(
                    type,
                    definition.ObjectId,
                    definition.InitialLevel);
            }

            return upgradeLevels.TryGetValue(type, out int level)
                ? level
                : definition?.InitialLevel ?? 0;
        }

        public int GetUpgradeLevel(
            StationSystemType type,
            string objectId,
            int initialLevel)
        {
            if (string.IsNullOrWhiteSpace(objectId))
            {
                return upgradeLevels.TryGetValue(type, out int sharedLevel)
                    ? sharedLevel
                    : initialLevel;
            }

            return EnsureObjectState(
                type,
                objectId,
                initialLevel,
                initialLevel > 0).UpgradeLevel;
        }

        public bool IsUnlocked(StationSystemType type)
        {
            StationSystemDefinition definition = GetDefinition(type);
            return IsUnlocked(
                type,
                definition?.ObjectId,
                definition?.InitialLevel ?? 0);
        }

        public bool IsUnlocked(
            StationSystemType type,
            string objectId,
            int initialLevel)
        {
            StationSystemDefinition definition = GetDefinition(type, objectId);
            return definition == null ||
                !definition.RequiresUpgradeToOperate ||
                GetUpgradeLevel(type, objectId, initialLevel) > 0;
        }

        public bool IsRequestedActive(StationSystemType type)
        {
            StationSystemDefinition definition = GetDefinition(type);
            return IsRequestedActive(
                type,
                definition?.ObjectId,
                definition?.InitialLevel ?? 0,
                definition?.InitiallyActive ?? false);
        }

        public bool IsRequestedActive(
            StationSystemType type,
            string objectId,
            int initialLevel,
            bool initiallyActive)
        {
            if (!string.IsNullOrWhiteSpace(objectId))
            {
                return EnsureObjectState(
                    type,
                    objectId,
                    initialLevel,
                    initiallyActive).RequestedActive;
            }

            StationSystemDefinition definition = GetDefinition(type, objectId);
            if (requestedStates.TryGetValue(type, out bool active))
                return active;
            return definition == null || definition.InitiallyActive;
        }

        public bool SetCriticalSystemActive(
            StationSystemType type,
            bool active)
        {
            return SetCriticalSystemActive(
                type,
                active,
                false);
        }

        public bool SetCriticalSystemActive(
            StationSystemType type,
            bool active,
            bool reportActivationWhenAlreadyActive)
        {
            if (type != StationSystemType.Battery &&
                type != StationSystemType.Computer)
            {
                return false;
            }

            StationSystemDefinition definition = GetDefinition(type);
            string objectId = definition?.ObjectId;
            if (!string.IsNullOrWhiteSpace(objectId))
            {
                ObjectRuntimeState objectState = EnsureObjectState(
                    type,
                    objectId,
                    definition.InitialLevel,
                    definition.InitiallyActive);
                if (objectState.RequestedActive == active)
                {
                    if (active && reportActivationWhenAlreadyActive)
                        ReportSystemActivated(definition, objectId);
                    return true;
                }

                objectState.RequestedActive = active;
            }
            else
            {
                if (requestedStates.TryGetValue(
                        type,
                        out bool current) &&
                    current == active)
                {
                    if (active && reportActivationWhenAlreadyActive)
                        ReportSystemActivated(definition, objectId);
                    return true;
                }

                requestedStates[type] = active;
            }

            if (active)
                ReportSystemActivated(definition, objectId);
            SystemsChanged?.Invoke();
            return true;
        }

        public bool IsMaintenanceReady(StationSystemType type)
        {
            return IsMaintenanceReady(type, null);
        }

        public bool IsMaintenanceReady(
            StationSystemType type,
            string objectId)
        {
            if (type == StationSystemType.Turret &&
                !string.IsNullOrWhiteSpace(objectId))
            {
                StationTurretController turret =
                    StationTurretController.FindById(objectId);
                return turret == null || turret.IsAlive;
            }

            MaintenanceRole role = type switch
            {
                StationSystemType.SolarPanel => MaintenanceRole.SolarPanel,
                StationSystemType.Antenna => MaintenanceRole.Antenna,
                StationSystemType.Turret => MaintenanceRole.Turret,
                _ => MaintenanceRole.Generic
            };

            if (role == MaintenanceRole.Generic)
                return true;

            MaintainableObject maintenance = FindMaintenance(
                type,
                objectId,
                role);
            return maintenance == null || maintenance.IsOperational;
        }

        public float GetCondition(StationSystemType type)
        {
            return GetCondition(type, null);
        }

        public float GetCondition(
            StationSystemType type,
            string objectId)
        {
            if (type == StationSystemType.Turret &&
                !string.IsNullOrWhiteSpace(objectId))
            {
                StationTurretController turret =
                    StationTurretController.FindById(objectId);
                return turret != null ? turret.Condition : 1f;
            }

            MaintenanceRole role = type switch
            {
                StationSystemType.SolarPanel => MaintenanceRole.SolarPanel,
                StationSystemType.Antenna => MaintenanceRole.Antenna,
                StationSystemType.Turret => MaintenanceRole.Turret,
                _ => MaintenanceRole.Generic
            };

            MaintainableObject maintenance = role != MaintenanceRole.Generic
                ? FindMaintenance(type, objectId, role)
                : null;
            return maintenance != null ? maintenance.Condition : 1f;
        }

        public bool CanStart(StationSystemType type, out string reason)
        {
            StationSystemDefinition definition = GetDefinition(type);
            return CanStart(
                type,
                definition?.ObjectId,
                definition?.InitialLevel ?? 0,
                out reason);
        }

        public bool CanStart(
            StationSystemType type,
            string objectId,
            int initialLevel,
            out string reason)
        {
            StationSystemDefinition definition = GetDefinition(type, objectId);
            if (definition == null || !definition.Controllable)
            {
                reason = "This system cannot be controlled from the computer.";
                return false;
            }

            if (!IsUnlocked(type, objectId, initialLevel))
            {
                reason = "Upgrade required.";
                return false;
            }

            if (!IsMaintenanceReady(type, objectId))
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

            float minimumChargePercent =
                energy.Config.GetMinimumChargePercent(type, objectId);
            if (!energy.HasSufficientCharge(minimumChargePercent / 100f))
            {
                reason =
                    $"Battery charge below {minimumChargePercent:0}%.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool SetRequestedActive(StationSystemType type, bool active)
        {
            StationSystemDefinition definition = GetDefinition(type);
            return SetRequestedActive(
                type,
                active,
                definition?.ObjectId,
                definition?.InitialLevel ?? 0,
                definition?.InitiallyActive ?? false);
        }

        public bool SetRequestedActive(
            StationSystemType type,
            bool active,
            string objectId,
            int initialLevel,
            bool initiallyActive)
        {
            StationSystemDefinition definition = GetDefinition(type, objectId);
            if (definition == null || !definition.Controllable)
                return false;

            if (type == StationSystemType.Drone &&
                !active &&
                DroneScanController.Instance != null &&
                DroneScanController.Instance.State == DroneState.Scanning)
            {
                return false;
            }

            if (active &&
                !CanStart(type, objectId, initialLevel, out _))
                return false;

            if (!string.IsNullOrWhiteSpace(objectId))
            {
                ObjectRuntimeState objectState = EnsureObjectState(
                    type,
                    objectId,
                    initialLevel,
                    initiallyActive);
                if (objectState.RequestedActive == active)
                    return true;

                objectState.RequestedActive = active;
                if (active)
                    ReportSystemActivated(definition, objectId);
                else
                    ReportSystemDeactivated(definition, objectId);
                SystemsChanged?.Invoke();
                return true;
            }

            if (requestedStates.TryGetValue(type, out bool current) && current == active)
                return true;

            requestedStates[type] = active;
            if (active)
                ReportSystemActivated(definition, objectId);
            else
                ReportSystemDeactivated(definition, objectId);
            SystemsChanged?.Invoke();
            return true;
        }

        public bool DisableFromFault(
            StationSystemType type,
            string objectId,
            string cause)
        {
            StationSystemDefinition definition = GetDefinition(type, objectId);
            if (definition == null)
                return false;

            bool changed;
            if (!string.IsNullOrWhiteSpace(objectId))
            {
                ObjectRuntimeState objectState = EnsureObjectState(
                    type,
                    objectId,
                    definition.InitialLevel,
                    definition.InitiallyActive);
                changed = objectState.RequestedActive;
                objectState.RequestedActive = false;
            }
            else
            {
                changed = !requestedStates.TryGetValue(type, out bool current) ||
                    current;
                requestedStates[type] = false;
            }

            QuestController.Instance?.ReportStationFault(
                ResolveQuestTargetId(type, objectId),
                definition.DisplayName,
                cause);
            ReportSystemDeactivated(definition, objectId, cause);
            if (changed)
                SystemsChanged?.Invoke();
            return true;
        }

        public bool CanUpgrade(
            StationSystemType type,
            PlayerInventory inventory,
            StationStorageController storage)
        {
            return CanUpgradeTo(
                type,
                GetUpgradeLevel(type) + 1,
                inventory,
                storage,
                out _);
        }

        public bool CanUpgradeTo(
            StationSystemType type,
            int targetLevel,
            PlayerInventory inventory,
            StationStorageController storage,
            out string reason)
        {
            StationSystemDefinition definition = GetDefinition(type);
            return CanUpgradeTo(
                type,
                definition?.ObjectId,
                definition?.InitialLevel ?? 0,
                targetLevel,
                inventory,
                storage,
                out reason);
        }

        public bool CanUpgradeTo(
            StationSystemType type,
            string objectId,
            int initialLevel,
            int targetLevel,
            PlayerInventory inventory,
            StationStorageController storage,
            out string reason)
        {
            StationSystemDefinition definition = GetDefinition(type, objectId);
            StationUpgradeLevelDefinition upgrade =
                Config.GetUpgradeDefinition(
                    type,
                    objectId,
                    targetLevel);
            int currentLevel = GetUpgradeLevel(
                type,
                objectId,
                initialLevel);
            int maxLevel = Config.GetMaxLevel(type, objectId);
            if (definition == null || !definition.Upgradeable ||
                targetLevel != currentLevel + 1 ||
                targetLevel > maxLevel ||
                upgrade == null)
            {
                reason = targetLevel <= currentLevel
                    ? "Upgrade already installed."
                    : "Previous upgrade level is required.";
                return false;
            }

            Dictionary<string, int> requiredTotals =
                new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (StationUpgradeItemRequirement requirement in
                     upgrade.RequiredItems)
            {
                if (requirement == null ||
                    string.IsNullOrWhiteSpace(requirement.ItemId))
                {
                    reason = "Upgrade item is not configured.";
                    return false;
                }

                requiredTotals.TryGetValue(
                    requirement.ItemId,
                    out int alreadyRequired);
                int totalRequired = alreadyRequired + requirement.Count;
                requiredTotals[requirement.ItemId] = totalRequired;
                int available = (inventory != null
                        ? inventory.CountItem(requirement.ItemId)
                        : 0) +
                    (storage != null
                        ? storage.CountItem(requirement.ItemId)
                        : 0);
                if (available < totalRequired)
                {
                    reason =
                        $"{requirement.DisplayName}: " +
                        $"{available}/{totalRequired}.";
                    return false;
                }
            }

            float energyCost = upgrade.EnergyCost;
            Energy.EnergySystemController energy =
                Energy.EnergySystemController.Instance;
            if (energyCost > 0f &&
                (energy == null || !energy.CanSpendEnergy(energyCost)))
            {
                reason = $"Required energy: {energyCost:0}.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryUpgrade(
            StationSystemType type,
            PlayerInventory inventory,
            StationStorageController storage)
        {
            return TryUpgradeTo(
                type,
                GetUpgradeLevel(type) + 1,
                inventory,
                storage);
        }

        public bool TryUpgradeTo(
            StationSystemType type,
            int targetLevel,
            PlayerInventory inventory,
            StationStorageController storage)
        {
            StationSystemDefinition definition = GetDefinition(type);
            return TryUpgradeTo(
                type,
                definition?.ObjectId,
                definition?.InitialLevel ?? 0,
                targetLevel,
                inventory,
                storage);
        }

        public bool TryUpgradeTo(
            StationSystemType type,
            string objectId,
            int initialLevel,
            int targetLevel,
            PlayerInventory inventory,
            StationStorageController storage)
        {
            if (!CanUpgradeTo(
                    type,
                    objectId,
                    initialLevel,
                    targetLevel,
                    inventory,
                    storage,
                    out _))
                return false;

            StationSystemDefinition definition = GetDefinition(type, objectId);
            StationUpgradeLevelDefinition upgrade =
                Config.GetUpgradeDefinition(
                    type,
                    objectId,
                    targetLevel);
            foreach (StationUpgradeItemRequirement requirement in
                     upgrade.RequiredItems)
            {
                int remaining = requirement.Count;
                while (remaining > 0 && storage != null &&
                    storage.TryRemoveOne(requirement.ItemId, out _))
                {
                    remaining--;
                }

                while (remaining > 0 && inventory != null &&
                    inventory.TryRemoveOne(requirement.ItemId, out _))
                {
                    remaining--;
                }

                if (remaining > 0)
                {
                    Debug.LogError(
                        $"Station upgrade '{type}' lost its validated " +
                        $"item source for '{requirement.ItemId}'.",
                        this);
                    return false;
                }
            }

            float energyCost = upgrade.EnergyCost;
            if (energyCost > 0f &&
                Energy.EnergySystemController.Instance?.TrySpendEnergy(
                    energyCost) != true)
            {
                Debug.LogError(
                    $"Station upgrade '{type}' lost its validated energy source.",
                    this);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(objectId))
            {
                ObjectRuntimeState objectState = EnsureObjectState(
                    type,
                    objectId,
                    initialLevel,
                    initialLevel > 0);
                objectState.UpgradeLevel = targetLevel;
                if (definition.RequiresUpgradeToOperate)
                    objectState.RequestedActive = true;
            }
            else
            {
                upgradeLevels[type] = targetLevel;
                if (definition.RequiresUpgradeToOperate)
                    requestedStates[type] = true;
            }

            QuestController.Instance?.Report(
                QuestSignalType.StationSystemUpgraded,
                ResolveQuestTargetId(type, objectId),
                definition.DisplayName,
                value: targetLevel);
            SystemsChanged?.Invoke();
            return true;
        }

        public bool CanDroneReach(Expeditions.ExpeditionLocationData location)
        {
            return location != null &&
                GetUpgradeLevel(StationSystemType.Drone) >=
                location.RequiredDroneUpgradeLevel;
        }

        public bool CanAntennaReach(Expeditions.ExpeditionLocationData location)
        {
            return location != null &&
                GetUpgradeLevel(StationSystemType.Antenna) >=
                location.RequiredAntennaUpgradeLevel;
        }

        public IEnumerable<KeyValuePair<StationSystemType, int>> UpgradeLevels =>
            upgradeLevels;
        public IEnumerable<KeyValuePair<StationSystemType, bool>> RequestedStates =>
            requestedStates;
        public IEnumerable<StationObjectSystemState> ObjectStates
        {
            get
            {
                foreach (KeyValuePair<string, ObjectRuntimeState> pair in
                         objectStates)
                {
                    yield return new StationObjectSystemState(
                        pair.Value.SystemType,
                        pair.Key,
                        pair.Value.UpgradeLevel,
                        pair.Value.RequestedActive);
                }
            }
        }

        public void RegisterObject(
            StationSystemType type,
            string objectId,
            int initialLevel,
            bool initiallyActive)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return;

            EnsureObjectState(
                type,
                objectId,
                initialLevel,
                initiallyActive);
        }

        public void Restore(
            IEnumerable<KeyValuePair<StationSystemType, int>> levels,
            IEnumerable<KeyValuePair<StationSystemType, bool>> states,
            IEnumerable<StationObjectSystemState> restoredObjects = null)
        {
            InitializeDefaults();

            if (levels != null)
            {
                foreach (KeyValuePair<StationSystemType, int> pair in levels)
                {
                    StationSystemDefinition definition = GetDefinition(pair.Key);
                    if (definition != null)
                    {
                        int minimumLevel = definition.RequiresUpgradeToOperate
                            ? 0
                            : definition.InitialLevel;
                        upgradeLevels[pair.Key] = Mathf.Clamp(
                            pair.Value, minimumLevel, definition.MaxLevel);
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

            if (restoredObjects != null)
            {
                foreach (StationObjectSystemState restored in restoredObjects)
                {
                    if (string.IsNullOrWhiteSpace(restored.ObjectId))
                        continue;

                    StationSystemDefinition definition =
                        GetDefinition(
                            restored.SystemType,
                            restored.ObjectId);
                    if (definition == null)
                        continue;

                    string id = NormalizeObjectId(restored.ObjectId);
                    int restoredLevel = Mathf.Clamp(
                        restored.UpgradeLevel,
                        0,
                        Config.GetMaxLevel(
                            restored.SystemType,
                            restored.ObjectId));
                    objectStates[id] = new ObjectRuntimeState
                    {
                        SystemType = restored.SystemType,
                        UpgradeLevel = restoredLevel,
                        RequestedActive =
                            definition.Controllable &&
                            restored.RequestedActive &&
                            (!definition.RequiresUpgradeToOperate ||
                             restoredLevel > 0)
                    };
                }
            }

            SystemsChanged?.Invoke();
            SynchronizeQuestStates();
        }

        public void ResetSystems()
        {
            InitializeDefaults();
            SystemsChanged?.Invoke();
            SynchronizeQuestStates();
        }

        private void InitializeDefaults()
        {
            upgradeLevels.Clear();
            requestedStates.Clear();
            objectStates.Clear();
            foreach (StationSystemDefinition definition in Config.StationObjects)
            {
                if (definition == null)
                    continue;

                bool requestedActive =
                    definition.InitiallyActive &&
                    (!definition.RequiresUpgradeToOperate ||
                     definition.InitialLevel > 0);
                if (string.IsNullOrWhiteSpace(definition.ObjectId))
                {
                    upgradeLevels[definition.SystemType] =
                        definition.InitialLevel;
                    requestedStates[definition.SystemType] = requestedActive;
                }
                else
                {
                    objectStates[NormalizeObjectId(definition.ObjectId)] =
                        new ObjectRuntimeState
                        {
                            SystemType = definition.SystemType,
                            UpgradeLevel = definition.InitialLevel,
                            RequestedActive = requestedActive
                        };
                }
            }
        }

        private ObjectRuntimeState EnsureObjectState(
            StationSystemType type,
            string objectId,
            int initialLevel,
            bool initiallyActive)
        {
            string id = NormalizeObjectId(objectId);
            if (objectStates.TryGetValue(
                    id,
                    out ObjectRuntimeState existing))
            {
                return existing;
            }

            StationSystemDefinition definition = GetDefinition(type, id);
            if (definition != null &&
                string.Equals(
                    definition.ObjectId,
                    id,
                    StringComparison.OrdinalIgnoreCase))
            {
                initialLevel = definition.InitialLevel;
                initiallyActive = definition.InitiallyActive;
            }

            int maxLevel = Config.GetMaxLevel(type, id);
            if (maxLevel <= 0)
                maxLevel = definition?.MaxLevel ?? Mathf.Max(1, initialLevel);
            int level = Mathf.Clamp(initialLevel, 0, maxLevel);
            ObjectRuntimeState created = new ObjectRuntimeState
            {
                SystemType = type,
                UpgradeLevel = level,
                RequestedActive =
                    initiallyActive &&
                    (definition == null ||
                     !definition.RequiresUpgradeToOperate ||
                     level > 0)
            };
            objectStates[id] = created;
            return created;
        }

        private static string NormalizeObjectId(string objectId)
        {
            return objectId?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static void ReportSystemActivated(
            StationSystemDefinition definition,
            string objectId)
        {
            if (definition == null)
                return;

            QuestController.Instance?.Report(
                QuestSignalType.StationSystemActivated,
                ResolveQuestTargetId(definition.SystemType, objectId),
                definition.DisplayName);
        }

        private static void ReportSystemDeactivated(
            StationSystemDefinition definition,
            string objectId,
            string cause = null)
        {
            if (definition == null)
                return;

            QuestController.Instance?.Report(
                QuestSignalType.StationSystemDeactivated,
                ResolveQuestTargetId(definition.SystemType, objectId),
                definition.DisplayName,
                cause: cause);
        }

        private void SynchronizeQuestStates()
        {
            QuestController quests = QuestController.Instance;
            if (quests == null)
                return;

            foreach (StationSystemDefinition definition in
                     Config.StationObjects)
            {
                if (definition == null)
                    continue;

                string objectId = definition.ObjectId;
                string targetId = ResolveQuestTargetId(
                    definition.SystemType,
                    objectId);
                bool active = IsRequestedActive(
                    definition.SystemType,
                    objectId,
                    definition.InitialLevel,
                    definition.InitiallyActive);
                quests.SynchronizeState(
                    active
                        ? QuestSignalType.StationSystemActivated
                        : QuestSignalType.StationSystemDeactivated,
                    targetId,
                    definition.DisplayName);
                quests.SynchronizeState(
                    QuestSignalType.StationSystemUpgraded,
                    targetId,
                    definition.DisplayName,
                    value: GetUpgradeLevel(
                        definition.SystemType,
                        objectId,
                        definition.InitialLevel));
            }
        }

        private static string ResolveQuestTargetId(
            StationSystemType type,
            string objectId)
        {
            return string.IsNullOrWhiteSpace(objectId)
                ? type.ToString()
                : objectId;
        }

        private MaintainableObject FindMaintenance(
            StationSystemType type,
            string objectId,
            MaintenanceRole fallbackRole)
        {
            StationSystemDefinition definition = GetDefinition(type, objectId);
            string stableId = string.IsNullOrWhiteSpace(objectId)
                ? definition?.ObjectId
                : objectId;
            if (!string.IsNullOrWhiteSpace(stableId))
            {
                return MaintainableObject.TryFind(
                    stableId,
                    out MaintainableObject identified)
                    ? identified
                    : null;
            }

            // Compatibility path for old configs without an ObjectId. New
            // station devices must resolve through their stable ID above.
            MaintainableObject[] candidates = FindObjectsByType<MaintainableObject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (MaintainableObject candidate in candidates)
            {
                if (candidate != null &&
                    candidate.isActiveAndEnabled &&
                    candidate.Role == fallbackRole)
                {
                    return candidate;
                }
            }
            return null;
        }

        private void OnDestroy()
        {
            if (Instance != this)
                return;

            Instance = null;
            InstanceChanged?.Invoke(null);
        }
    }
}
