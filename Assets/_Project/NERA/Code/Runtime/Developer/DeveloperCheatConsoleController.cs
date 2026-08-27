using System;
using System.Collections.Generic;
using NERA.Antenna;
using NERA.Core;
using NERA.Drone;
using NERA.Energy;
using NERA.Enemies;
using NERA.Expeditions;
using NERA.Inventory;
using NERA.Items;
using NERA.Locations;
using NERA.Maintenance;
using NERA.Player;
using NERA.Station;
using NERA.Terminal;
using NERA.World;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Development
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class DeveloperCheatConsoleController : MonoBehaviour
    {
        private const string StationSceneName = "Player_Station";
        private const string StationSpawnPointId = "Station_Start";

        [Header("Build Availability")]
        [SerializeField] private bool enabledInEditor = true;
        [SerializeField] private bool enabledInDevelopmentBuild = true;
        [SerializeField] private bool enabledInReleaseBuild;

        [Header("Window")]
        [SerializeField] private GameObject windowRoot;
        [SerializeField] private Button homeButton;
        [SerializeField] private Button cleanButton;
        [SerializeField] private Button clearWeatherButton;
        [SerializeField] private Button sandstormButton;
        [SerializeField] private Button contaminateButton;

        [Header("Locations")]
        [SerializeField] private Button[] expeditionButtons = Array.Empty<Button>();
        [SerializeField] private Button[] signalButtons = Array.Empty<Button>();

        [Header("Station Upgrades")]
        [SerializeField] private Button turretOneButton;
        [SerializeField] private Button turretTwoButton;
        [SerializeField] private Button droneButton;
        [SerializeField] private Button antennaButton;
        [SerializeField] private Button batteryButton;
        [SerializeField] private Button solarPanelButton;

        [Header("Station Power Controls")]
        [SerializeField] private Button[] stationEnableButtons =
            Array.Empty<Button>();
        [SerializeField] private Button[] stationDisableButtons =
            Array.Empty<Button>();

        [Header("IO")]
        [SerializeField] private Button spawnIoButton;
        [SerializeField] private Button killIoButton;
        [SerializeField] private GameObject ioEnemyPrefab;
        [SerializeField, Min(0.1f)] private float ioSpawnDistance = 5f;
        [SerializeField, Min(0f)] private float ioHoverHeight = 1.6f;

        [Header("Inventory")]
        [SerializeField] private Button[] itemButtons = Array.Empty<Button>();
        [SerializeField] private ItemData[] inventoryItems = Array.Empty<ItemData>();

        private ParkourPlayerBridge player;
        private CursorLockMode cursorLockBeforeOpen;
        private bool cursorVisibleBeforeOpen;

        private static readonly StationControlTarget[] StationControlTargets =
        {
            new StationControlTarget(
                "Turret 1",
                StationSystemType.Turret,
                "station_turret_01"),
            new StationControlTarget(
                "Turret 2",
                StationSystemType.Turret,
                "station_turret_02"),
            new StationControlTarget(
                "Drone",
                StationSystemType.Drone,
                "station_drone"),
            new StationControlTarget(
                "Antenna",
                StationSystemType.Antenna,
                "station_antenna"),
            new StationControlTarget(
                "Battery",
                StationSystemType.Battery,
                "station_battery"),
            new StationControlTarget(
                "Solar panel",
                StationSystemType.SolarPanel,
                "station_solar_01"),
            new StationControlTarget(
                "Terminal",
                StationSystemType.Terminal,
                string.Empty)
        };

        public static DeveloperCheatConsoleController Instance { get; private set; }
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (!IsAllowedInCurrentBuild())
            {
                Destroy(gameObject);
                return;
            }

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            BindButtons();
            if (windowRoot != null)
                windowRoot.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                SetOpen(!IsOpen);
                return;
            }

            if (IsOpen && Input.GetKeyDown(KeyCode.Escape))
                SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            if (IsOpen == open)
                return;

            IsOpen = open;
            if (open)
            {
                cursorLockBeforeOpen = Cursor.lockState;
                cursorVisibleBeforeOpen = Cursor.visible;
                player = FindFirstObjectByType<ParkourPlayerBridge>();
                player?.SetInputEnabled(this, false);
                if (windowRoot != null)
                    windowRoot.SetActive(true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (windowRoot != null)
                windowRoot.SetActive(false);
            player?.SetInputEnabled(this, true);
            player = null;
            Cursor.lockState = cursorLockBeforeOpen;
            Cursor.visible = cursorVisibleBeforeOpen;
        }

        public void GoHome()
        {
            BootInitializer runtime = BootInitializer.Instance;
            if (runtime == null)
            {
                LogFailure("Home", "BootInitializer is unavailable.");
                return;
            }

            SetOpen(false);
            bool loaded = runtime.LoadGameplayScene(
                StationSceneName,
                StationSpawnPointId);
            LogResult("Home", loaded, "station scene loaded");
        }

        public void UnlockExpedition(int ordinal)
        {
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            ExpeditionLocationData location = FindLocation(
                discovery,
                LocationType.Expedition,
                ordinal);
            if (discovery == null || location == null)
            {
                LogFailure("Expedition", $"location #{ordinal} is unavailable.");
                return;
            }

            bool changed = discovery.Discover(location);
            bool available = changed || discovery.IsDiscovered(location);
            LogResult(
                $"Expedition #{ordinal}",
                available,
                changed ? "unlocked" : "already unlocked");
        }

        public void RevealSignal(int ordinal)
        {
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            ExpeditionLocationData signal = FindLocation(
                discovery,
                LocationType.UnknownSignal,
                ordinal);
            AntennaController antenna = AntennaController.Instance;
            if (antenna == null || signal == null)
            {
                LogFailure("Signal", $"signal #{ordinal} is unavailable.");
                return;
            }

            bool revealed = antenna.ForceRevealSignalForDebug(signal);
            LogResult(
                $"Signal #{ordinal}",
                revealed,
                revealed
                    ? "revealed on an unlocked expedition sector"
                    : "requires at least one unlocked expedition sector");
        }

        public void SetClearWeather()
        {
            StationWeatherController weather = StationWeatherController.Instance;
            if (weather == null)
            {
                LogFailure("Weather", "StationWeatherController is unavailable.");
                return;
            }

            weather.SetWeather(StationWeather.Clear);
            LogResult("Weather", true, "clear");
        }

        public void StartSandstorm()
        {
            StationWeatherController weather = StationWeatherController.Instance;
            if (weather == null)
            {
                LogFailure("Weather", "StationWeatherController is unavailable.");
                return;
            }

            weather.SetWeather(StationWeather.Sandstorm);
            LogResult("Weather", true, "sandstorm");
        }

        public void CleanAllObjects()
        {
            SetAllObjectConditions(1f, "cleaned");
        }

        public void ContaminateAllObjects()
        {
            SetAllObjectConditions(
                0f,
                "contaminated",
                skipAbsentDrone: true);
        }

        public void FullyUpgradeTurretOne() => FullyUpgrade(
            StationSystemType.Turret,
            "station_turret_01");

        public void FullyUpgradeTurretTwo() => FullyUpgrade(
            StationSystemType.Turret,
            "station_turret_02");

        public void FullyUpgradeDrone() => FullyUpgrade(
            StationSystemType.Drone,
            "station_drone");

        public void FullyUpgradeAntenna() => FullyUpgrade(
            StationSystemType.Antenna,
            "station_antenna");

        public void FullyUpgradeBattery() => FullyUpgrade(
            StationSystemType.Battery,
            "station_battery");

        public void FullyUpgradeSolarPanel() => FullyUpgrade(
            StationSystemType.SolarPanel,
            "station_solar_01");

        public void SpawnIo()
        {
            if (ioEnemyPrefab == null)
            {
                LogFailure("Spawn IO", "enemy prefab is not configured.");
                return;
            }

            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
            {
                LogFailure("Spawn IO", "player is unavailable.");
                return;
            }

            Transform playerTransform = playerObject.transform;
            Vector3 forward = playerTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 spawnPosition =
                playerTransform.position + forward * ioSpawnDistance;
            if (Physics.Raycast(
                    spawnPosition + Vector3.up * 10f,
                    Vector3.down,
                    out RaycastHit hit,
                    30f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
            {
                spawnPosition.y = hit.point.y + ioHoverHeight;
            }
            else
            {
                spawnPosition.y = playerTransform.position.y + ioHoverHeight;
            }

            Vector3 lookDirection = playerTransform.position - spawnPosition;
            lookDirection.y = 0f;
            Quaternion rotation = lookDirection.sqrMagnitude > 0.01f
                ? Quaternion.LookRotation(lookDirection.normalized)
                : Quaternion.identity;
            GameObject enemy = Instantiate(
                ioEnemyPrefab,
                spawnPosition,
                rotation);
            enemy.name = $"Debug_{ioEnemyPrefab.name}";
            LogResult("Spawn IO", enemy != null, "one enemy spawned");
        }

        public void KillAllIo()
        {
            var enemies = new List<IOEnemyController>(
                IOEnemyController.ActiveEnemies);
            int killed = 0;
            foreach (IOEnemyController enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive)
                    continue;

                enemy.TakeDamage(float.MaxValue, gameObject);
                killed++;
            }

            LogResult("Kill IO", true, $"{killed} enemies killed");
        }

        public void GiveItem(int itemIndex)
        {
            if (itemIndex < 0 || itemIndex >= inventoryItems.Length ||
                inventoryItems[itemIndex] == null)
            {
                LogFailure("Give item", $"item index {itemIndex} is invalid.");
                return;
            }

            PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();
            ItemData item = inventoryItems[itemIndex];
            bool added = inventory != null && inventory.AddItem(item);
            LogResult(
                "Give item",
                added,
                added
                    ? $"{item.ItemId} +1"
                    : $"no free inventory slot for {item.ItemId}");
        }

        private bool IsAllowedInCurrentBuild()
        {
            if (Application.isEditor)
                return enabledInEditor;
            if (Debug.isDebugBuild)
                return enabledInDevelopmentBuild;
            return enabledInReleaseBuild;
        }

        private void BindButtons()
        {
            Bind(homeButton, GoHome);
            Bind(cleanButton, CleanAllObjects);
            Bind(clearWeatherButton, SetClearWeather);
            Bind(sandstormButton, StartSandstorm);
            Bind(contaminateButton, ContaminateAllObjects);

            BindIndexed(expeditionButtons, index => UnlockExpedition(index + 1));
            BindIndexed(signalButtons, index => RevealSignal(index + 1));

            Bind(turretOneButton, FullyUpgradeTurretOne);
            Bind(turretTwoButton, FullyUpgradeTurretTwo);
            Bind(droneButton, FullyUpgradeDrone);
            Bind(antennaButton, FullyUpgradeAntenna);
            Bind(batteryButton, FullyUpgradeBattery);
            Bind(solarPanelButton, FullyUpgradeSolarPanel);

            BindIndexed(
                stationEnableButtons,
                index => SetStationControlActive(index, true));
            BindIndexed(
                stationDisableButtons,
                index => SetStationControlActive(index, false));

            Bind(spawnIoButton, SpawnIo);
            Bind(killIoButton, KillAllIo);
            BindIndexed(itemButtons, GiveItem);
        }

        private static void SetStationControlActive(int index, bool active)
        {
            if (index < 0 || index >= StationControlTargets.Length)
            {
                LogFailure("Station power", $"control index {index} is invalid.");
                return;
            }

            StationSystemsController systems = StationSystemsController.Instance;
            StationControlTarget target = StationControlTargets[index];
            bool changed = systems != null &&
                systems.ForceSetRequestedActiveForDebug(
                    target.SystemType,
                    active,
                    target.ObjectId);
            if (!changed)
            {
                LogFailure(
                    "Station power",
                    $"{target.Label} is unavailable in the current scene.");
                return;
            }

            if (target.SystemType == StationSystemType.Battery)
            {
                StationPowerController.Instance?.SetState(
                    active
                        ? StationPowerState.Online
                        : StationPowerState.Offline);
            }
            else if (target.SystemType == StationSystemType.Terminal && !active)
            {
                TerminalUIScreen.Instance?.Close();
            }

            if (target.SystemType == StationSystemType.Antenna)
                AntennaController.Instance?.RefreshAvailability();
            else if (target.SystemType == StationSystemType.Drone)
                DroneScanController.Instance?.RefreshAvailability();

            LogResult(
                "Station power",
                true,
                $"{target.Label}: {(active ? "enabled" : "disabled")}");
        }

        private static void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }

        private static void BindIndexed(
            IReadOnlyList<Button> buttons,
            Action<int> action)
        {
            if (buttons == null || action == null)
                return;

            for (int index = 0; index < buttons.Count; index++)
            {
                Button button = buttons[index];
                if (button == null)
                    continue;
                int capturedIndex = index;
                button.onClick.AddListener(() => action(capturedIndex));
            }
        }

        private static ExpeditionLocationData FindLocation(
            ExpeditionDiscoveryController discovery,
            LocationType locationType,
            int ordinal)
        {
            if (discovery == null || ordinal <= 0)
                return null;

            int current = 0;
            foreach (ExpeditionLocationData location in discovery.KnownLocations)
            {
                if (location == null || location.LocationType != locationType)
                    continue;

                current++;
                if (current == ordinal)
                    return location;
            }
            return null;
        }

        private static void SetAllObjectConditions(
            float condition,
            string result,
            bool skipAbsentDrone = false)
        {
            var objects = new List<MaintainableObject>(
                MaintainableObject.ActiveObjects);
            int changed = 0;
            int skipped = 0;
            foreach (MaintainableObject maintainable in objects)
            {
                if (maintainable == null)
                    continue;
                if (skipAbsentDrone &&
                    maintainable.Role == MaintenanceRole.Drone &&
                    DroneScanController.Instance?.IsAtStation == false)
                {
                    skipped++;
                    continue;
                }

                maintainable.SetCondition(condition);
                changed++;
            }

            string skippedResult = skipped > 0
                ? $", {skipped} absent drone object skipped"
                : string.Empty;
            LogResult(
                "Maintenance",
                true,
                $"{changed} objects {result}{skippedResult}");
        }

        private static void FullyUpgrade(
            StationSystemType systemType,
            string objectId)
        {
            StationSystemsController systems = StationSystemsController.Instance;
            ItemCatalogData catalog = Resources.Load<ItemCatalogData>(
                "ItemCatalog_Default");
            StationSystemDefinition definition = systems?.GetDefinition(
                systemType,
                objectId);
            if (systems == null || catalog == null || definition == null)
            {
                LogFailure(
                    "Full upgrade",
                    $"{systemType}/{objectId} is not configured.");
                return;
            }

            var requests = new List<StationPartInstallRequest>();
            foreach (StationObjectSlotDefinition slot in definition.Slots)
            {
                if (slot == null ||
                    !string.IsNullOrEmpty(systems.GetInstalledPartItemId(
                        systemType,
                        objectId,
                        slot.SlotId)))
                {
                    continue;
                }

                ItemData part = FindCompatiblePart(
                    catalog,
                    systemType,
                    objectId,
                    slot.SlotId);
                if (part != null)
                {
                    requests.Add(new StationPartInstallRequest(
                        slot.SlotId,
                        part));
                }
            }

            if (requests.Count == 0)
            {
                LogResult("Full upgrade", true, $"{objectId} already upgraded");
                return;
            }

            bool installed = systems.TryInstallParts(
                systemType,
                objectId,
                requests,
                out string reason);
            LogResult(
                "Full upgrade",
                installed,
                installed ? $"{objectId}: {requests.Count} parts installed" : reason);
        }

        private static ItemData FindCompatiblePart(
            ItemCatalogData catalog,
            StationSystemType systemType,
            string objectId,
            string slotId)
        {
            foreach (ItemData item in catalog.Items)
            {
                if (item != null && item.FindEngineeringCompatibility(
                        systemType,
                        objectId,
                        slotId) != null)
                {
                    return item;
                }
            }
            return null;
        }

        private static void LogResult(
            string command,
            bool success,
            string result)
        {
            if (success)
                Debug.Log($"Developer cheats: {command} — {result}.");
            else
                LogFailure(command, result);
        }

        private static void LogFailure(string command, string reason)
        {
            Debug.LogWarning($"Developer cheats: {command} failed — {reason}");
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                player?.SetInputEnabled(this, true);
                Cursor.lockState = cursorLockBeforeOpen;
                Cursor.visible = cursorVisibleBeforeOpen;
            }

            if (Instance == this)
                Instance = null;
        }

        private sealed class StationControlTarget
        {
            public StationControlTarget(
                string label,
                StationSystemType systemType,
                string objectId)
            {
                Label = label;
                SystemType = systemType;
                ObjectId = objectId;
            }

            public string Label { get; }
            public StationSystemType SystemType { get; }
            public string ObjectId { get; }
        }
    }
}
