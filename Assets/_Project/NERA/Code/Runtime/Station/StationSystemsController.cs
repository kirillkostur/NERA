using System;
using System.Collections.Generic;
using NERA.Drone;
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
            bool requestedActive,
            IEnumerable<StationInstalledPartState> installedParts = null)
        {
            SystemType = systemType;
            ObjectId = objectId?.Trim() ?? string.Empty;
            RequestedActive = requestedActive;
            InstalledParts = installedParts != null
                ? new List<StationInstalledPartState>(installedParts)
                : (IReadOnlyList<StationInstalledPartState>)
                    Array.Empty<StationInstalledPartState>();
        }

        public StationSystemType SystemType { get; }
        public string ObjectId { get; }
        public bool RequestedActive { get; }
        public IReadOnlyList<StationInstalledPartState> InstalledParts { get; }
    }

    /// <summary>
    /// Runtime state for station objects. Physical parts are the only upgrade
    /// progression: there are no abstract levels or sequential upgrade costs.
    /// </summary>
    public sealed class StationSystemsController : MonoBehaviour
    {
        private sealed class ObjectRuntimeState
        {
            public StationSystemType SystemType;
            public bool RequestedActive;
        }

        [SerializeField] private StationSystemsConfig config;

        private readonly Dictionary<StationSystemType, bool> requestedStates =
            new Dictionary<StationSystemType, bool>();
        private readonly Dictionary<string, ObjectRuntimeState> objectStates =
            new Dictionary<string, ObjectRuntimeState>(
                StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, string>>
            installedParts =
                new Dictionary<string, Dictionary<string, string>>(
                    StringComparer.OrdinalIgnoreCase);
        private ItemCatalogData itemCatalog;

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
                    energy.Config.GetMinimumCharge01(type, objectId),
                    definition.PowerPriority);
        }

        public string GetInstalledPartItemId(
            StationSystemType type,
            string objectId,
            string slotId)
        {
            string normalizedSlot = NormalizeSlotId(slotId);
            if (string.IsNullOrEmpty(normalizedSlot) ||
                !installedParts.TryGetValue(
                    GetPartStateKey(type, objectId),
                    out Dictionary<string, string> parts))
            {
                return string.Empty;
            }

            return parts.TryGetValue(normalizedSlot, out string itemId)
                ? itemId
                : string.Empty;
        }

        public IReadOnlyList<StationInstalledPartState> GetInstalledParts(
            StationSystemType type,
            string objectId)
        {
            if (!installedParts.TryGetValue(
                    GetPartStateKey(type, objectId),
                    out Dictionary<string, string> parts))
            {
                return Array.Empty<StationInstalledPartState>();
            }

            var result = new List<StationInstalledPartState>(parts.Count);
            foreach (KeyValuePair<string, string> pair in parts)
                result.Add(new StationInstalledPartState(pair.Key, pair.Value));
            result.Sort((left, right) => string.Compare(
                left.SlotId,
                right.SlotId,
                StringComparison.OrdinalIgnoreCase));
            return result;
        }

        public int GetInstalledPartCount(
            StationSystemType type,
            string objectId)
        {
            return installedParts.TryGetValue(
                GetPartStateKey(type, objectId),
                out Dictionary<string, string> parts)
                ? parts.Count
                : 0;
        }

        public bool TryInstallParts(
            StationSystemType type,
            string objectId,
            IReadOnlyList<StationPartInstallRequest> requests,
            out string reason)
        {
            if (requests == null || requests.Count == 0)
            {
                reason = "No parts selected.";
                return false;
            }

            StationSystemDefinition definition = GetDefinition(type, objectId);
            if (definition == null)
            {
                reason = "Station object is not configured.";
                return false;
            }

            string stateKey = GetPartStateKey(type, objectId);
            installedParts.TryGetValue(
                stateKey,
                out Dictionary<string, string> currentParts);
            var requestedSlots = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            foreach (StationPartInstallRequest request in requests)
            {
                string slotId = NormalizeSlotId(request.SlotId);
                if (string.IsNullOrEmpty(slotId) || request.Item == null ||
                    request.Item.ItemType != ItemType.EngineeringPart)
                {
                    reason = "Engineering part or slot is not configured.";
                    return false;
                }

                if (definition.FindSlot(slotId) == null)
                {
                    reason = $"Slot '{request.SlotId}' is not declared in " +
                        $"{definition.DisplayName}.";
                    return false;
                }

                if (!requestedSlots.Add(slotId) ||
                    currentParts != null && currentParts.ContainsKey(slotId))
                {
                    reason = $"Slot '{request.SlotId}' is already occupied.";
                    return false;
                }

                if (request.Item.FindEngineeringCompatibility(
                        type,
                        objectId,
                        slotId) == null)
                {
                    reason = $"{request.Item.DisplayName} does not fit " +
                        $"slot '{request.SlotId}'.";
                    return false;
                }
            }

            if (currentParts == null)
            {
                currentParts = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase);
                installedParts[stateKey] = currentParts;
            }

            foreach (StationPartInstallRequest request in requests)
            {
                currentParts[NormalizeSlotId(request.SlotId)] =
                    request.Item.ItemId;
            }

            int installedCount = currentParts.Count;
            QuestController.Instance?.Report(
                QuestSignalType.StationSystemUpgraded,
                ResolveQuestTargetId(type, objectId),
                definition.DisplayName,
                value: installedCount);
            SystemsChanged?.Invoke();
            return Succeed(out reason);
        }

        public float GetStat(
            StationSystemType type,
            string objectId,
            StationObjectStat stat,
            float fallback = 0f)
        {
            StationSystemDefinition definition = GetDefinition(type, objectId);
            float baseValue = definition?.GetBaseStat(stat, fallback) ?? fallback;
            if (!installedParts.TryGetValue(
                    GetPartStateKey(type, objectId),
                    out Dictionary<string, string> parts) ||
                parts.Count == 0)
            {
                return baseValue;
            }

            itemCatalog ??= Resources.Load<ItemCatalogData>(
                "ItemCatalog_Default");
            float additive = 0f;
            float multiplier = 1f;
            foreach (KeyValuePair<string, string> part in parts)
            {
                ItemData item = itemCatalog?.Find(part.Value);
                EngineeringPartCompatibility compatibility =
                    item?.FindEngineeringCompatibility(
                        type,
                        objectId,
                        part.Key);
                if (compatibility == null)
                    continue;

                foreach (StationObjectStatModifierDefinition modifier in
                         compatibility.Modifiers)
                {
                    if (modifier == null || modifier.Stat != stat)
                        continue;
                    if (modifier.Mode == StationStatModifierMode.Add)
                        additive += modifier.Value;
                    else
                        multiplier *= modifier.Value;
                }
            }
            return (baseValue + additive) * multiplier;
        }

        public bool IsRequestedActive(
            StationSystemType type,
            string objectId = null)
        {
            StationSystemDefinition definition = GetDefinition(type, objectId);
            string resolvedId = ResolveObjectId(definition, objectId);
            if (!string.IsNullOrEmpty(resolvedId))
            {
                return EnsureObjectState(
                    type,
                    resolvedId,
                    definition?.InitiallyActive ?? true).RequestedActive;
            }

            return requestedStates.TryGetValue(type, out bool active)
                ? active
                : definition?.InitiallyActive ?? true;
        }

        public bool SetCriticalSystemActive(
            StationSystemType type,
            bool active,
            bool reportActivationWhenAlreadyActive = false)
        {
            if (type != StationSystemType.Battery &&
                type != StationSystemType.Computer)
            {
                return false;
            }

            StationSystemDefinition definition = GetDefinition(type);
            if (definition == null)
                return false;
            string objectId = definition.ObjectId;
            bool current = IsRequestedActive(type, objectId);
            if (current == active)
            {
                if (active && reportActivationWhenAlreadyActive)
                    ReportSystemActivated(definition, objectId);
                return true;
            }

            SetRuntimeActive(type, objectId, active, definition.InitiallyActive);
            if (active)
                ReportSystemActivated(definition, objectId);
            else
                ReportSystemDeactivated(definition, objectId);
            SystemsChanged?.Invoke();
            return true;
        }

        public bool IsMaintenanceReady(
            StationSystemType type,
            string objectId = null)
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

        public float GetCondition(
            StationSystemType type,
            string objectId = null)
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
            return CanStart(type, definition?.ObjectId, out reason);
        }

        public bool CanStart(
            StationSystemType type,
            string objectId,
            out string reason)
        {
            StationSystemDefinition definition = GetDefinition(type, objectId);
            if (definition == null || !definition.Controllable)
            {
                reason = "This system cannot be controlled from the computer.";
                return false;
            }
            if (!IsMaintenanceReady(type, objectId))
            {
                reason = "Cleaning or repair required.";
                return false;
            }

            Energy.EnergySystemController energy =
                Energy.EnergySystemController.Instance;
            if (energy == null || !energy.HasUsablePower)
            {
                reason = "Station power is unavailable.";
                return false;
            }

            float minimumChargePercent =
                energy.Config.GetMinimumChargePercent(type, objectId);
            if (!energy.HasSufficientCharge(minimumChargePercent / 100f))
            {
                reason = $"Battery charge below {minimumChargePercent:0}%.";
                return false;
            }
            return Succeed(out reason);
        }

        public bool SetRequestedActive(
            StationSystemType type,
            bool active,
            string objectId = null)
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
            if (active && !CanStart(type, objectId, out _))
                return false;

            string resolvedId = ResolveObjectId(definition, objectId);
            if (IsRequestedActive(type, resolvedId) == active)
                return true;
            SetRuntimeActive(type, resolvedId, active, definition.InitiallyActive);
            if (active)
                ReportSystemActivated(definition, resolvedId);
            else
                ReportSystemDeactivated(definition, resolvedId);
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
            string resolvedId = ResolveObjectId(definition, objectId);
            bool changed = IsRequestedActive(type, resolvedId);
            SetRuntimeActive(type, resolvedId, false, definition.InitiallyActive);
            QuestController.Instance?.ReportStationFault(
                ResolveQuestTargetId(type, resolvedId),
                definition.DisplayName,
                cause);
            ReportSystemDeactivated(definition, resolvedId, cause);
            if (changed)
                SystemsChanged?.Invoke();
            return true;
        }

        public bool DisableFromPowerLimit(
            StationSystemType type,
            string objectId)
        {
            StationSystemDefinition definition = GetDefinition(type, objectId);
            if (definition == null)
                return false;

            string resolvedId = ResolveObjectId(definition, objectId);
            if (!IsRequestedActive(type, resolvedId))
                return false;

            SetRuntimeActive(
                type,
                resolvedId,
                false,
                definition.InitiallyActive);
            ReportSystemDeactivated(
                definition,
                resolvedId,
                "Insufficient battery power output.");
            SystemsChanged?.Invoke();
            return true;
        }

        public bool CanDroneReach(Expeditions.ExpeditionLocationData location)
        {
            if (location == null)
                return false;
            StationSystemDefinition drone = GetDefinition(StationSystemType.Drone);
            return GetStat(
                    StationSystemType.Drone,
                    drone?.ObjectId,
                    StationObjectStat.TravelRange) >=
                location.RequiredDroneTravelRange;
        }

        public bool CanAntennaReach(Expeditions.ExpeditionLocationData location)
        {
            if (location == null)
                return false;
            StationSystemDefinition antenna =
                GetDefinition(StationSystemType.Antenna);
            return GetStat(
                    StationSystemType.Antenna,
                    antenna?.ObjectId,
                    StationObjectStat.ScanRange) >=
                location.RequiredAntennaScanRange;
        }

        public IEnumerable<KeyValuePair<StationSystemType, bool>>
            RequestedStates => requestedStates;

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
                        pair.Value.RequestedActive,
                        GetInstalledParts(pair.Value.SystemType, pair.Key));
                }
            }
        }

        public void RegisterObject(
            StationSystemType type,
            string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return;
            StationSystemDefinition definition = GetDefinition(type, objectId);
            EnsureObjectState(
                type,
                objectId,
                definition?.InitiallyActive ?? true);
        }

        public void Restore(
            IEnumerable<KeyValuePair<StationSystemType, bool>> states,
            IEnumerable<StationObjectSystemState> restoredObjects = null)
        {
            InitializeDefaults();
            if (states != null)
            {
                foreach (KeyValuePair<StationSystemType, bool> pair in states)
                {
                    StationSystemDefinition definition = GetDefinition(pair.Key);
                    if (definition != null &&
                        string.IsNullOrWhiteSpace(definition.ObjectId))
                    {
                        requestedStates[pair.Key] = pair.Value;
                    }
                }
            }

            if (restoredObjects != null)
            {
                foreach (StationObjectSystemState restored in restoredObjects)
                {
                    StationSystemDefinition definition = GetDefinition(
                        restored.SystemType,
                        restored.ObjectId);
                    if (definition == null)
                        continue;

                    if (string.IsNullOrWhiteSpace(restored.ObjectId))
                    {
                        RestoreInstalledParts(
                            restored.SystemType,
                            string.Empty,
                            restored.InstalledParts);
                        continue;
                    }

                    objectStates[NormalizeObjectId(restored.ObjectId)] =
                        new ObjectRuntimeState
                        {
                            SystemType = restored.SystemType,
                            RequestedActive = restored.RequestedActive
                        };
                    RestoreInstalledParts(
                        restored.SystemType,
                        restored.ObjectId,
                        restored.InstalledParts);
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
            requestedStates.Clear();
            objectStates.Clear();
            installedParts.Clear();
            foreach (StationSystemDefinition definition in Config.StationObjects)
            {
                if (definition == null)
                    continue;
                if (string.IsNullOrWhiteSpace(definition.ObjectId))
                {
                    requestedStates[definition.SystemType] =
                        definition.InitiallyActive;
                }
                else
                {
                    objectStates[NormalizeObjectId(definition.ObjectId)] =
                        new ObjectRuntimeState
                        {
                            SystemType = definition.SystemType,
                            RequestedActive = definition.InitiallyActive
                        };
                }
            }
        }

        private ObjectRuntimeState EnsureObjectState(
            StationSystemType type,
            string objectId,
            bool initiallyActive)
        {
            string id = NormalizeObjectId(objectId);
            if (objectStates.TryGetValue(id, out ObjectRuntimeState existing))
                return existing;
            StationSystemDefinition definition = GetDefinition(type, id);
            ObjectRuntimeState created = new ObjectRuntimeState
            {
                SystemType = type,
                RequestedActive = definition?.InitiallyActive ?? initiallyActive
            };
            objectStates[id] = created;
            return created;
        }

        private void SetRuntimeActive(
            StationSystemType type,
            string objectId,
            bool active,
            bool initiallyActive)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                requestedStates[type] = active;
            else
                EnsureObjectState(type, objectId, initiallyActive)
                    .RequestedActive = active;
        }

        private void RestoreInstalledParts(
            StationSystemType type,
            string objectId,
            IEnumerable<StationInstalledPartState> restored)
        {
            if (restored == null)
                return;
            StationSystemDefinition definition = GetDefinition(type, objectId);
            if (definition == null)
                return;

            var parts = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (StationInstalledPartState part in restored)
            {
                string slotId = NormalizeSlotId(part.SlotId);
                string itemId = part.ItemId?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(slotId) &&
                    !string.IsNullOrEmpty(itemId) &&
                    definition.FindSlot(slotId) != null)
                {
                    parts[slotId] = itemId;
                }
            }
            if (parts.Count > 0)
                installedParts[GetPartStateKey(type, objectId)] = parts;
        }

        private void SynchronizeQuestStates()
        {
            QuestController quests = QuestController.Instance;
            if (quests == null)
                return;

            foreach (StationSystemDefinition definition in Config.StationObjects)
            {
                if (definition == null)
                    continue;
                string objectId = definition.ObjectId;
                string targetId = ResolveQuestTargetId(
                    definition.SystemType,
                    objectId);
                bool active = IsRequestedActive(
                    definition.SystemType,
                    objectId);
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
                    value: GetInstalledPartCount(
                        definition.SystemType,
                        objectId));
            }
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

            MaintainableObject[] candidates =
                FindObjectsByType<MaintainableObject>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            foreach (MaintainableObject candidate in candidates)
            {
                if (candidate != null && candidate.isActiveAndEnabled &&
                    candidate.Role == fallbackRole)
                {
                    return candidate;
                }
            }
            return null;
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

        private static string ResolveQuestTargetId(
            StationSystemType type,
            string objectId)
        {
            return string.IsNullOrWhiteSpace(objectId)
                ? type.ToString()
                : objectId;
        }

        private static string ResolveObjectId(
            StationSystemDefinition definition,
            string objectId)
        {
            return string.IsNullOrWhiteSpace(objectId)
                ? definition?.ObjectId ?? string.Empty
                : objectId.Trim();
        }

        private static string GetPartStateKey(
            StationSystemType type,
            string objectId)
        {
            return $"{(int)type}:{NormalizeObjectId(objectId)}";
        }

        private static string NormalizeObjectId(string objectId)
        {
            return objectId?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static string NormalizeSlotId(string slotId)
        {
            return slotId?.Trim().ToLowerInvariant() ?? string.Empty;
        }

        private static bool Succeed(out string reason)
        {
            reason = string.Empty;
            return true;
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
