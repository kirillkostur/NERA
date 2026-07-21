using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NeraInteractionMode = NERA.Interaction.InteractionMode;
using NERA.Antenna;
using NERA.Drone;
using NERA.Combat;
using NERA.Energy;
using NERA.Enemies;
using NERA.Expeditions;
using NERA.Inventory;
using NERA.Items;
using NERA.Library;
using NERA.Locations;
using NERA.Maintenance;
using NERA.Research;
using NERA.Station;

namespace NERA.Tests
{
    public sealed class Sprint01FoundationTests
    {
        private static readonly string[] RequiredBuildScenes =
        {
            "Assets/_Project/NERA/Scenes/Boot/Boot.unity",
            "Assets/_Project/NERA/Scenes/Station/Player_Station.unity",
            "Assets/_Project/NERA/Scenes/Expeditions/Expedition_01.unity"
        };

        [Test]
        public void RequiredScenesAreEnabledInBuildSettings()
        {
            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            foreach (string requiredScene in RequiredBuildScenes)
            {
                Assert.That(
                    enabledScenes,
                    Does.Contain(requiredScene),
                    $"Required scene is missing or disabled: {requiredScene}"
                );
            }
        }

        [Test]
        public void InteractionModesContainPressAndHold()
        {
            Assert.That(
                NeraInteractionMode.Press,
                Is.Not.EqualTo(NeraInteractionMode.Hold)
            );
        }
    }

    public sealed class Sprint02StationPowerTests
    {
        private GameObject root;
        private StationPowerController power;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Test_StationPower");
            power = root.AddComponent<StationPowerController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void RestorePowerTransitionsOfflineToOnlineAndRaisesEvent()
        {
            StationPowerState? observedState = null;
            power.StateChanged += state => observedState = state;

            bool restored = power.RestorePower();

            Assert.That(restored, Is.True);
            Assert.That(power.State, Is.EqualTo(StationPowerState.Online));
            Assert.That(power.IsPowered, Is.True);
            Assert.That(observedState, Is.EqualTo(StationPowerState.Online));
        }

        [Test]
        public void RestorePowerIsIdempotent()
        {
            Assert.That(power.RestorePower(), Is.True);
            Assert.That(power.RestorePower(), Is.False);
            Assert.That(power.State, Is.EqualTo(StationPowerState.Online));
        }
    }

    public sealed class StationEnergySystemTests
    {
        private GameObject root;
        private StationEnvironmentController environment;
        private EnergySystemController energy;

        [SetUp]
        public void SetUp()
        {
            ClearSingleton(typeof(StationEnvironmentController));
            ClearSingleton(typeof(EnergySystemController));
            root = new GameObject("Test_EnergySystem");
            environment = root.AddComponent<StationEnvironmentController>();
            energy = root.AddComponent<EnergySystemController>();
            SetSingleton(typeof(StationEnvironmentController), environment);
            SetSingleton(typeof(EnergySystemController), energy);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            ClearSingleton(typeof(StationEnvironmentController));
            ClearSingleton(typeof(EnergySystemController));
        }

        [Test]
        public void MultipleBatteriesShareCapacityAndInitialCharge()
        {
            energy.RegisterBattery("battery_01", 1000f, 1000f);
            energy.RegisterBattery("battery_02", 1000f, 1000f);

            Assert.That(energy.TotalCapacity, Is.EqualTo(2000f));
            Assert.That(energy.CurrentEnergy, Is.EqualTo(2000f));
        }

        [Test]
        public void ReloadingStationDoesNotDuplicateBatteryOrSolarPanel()
        {
            energy.RegisterBattery("station/battery_01", 1000f, 1000f);
            energy.RegisterSolarPanel("station/panel_01", 1f);

            energy.RegisterBattery("station/battery_01", 1000f, 1000f);
            energy.RegisterSolarPanel("station/panel_01", 1f);

            environment.SetTime(12f);
            environment.SetWeather(StationWeather.Clear);
            energy.AdvanceSimulation(1f);

            Assert.That(energy.TotalCapacity, Is.EqualTo(1000f));
            Assert.That(
                energy.CurrentGeneration,
                Is.EqualTo(energy.Config.ClearDayGeneration)
            );
        }

        [Test]
        public void SolarPanelGeneratesByDayButNotAtNight()
        {
            energy.RegisterBattery("battery_01", 1000f, 0f);
            energy.RegisterSolarPanel("panel_01", 1f);
            environment.SetWeather(StationWeather.Clear);

            environment.SetTime(12f);
            energy.AdvanceSimulation(1f);
            Assert.That(
                energy.CurrentGeneration,
                Is.EqualTo(energy.Config.ClearDayGeneration)
            );

            environment.SetTime(0f);
            energy.AdvanceSimulation(1f);
            Assert.That(energy.CurrentGeneration, Is.Zero);
        }

        [Test]
        public void EmergencyReserveDisconnectsNonEssentialConsumers()
        {
            energy.RegisterBattery("battery_01", 1000f, 1000f);
            energy.RegisterConsumer("laboratory", 4f, true);
            energy.SetConsumerActive("laboratory", true);
            energy.RestoreState(200f, true);
            energy.AdvanceSimulation(0.1f);

            Assert.That(energy.State, Is.EqualTo(EnergyState.Emergency));
            Assert.That(energy.IsConsumerPowered("laboratory"), Is.False);
            Assert.That(energy.CurrentConsumption, Is.Zero);
        }

        [Test]
        public void LaboratoryCannotReceivePowerBeforeGridStarts()
        {
            energy.RegisterBattery("battery_01", 1000f, 1000f);
            energy.RegisterConsumer("laboratory", 4f, true);

            Assert.That(energy.CanPowerConsumer("laboratory"), Is.False);

            energy.SetGridEnabled(true);

            Assert.That(energy.CanPowerConsumer("laboratory"), Is.True);
        }

        private static void ClearSingleton(System.Type controllerType)
        {
            SetSingleton(controllerType, null);
        }

        private static void SetSingleton(
            System.Type controllerType,
            object value
        )
        {
            PropertyInfo instanceProperty = controllerType.GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public
            );
            MethodInfo setter = instanceProperty?.GetSetMethod(true);
            setter?.Invoke(null, new[] { value });
        }
    }

    public sealed class Sprint03DroneStateTests
    {
        private GameObject root;
        private StationPowerController power;
        private ExpeditionDiscoveryController discovery;
        private DroneScanController drone;
        private ExpeditionLocationData location;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Test_DroneState");
            power = root.AddComponent<StationPowerController>();
            discovery = root.AddComponent<ExpeditionDiscoveryController>();
            drone = root.AddComponent<DroneScanController>();
            location = ScriptableObject.CreateInstance<ExpeditionLocationData>();
            SetPrivateField(drone, "stationPower", power);
            SetPrivateField(drone, "discovery", discovery);

            SerializedObject locationObject = new SerializedObject(location);
            locationObject.FindProperty("locationId").stringValue = "Test_Expedition";
            locationObject.FindProperty("droneScanDuration").floatValue = 2f;
            locationObject.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(location);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void DroneUnlocksWhenStationPowerComesOnline()
        {
            Assert.That(drone.State, Is.EqualTo(DroneState.Locked));

            power.RestorePower();
            drone.RefreshAvailability();

            Assert.That(drone.State, Is.EqualTo(DroneState.Ready));
        }

        [Test]
        public void DroneCannotLaunchWithoutConfiguredLocation()
        {
            power.RestorePower();
            drone.RefreshAvailability();

            Assert.That(drone.LaunchScan(), Is.False);
            Assert.That(drone.State, Is.EqualTo(DroneState.Ready));
        }

        [Test]
        public void DroneScanDiscoversConfiguredLocation()
        {
            power.RestorePower();
            drone.RefreshAvailability();

            Assert.That(drone.LaunchScan(location), Is.True);
            Assert.That(drone.State, Is.EqualTo(DroneState.Scanning));

            drone.AdvanceScan(1f);
            Assert.That(drone.ScanProgress, Is.EqualTo(0.5f).Within(0.001f));

            drone.AdvanceScan(1f);
            Assert.That(drone.State, Is.EqualTo(DroneState.ScanComplete));
            Assert.That(discovery.IsDiscovered(location), Is.True);
        }

        [Test]
        public void DiscoveryCanFilterLocationsBySourceAndType()
        {
            ExpeditionLocationData expedition =
                CreateLocation("expedition_02", LocationType.Expedition, DiscoverySource.Drone);
            ExpeditionLocationData signal =
                CreateLocation("unknown_signal_01", LocationType.UnknownSignal, DiscoverySource.Antenna);
            AddKnownLocation(expedition);
            AddKnownLocation(signal);

            Assert.That(
                discovery.GetKnownLocations(DiscoverySource.Drone),
                Is.EquivalentTo(new[] { expedition })
            );
            Assert.That(
                discovery.GetKnownLocations(DiscoverySource.Antenna),
                Is.EquivalentTo(new[] { signal })
            );
            Assert.That(
                discovery.GetKnownLocations(LocationType.UnknownSignal),
                Is.EquivalentTo(new[] { signal })
            );

            Object.DestroyImmediate(expedition);
            Object.DestroyImmediate(signal);
        }

        [Test]
        public void NextUndiscoveredLocationSkipsAlreadyDiscoveredTargets()
        {
            ExpeditionLocationData first =
                CreateLocation("expedition_02", LocationType.Expedition, DiscoverySource.Drone);
            ExpeditionLocationData second =
                CreateLocation("expedition_03", LocationType.Expedition, DiscoverySource.Drone);
            AddKnownLocation(first);
            AddKnownLocation(second);

            Assert.That(
                discovery.TryGetNextUndiscovered(
                    DiscoverySource.Drone,
                    out ExpeditionLocationData next
                ),
                Is.True
            );
            Assert.That(next, Is.EqualTo(first));

            discovery.Discover(first);

            Assert.That(
                discovery.TryGetNextUndiscovered(
                    DiscoverySource.Drone,
                    out next
                ),
                Is.True
            );
            Assert.That(next, Is.EqualTo(second));

            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        private static void SetPrivateField(
            DroneScanController target,
            string fieldName,
            object value
        )
        {
            FieldInfo field = typeof(DroneScanController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private void AddKnownLocation(ExpeditionLocationData knownLocation)
        {
            SerializedObject serializedDiscovery = new SerializedObject(discovery);
            SerializedProperty locations =
                serializedDiscovery.FindProperty("knownLocations");
            int index = locations.arraySize;
            locations.InsertArrayElementAtIndex(index);
            locations.GetArrayElementAtIndex(index).objectReferenceValue =
                knownLocation;
            serializedDiscovery.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ExpeditionLocationData CreateLocation(
            string locationId,
            LocationType locationType,
            DiscoverySource discoverySource
        )
        {
            ExpeditionLocationData data =
                ScriptableObject.CreateInstance<ExpeditionLocationData>();
            SerializedObject serialized = new SerializedObject(data);
            serialized.FindProperty("locationId").stringValue = locationId;
            serialized.FindProperty("locationType").enumValueIndex =
                (int)locationType;
            serialized.FindProperty("discoverySource").enumValueIndex =
                (int)discoverySource;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }
    }

    public sealed class Sprint04IOEnemyTests
    {
        [Test]
        public void PlayerHealthReceivesEnergyDamage()
        {
            GameObject player = new GameObject("Test_Player");
            PlayerHealth health = player.AddComponent<PlayerHealth>();
            health.RestoreFullHealth();

            health.TakeDamage(25f, null);

            Assert.That(health.IsAlive, Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(75f));
            Object.DestroyImmediate(player);
        }

        [Test]
        public void BlueIOImplementsDamageableContract()
        {
            GameObject enemy = new GameObject("Test_BlueIO");
            IOEnemyController controller =
                enemy.AddComponent<IOEnemyController>();

            Assert.That(controller, Is.InstanceOf<IDamageable>());
            Assert.That(controller.IsAlive, Is.True);
            Object.DestroyImmediate(enemy);
        }
    }

    public sealed class AntennaControllerTests
    {
        private GameObject root;
        private StationEnvironmentController environment;
        private EnergySystemController energy;
        private StationPowerController power;
        private ExpeditionDiscoveryController discovery;
        private MaintainableObject maintenance;
        private AntennaController antenna;
        private ExpeditionLocationData expedition;
        private ExpeditionLocationData signal;

        [SetUp]
        public void SetUp()
        {
            SetSingleton(typeof(StationEnvironmentController), null);
            SetSingleton(typeof(EnergySystemController), null);
            SetSingleton(typeof(StationPowerController), null);
            SetSingleton(typeof(ExpeditionDiscoveryController), null);
            SetSingleton(typeof(AntennaController), null);

            root = new GameObject("Test_AntennaSystems");
            environment = root.AddComponent<StationEnvironmentController>();
            energy = root.AddComponent<EnergySystemController>();
            power = root.AddComponent<StationPowerController>();
            discovery = root.AddComponent<ExpeditionDiscoveryController>();
            maintenance = root.AddComponent<MaintainableObject>();
            SerializedObject serializedMaintenance = new SerializedObject(maintenance);
            serializedMaintenance.FindProperty("role").enumValueIndex =
                (int)MaintenanceRole.Antenna;
            serializedMaintenance.ApplyModifiedPropertiesWithoutUndo();
            antenna = root.AddComponent<AntennaController>();
            SerializedObject serializedAntenna = new SerializedObject(antenna);
            serializedAntenna.FindProperty("signalDiscoveryChance").floatValue = 1f;
            serializedAntenna.ApplyModifiedPropertiesWithoutUndo();

            SetSingleton(typeof(StationEnvironmentController), environment);
            SetSingleton(typeof(EnergySystemController), energy);
            SetSingleton(typeof(StationPowerController), power);
            SetSingleton(typeof(ExpeditionDiscoveryController), discovery);
            SetSingleton(typeof(AntennaController), antenna);

            energy.RegisterBattery("battery_01", 1000f, 1000f);
            energy.SetGridEnabled(true);
            power.RestorePower();

            expedition = CreateLocation(
                "expedition_01",
                LocationType.Expedition,
                DiscoverySource.Drone
            );
            signal = CreateLocation(
                "unknown_signal_01",
                LocationType.UnknownSignal,
                DiscoverySource.Antenna
            );
            AddKnownLocation(expedition);
            AddKnownLocation(signal);
            discovery.Discover(expedition);
            antenna.RefreshAvailability();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(signal);
            Object.DestroyImmediate(expedition);
            Object.DestroyImmediate(root);
            SetSingleton(typeof(StationEnvironmentController), null);
            SetSingleton(typeof(EnergySystemController), null);
            SetSingleton(typeof(StationPowerController), null);
            SetSingleton(typeof(ExpeditionDiscoveryController), null);
            SetSingleton(typeof(AntennaController), null);
        }

        [Test]
        public void AntennaCalibrationDiscoversUnknownSignal()
        {
            Assert.That(power.IsPowered, Is.True, "Station power must be online for calibration.");
            Assert.That(discovery.IsDiscovered(expedition), Is.True, "Expedition sector must be open.");
            Assert.That(signal.DiscoverySource, Is.EqualTo(DiscoverySource.Antenna));
            Assert.That(antenna.IsOperational, Is.True, "Antenna must be operational.");
            Assert.That(
                energy.CanPowerConsumer("antenna_calibration"),
                Is.True,
                "Antenna energy consumer must be registered and powered."
            );
            Assert.That(antenna.CanCalibrate(signal), Is.True);

            Assert.That(antenna.StartCalibration(signal), Is.True);
            Assert.That(antenna.State, Is.EqualTo(AntennaState.Calibrating));

            antenna.AdvanceCalibration(antenna.CalibrationDuration);

            Assert.That(antenna.State, Is.EqualTo(AntennaState.SignalFound));
            Assert.That(antenna.ActiveSignal, Is.EqualTo(signal));
            Assert.That(antenna.ActiveSignalSectorIndex, Is.EqualTo(expedition.MapSectorIndex));
            Assert.That(discovery.IsDiscovered(signal), Is.False);
        }

        [Test]
        public void AntennaCannotCalibrateDroneLocations()
        {
            ExpeditionLocationData expedition = CreateLocation(
                "expedition_03",
                LocationType.Expedition,
                DiscoverySource.Drone
            );

            Assert.That(antenna.StartCalibration(expedition), Is.False);

            Object.DestroyImmediate(expedition);
        }

        [Test]
        public void MaintenanceConditionCanFaultAntennaAndRepairRestoresIt()
        {
            maintenance.SetCondition(0f);
            antenna.RefreshAvailability();

            Assert.That(antenna.State, Is.EqualTo(AntennaState.Faulted));
            Assert.That(antenna.CanCalibrate(signal), Is.False);

            Assert.That(antenna.Repair(), Is.True);
            Assert.That(antenna.Condition, Is.EqualTo(1f));
            Assert.That(antenna.CanCalibrate(signal), Is.True);
        }

        private void AddKnownLocation(ExpeditionLocationData knownLocation)
        {
            SerializedObject serializedDiscovery = new SerializedObject(discovery);
            SerializedProperty locations =
                serializedDiscovery.FindProperty("knownLocations");
            int index = locations.arraySize;
            locations.InsertArrayElementAtIndex(index);
            locations.GetArrayElementAtIndex(index).objectReferenceValue =
                knownLocation;
            serializedDiscovery.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ExpeditionLocationData CreateLocation(
            string locationId,
            LocationType locationType,
            DiscoverySource discoverySource
        )
        {
            ExpeditionLocationData data =
                ScriptableObject.CreateInstance<ExpeditionLocationData>();
            SerializedObject serialized = new SerializedObject(data);
            serialized.FindProperty("locationId").stringValue = locationId;
            serialized.FindProperty("locationType").enumValueIndex =
                (int)locationType;
            serialized.FindProperty("discoverySource").enumValueIndex =
                (int)discoverySource;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        private static void SetSingleton(System.Type controllerType, object value)
        {
            PropertyInfo instanceProperty = controllerType.GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public
            );
            instanceProperty?.GetSetMethod(true)?.Invoke(null, new[] { value });
        }
    }

    public sealed class Sprint05InventoryTests
    {
        private GameObject root;
        private PlayerInventory inventory;
        private readonly System.Collections.Generic.List<ItemData> createdItems = new System.Collections.Generic.List<ItemData>();
        private readonly System.Collections.Generic.List<ItemEnergyDefinition> createdEnergyDefinitions = new System.Collections.Generic.List<ItemEnergyDefinition>();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Test_PlayerInventory");
            inventory = root.AddComponent<PlayerInventory>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ItemData item in createdItems)
                Object.DestroyImmediate(item);
            foreach (ItemEnergyDefinition definition in createdEnergyDefinitions)
                Object.DestroyImmediate(definition);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ItemsRouteToTheirDedicatedSlotGroups()
        {
            ItemData engineering = CreateItem("engineering", ItemType.EngineeringPart);
            ItemData record = CreateItem("record", ItemType.Record);
            ItemData equipment = CreateItem("equipment", ItemType.Equipment);
            ItemData anomaly = CreateItem("anomaly", ItemType.Anomaly);

            Assert.That(inventory.AddItem(engineering), Is.True);
            Assert.That(inventory.AddItem(record), Is.True);
            Assert.That(inventory.AddItem(equipment), Is.True);
            Assert.That(inventory.AddItem(anomaly), Is.True);

            Assert.That(inventory.GetItem(InventorySlotGroup.Backpack, 0), Is.EqualTo(engineering));
            Assert.That(inventory.GetItem(InventorySlotGroup.Backpack, 1), Is.EqualTo(record));
            Assert.That(inventory.GetItem(InventorySlotGroup.QuickAccess, 1), Is.EqualTo(equipment));
            Assert.That(inventory.GetItem(InventorySlotGroup.Anomaly, 0), Is.EqualTo(anomaly));
        }

        [Test]
        public void EquipmentFillsActiveQuickSlotsBeforeAuxiliarySlots()
        {
            ItemData[] equipment = new ItemData[PlayerInventory.QuickAccessCapacity];
            for (int i = 0; i < equipment.Length; i++)
            {
                equipment[i] = CreateItem($"equipment_{i}", ItemType.Equipment);
                Assert.That(inventory.AddItem(equipment[i]), Is.True);
            }

            Assert.That(inventory.GetItem(InventorySlotGroup.QuickAccess, 1), Is.EqualTo(equipment[0]));
            Assert.That(inventory.GetItem(InventorySlotGroup.QuickAccess, 2), Is.EqualTo(equipment[1]));
            Assert.That(inventory.GetItem(InventorySlotGroup.QuickAccess, 3), Is.EqualTo(equipment[2]));
            Assert.That(inventory.GetItem(InventorySlotGroup.QuickAccess, 0), Is.EqualTo(equipment[3]));
            Assert.That(inventory.GetItem(InventorySlotGroup.QuickAccess, 4), Is.EqualTo(equipment[4]));
        }

        [Test]
        public void WorldItemPickupAddsItemInstanceToPlayerInventory()
        {
            ItemData item = CreateItem("world_part", ItemType.EngineeringPart);
            GameObject worldObject = new GameObject("Test_WorldItem");
            WorldItem worldItem = worldObject.AddComponent<WorldItem>();
            worldItem.Initialize(item);

            SerializedObject serializedWorldItem = new SerializedObject(worldItem);
            serializedWorldItem.FindProperty("destroyAfterPickup").boolValue = false;
            serializedWorldItem.ApplyModifiedPropertiesWithoutUndo();

            worldItem.CompleteInteraction(root);

            Assert.That(inventory.GetItem(InventorySlotGroup.Backpack, 0), Is.EqualTo(item));
            Assert.That(
                inventory.GetItemInstance(InventorySlotGroup.Backpack, 0)?.ItemData,
                Is.EqualTo(item)
            );
            Assert.That(worldObject.activeSelf, Is.False);

            Object.DestroyImmediate(worldObject);
        }

        [Test]
        public void InvalidSerializedInstancesDoNotOccupyEmptySlots()
        {
            ItemInstance invalidInstance = JsonUtility.FromJson<ItemInstance>("{}");
            inventory.RestoreInstanceSlots(
                new[] { invalidInstance, null },
                new ItemInstance[PlayerInventory.AnomalyCapacity],
                new ItemInstance[PlayerInventory.QuickAccessCapacity]
            );

            ItemData item = CreateItem("recovered_pickup", ItemType.EngineeringPart);

            Assert.That(inventory.AddItem(item), Is.True);
            Assert.That(inventory.GetItem(InventorySlotGroup.Backpack, 0), Is.EqualTo(item));
        }

        [Test]
        public void EqualItemsOccupySeparateBackpackSlots()
        {
            ItemData first = CreateItem("same_part", ItemType.EngineeringPart);
            ItemData second = CreateItem("same_part", ItemType.EngineeringPart);

            Assert.That(inventory.AddItem(first), Is.True);
            Assert.That(inventory.AddItem(second), Is.True);
            Assert.That(inventory.GetItem(InventorySlotGroup.Backpack, 0), Is.EqualTo(first));
            Assert.That(inventory.GetItem(InventorySlotGroup.Backpack, 1), Is.EqualTo(second));
            Assert.That(inventory.Count, Is.EqualTo(2));
        }

        [Test]
        public void BackpackCapacityComesFromInventoryConfig()
        {
            InventoryConfig config = ScriptableObject.CreateInstance<InventoryConfig>();
            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("backpackCapacity").intValue = 8;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            inventory.Configure(config);

            Assert.That(inventory.BackpackCapacity, Is.EqualTo(8));
            Assert.That(inventory.BackpackSlots.Count, Is.EqualTo(8));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void BackpackCapacityIsLimitedToAuthoredSpawnPoints()
        {
            InventoryConfig config = ScriptableObject.CreateInstance<InventoryConfig>();
            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("backpackCapacity").intValue = 20;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            inventory.Configure(config);

            Assert.That(
                inventory.BackpackCapacity,
                Is.EqualTo(InventoryConfig.MaxBackpackCapacity)
            );
            Assert.That(
                inventory.BackpackSlots.Count,
                Is.EqualTo(InventoryConfig.MaxBackpackCapacity)
            );

            Object.DestroyImmediate(config);
        }

        [Test]
        public void StructuredRestorePreservesEmptySlotPositions()
        {
            ItemData first = CreateItem("first", ItemType.EngineeringPart);
            ItemData third = CreateItem("third", ItemType.EngineeringPart);
            ItemData[] backpack =
            {
                first,
                null,
                third,
                null,
                null
            };

            inventory.RestoreSlots(
                backpack,
                new ItemData[PlayerInventory.AnomalyCapacity],
                new ItemData[PlayerInventory.QuickAccessCapacity]
            );

            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 0),
                Is.EqualTo(first)
            );
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 1),
                Is.Null
            );
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 2),
                Is.EqualTo(third)
            );
        }

        [Test]
        public void RemovingSelectedSlotDoesNotRemoveEqualItemFromAnotherSlot()
        {
            ItemData first = CreateItem("same_part", ItemType.EngineeringPart);
            ItemData second = CreateItem("same_part", ItemType.EngineeringPart);
            inventory.AddItem(first);
            inventory.AddItem(second);

            Assert.That(
                inventory.RemoveItemAt(
                    InventorySlotGroup.Backpack,
                    1,
                    out ItemData removed
                ),
                Is.True
            );
            Assert.That(removed, Is.EqualTo(second));
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 0),
                Is.EqualTo(first)
            );
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 1),
                Is.Null
            );
        }

        [Test]
        public void ItemsCanMoveToAnyValidInventorySlot()
        {
            ItemData first = CreateItem("first", ItemType.EngineeringPart);
            ItemData second = CreateItem("second", ItemType.EngineeringPart);
            inventory.AddItem(first);
            inventory.AddItem(second);

            Assert.That(
                inventory.TryMoveItem(
                    InventorySlotGroup.Backpack,
                    0,
                    InventorySlotGroup.Backpack,
                    4
                ),
                Is.True
            );
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 4),
                Is.EqualTo(first)
            );
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 0),
                Is.Null
            );
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 1),
                Is.EqualTo(second)
            );
        }

        [Test]
        public void RepeatedItemDataCreatesIndependentInstances()
        {
            ItemData item = CreateChargeableItem("charged_tool", ItemType.EngineeringPart);

            Assert.That(inventory.AddItem(item), Is.True);
            Assert.That(inventory.AddItem(item), Is.True);

            ItemInstance first = inventory.GetItemInstance(InventorySlotGroup.Backpack, 0);
            ItemInstance second = inventory.GetItemInstance(InventorySlotGroup.Backpack, 1);
            Assert.That(first.InstanceId, Is.Not.EqualTo(second.InstanceId));
            Assert.That(inventory.TryConsumeCharge(first, 10f), Is.True);
            Assert.That(first.Charge, Is.EqualTo(90f).Within(0.001f));
            Assert.That(second.Charge, Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void InstanceRestorePreservesIdentitySlotAndCharge()
        {
            ItemData item = CreateChargeableItem("restored_tool", ItemType.EngineeringPart);
            ItemInstance instance = ItemInstance.Create(item);
            instance.TryConsume(35f);

            inventory.RestoreInstanceSlots(
                new[] { instance, null },
                new ItemInstance[PlayerInventory.AnomalyCapacity],
                new ItemInstance[PlayerInventory.QuickAccessCapacity]
            );

            ItemInstance restored = inventory.GetItemInstance(InventorySlotGroup.Backpack, 0);
            Assert.That(restored.InstanceId, Is.EqualTo(instance.InstanceId));
            Assert.That(restored.Charge, Is.EqualTo(65f).Within(0.001f));
            Assert.That(inventory.GetItem(InventorySlotGroup.Backpack, 1), Is.Null);
        }

        [Test]
        public void UnifiedEquipmentUseConsumesConfiguredItemEnergy()
        {
            ItemData scanner = CreateChargeableItem("scanner", ItemType.Equipment);
            SerializedObject serializedScanner = new SerializedObject(scanner);
            serializedScanner.FindProperty("quickAccessAction").enumValueIndex =
                (int)QuickAccessAction.Scan;
            serializedScanner.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(inventory.AddItem(scanner), Is.True);
            ItemInstance instance = inventory.GetItemInstance(
                InventorySlotGroup.QuickAccess,
                PlayerInventory.ActiveQuickAccessStartIndex
            );
            PlayerEquipmentController equipment =
                root.AddComponent<PlayerEquipmentController>();

            Assert.That(equipment.TryUseItem(instance), Is.True);
            Assert.That(instance.Charge, Is.EqualTo(90f).Within(0.001f));
            Assert.That(equipment.TryUseItem(instance), Is.True);
            Assert.That(instance.Charge, Is.EqualTo(80f).Within(0.001f));
        }

        private ItemData CreateChargeableItem(string id, ItemType type)
        {
            ItemEnergyDefinition energy = ScriptableObject.CreateInstance<ItemEnergyDefinition>();
            SerializedObject serializedEnergy = new SerializedObject(energy);
            serializedEnergy.FindProperty("capacity").floatValue = 100f;
            serializedEnergy.FindProperty("initialCharge").floatValue = 100f;
            serializedEnergy.FindProperty("energyPerUse").floatValue = 10f;
            serializedEnergy.FindProperty("rechargePerSecond").floatValue = 20f;
            serializedEnergy.ApplyModifiedPropertiesWithoutUndo();
            createdEnergyDefinitions.Add(energy);

            ItemData item = CreateItem(id, type);
            SerializedObject serializedItem = new SerializedObject(item);
            serializedItem.FindProperty("energyDefinition").objectReferenceValue = energy;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private ItemData CreateItem(string id, ItemType type)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("itemType").enumValueIndex = (int)type;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            createdItems.Add(item);
            return item;
        }
    }

    public sealed class Sprint05LaboratoryTests
    {
        private GameObject systems;
        private GameObject player;
        private StationPowerController power;
        private ResearchController research;
        private PlayerInventory inventory;
        private ResearchDefinition definition;
        private LibraryEntryData libraryEntry;
        private ItemData sample;

        [SetUp]
        public void SetUp()
        {
            systems = new GameObject("Test_ResearchSystems");
            power = systems.AddComponent<StationPowerController>();
            research = systems.AddComponent<ResearchController>();
            research.SetPowerSource(power);

            player = new GameObject("Test_ResearchPlayer");
            inventory = player.AddComponent<PlayerInventory>();

            libraryEntry = ScriptableObject.CreateInstance<LibraryEntryData>();
            SerializedObject serializedEntry = new SerializedObject(libraryEntry);
            serializedEntry.FindProperty("entryId").stringValue = "test_sample_entry";
            serializedEntry.FindProperty("title").stringValue = "Test Sample Entry";
            serializedEntry.ApplyModifiedPropertiesWithoutUndo();

            definition = ScriptableObject.CreateInstance<ResearchDefinition>();
            SerializedObject serializedDefinition = new SerializedObject(definition);
            serializedDefinition.FindProperty("researchId").stringValue = "test_sample_research";
            serializedDefinition.FindProperty("displayName").stringValue = "Test Sample";
            serializedDefinition.FindProperty("analysisDuration").floatValue = 2f;
            serializedDefinition.FindProperty("unlockedEntry").objectReferenceValue = libraryEntry;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

            sample = ScriptableObject.CreateInstance<ItemData>();
            SerializedObject serializedItem = new SerializedObject(sample);
            serializedItem.FindProperty("itemId").stringValue = "test_sample";
            serializedItem.FindProperty("displayName").stringValue = "Test Sample";
            serializedItem.FindProperty("itemType").enumValueIndex = (int)ItemType.Anomaly;
            serializedItem.FindProperty("researchDefinition").objectReferenceValue = definition;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(sample);
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(libraryEntry);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(systems);
        }

        [Test]
        public void AnalyzedSampleRemainsInLabAndCannotBeScannedAgain()
        {
            power.RestorePower();
            Assert.That(inventory.AddItem(sample), Is.True, "Sample should enter anomaly storage.");
            Assert.That(research.LoadItem(sample, inventory), Is.True, "Sample should enter laboratory slot.");
            Assert.That(research.StartAnalysis(), Is.True, "Powered laboratory should start scanning.");

            research.AdvanceAnalysis(2f);

            Assert.That(research.State, Is.EqualTo(ResearchController.ResearchState.Complete));
            Assert.That(research.LoadedItem, Is.EqualTo(sample));
            Assert.That(research.IsAnalyzed(sample), Is.True);

            Assert.That(research.RetrieveLoadedItem(), Is.True, "Analyzed sample should return to backpack.");
            Assert.That(inventory.Contains(sample.ItemId), Is.True);

            Assert.That(research.LoadItem(sample, inventory), Is.True, "Analyzed sample should still be loadable for inspection.");
            Assert.That(research.State, Is.EqualTo(ResearchController.ResearchState.Complete));
            Assert.That(research.StartAnalysis(), Is.False);
        }

        [Test]
        public void KnownItemCanUseLaboratorySlotWithoutCreatingResearch()
        {
            ItemData knownItem = ScriptableObject.CreateInstance<ItemData>();
            SerializedObject serialized = new SerializedObject(knownItem);
            serialized.FindProperty("itemId").stringValue = "known_part";
            serialized.FindProperty("displayName").stringValue = "Known Part";
            serialized.FindProperty("description").stringValue = "Already identified.";
            serialized.FindProperty("itemType").enumValueIndex =
                (int)ItemType.EngineeringPart;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(inventory.AddItem(knownItem), Is.True);
            Assert.That(research.LoadItem(knownItem, inventory), Is.True);
            Assert.That(research.LoadedItem, Is.EqualTo(knownItem));
            Assert.That(research.IsResearchable(knownItem), Is.False);
            Assert.That(research.CanStartAnalysis, Is.False);
            Assert.That(research.StatusMessage, Is.EqualTo("Known Part"));
            Assert.That(research.RetrieveLoadedItem(), Is.True);

            Object.DestroyImmediate(knownItem);
        }

        [Test]
        public void LibraryCataloguesKnownStationItemsButNotAnomaliesOnPickup()
        {
            GameObject libraryRoot = new GameObject("Test_Library");
            LibraryController library = libraryRoot.AddComponent<LibraryController>();
            ItemData stationItem = CreateItem("known_station_part", ItemType.EngineeringPart);
            ItemData anomalyItem = CreateItem("unknown_anomaly", ItemType.Anomaly);

            Assert.That(library.RegisterKnownItem(stationItem), Is.True);
            Assert.That(library.IsKnownItem(stationItem), Is.True);

            Assert.That(library.RegisterKnownItem(stationItem), Is.False);
            Assert.That(library.RegisterKnownItem(anomalyItem), Is.False);
            Assert.That(library.IsKnownItem(anomalyItem), Is.False);

            Object.DestroyImmediate(stationItem);
            Object.DestroyImmediate(anomalyItem);
            Object.DestroyImmediate(libraryRoot);
        }

        private static ItemData CreateItem(string id, ItemType type)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("itemType").enumValueIndex = (int)type;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }
    }

}
