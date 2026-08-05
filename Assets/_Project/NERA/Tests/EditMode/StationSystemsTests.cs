using NERA.Expeditions;
using NERA.Energy;
using NERA.Inventory;
using NERA.Interaction;
using NERA.Items;
using NERA.Maintenance;
using NERA.Station;
using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace NERA.Tests
{
    public sealed class StationSystemsTests
    {
        private GameObject stationRoot;
        private GameObject playerRoot;
        private StationStorageController storage;
        private StationSystemsController systems;
        private EnergySystemController energy;
        private PlayerInventory inventory;
        private ItemData servoDrive;
        private ItemData signalRelay;

        [SetUp]
        public void SetUp()
        {
            SetSingleton(typeof(EnergySystemController), null);
            SetSingleton(typeof(StationStorageController), null);
            SetSingleton(typeof(StationSystemsController), null);
            stationRoot = new GameObject("Test_StationSystems");
            energy = stationRoot.AddComponent<EnergySystemController>();
            SetSingleton(typeof(EnergySystemController), energy);
            energy.RegisterBattery("test_battery", 1000f, 1000f);
            energy.SetGridEnabled(true);
            storage = stationRoot.AddComponent<StationStorageController>();
            systems = stationRoot.AddComponent<StationSystemsController>();
            SetSingleton(typeof(StationStorageController), storage);
            SetSingleton(typeof(StationSystemsController), systems);
            systems.ResetSystems();

            playerRoot = new GameObject("Test_StationPlayer");
            inventory = playerRoot.AddComponent<PlayerInventory>();

            servoDrive = ScriptableObject.CreateInstance<ItemData>();
            SerializedObject serializedItem = new SerializedObject(servoDrive);
            serializedItem.FindProperty("itemId").stringValue = "servo_drive_01";
            serializedItem.FindProperty("displayName").stringValue = "Servo Drive";
            serializedItem.FindProperty("itemType").enumValueIndex =
                (int)ItemType.EngineeringPart;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            signalRelay = CreateItem(
                "nera_signal_relay_02",
                ItemType.EngineeringPart);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(servoDrive);
            Object.DestroyImmediate(signalRelay);
            Object.DestroyImmediate(playerRoot);
            Object.DestroyImmediate(stationRoot);
            SetSingleton(typeof(EnergySystemController), null);
            SetSingleton(typeof(StationStorageController), null);
            SetSingleton(typeof(StationSystemsController), null);
        }

        [Test]
        public void ExplicitDepositMovesBackpackInstancesIntoStationStorage()
        {
            Assert.That(inventory.AddItem(servoDrive), Is.True);
            ItemInstance original = inventory.GetItemInstance(
                InventorySlotGroup.Backpack,
                0);

            Assert.That(storage.DepositBackpack(inventory), Is.EqualTo(1));
            Assert.That(inventory.Count, Is.Zero);
            Assert.That(storage.Count, Is.EqualTo(1));
            Assert.That(
                storage.BackpackSlots[0].InstanceId,
                Is.EqualTo(original.InstanceId));
        }

        [Test]
        public void StorageRoutesToolsAndAnomaliesToDedicatedGroups()
        {
            ItemData tool = CreateItem("test_tool", ItemType.Equipment);
            ItemData anomaly = CreateItem("test_anomaly", ItemType.Anomaly);
            inventory.AddItem(servoDrive);
            inventory.AddItem(tool);
            inventory.AddItem(anomaly);

            Assert.That(storage.DepositAll(inventory), Is.EqualTo(3));
            Assert.That(storage.BackpackSlots[0].ItemData, Is.EqualTo(servoDrive));
            Assert.That(storage.QuickAccessSlots[0].ItemData, Is.EqualTo(tool));
            Assert.That(storage.AnomalySlots[0].ItemData, Is.EqualTo(anomaly));

            Assert.That(
                storage.WithdrawTo(InventorySlotGroup.QuickAccess, 0, inventory),
                Is.True);
            Assert.That(inventory.Contains("test_tool"), Is.True);
            Assert.That(storage.QuickAccessSlots[0], Is.Null);

            Object.DestroyImmediate(tool);
            Object.DestroyImmediate(anomaly);
        }

        [Test]
        public void DragTransferMovesAndSwapsItemsWithoutLosingInstances()
        {
            ItemData second = CreateItem("second_part", ItemType.EngineeringPart);
            inventory.AddItem(servoDrive);
            inventory.AddItem(second);
            ItemInstance firstInstance = inventory.BackpackItemInstances[0];
            ItemInstance secondInstance = inventory.BackpackItemInstances[1];

            Assert.That(storage.MoveFromInventory(
                inventory,
                InventorySlotGroup.Backpack,
                0,
                InventorySlotGroup.Backpack,
                0), Is.True);
            Assert.That(storage.BackpackSlots[0], Is.SameAs(firstInstance));

            Assert.That(storage.MoveFromInventory(
                inventory,
                InventorySlotGroup.Backpack,
                1,
                InventorySlotGroup.Backpack,
                0), Is.True);
            Assert.That(storage.BackpackSlots[0], Is.SameAs(secondInstance));
            Assert.That(inventory.BackpackItemInstances[1], Is.SameAs(firstInstance));

            Assert.That(storage.MoveToInventory(
                InventorySlotGroup.Backpack,
                0,
                inventory,
                InventorySlotGroup.Backpack,
                0), Is.True);
            Assert.That(inventory.BackpackItemInstances[0], Is.SameAs(secondInstance));
            Assert.That(storage.BackpackSlots[0], Is.Null);

            Object.DestroyImmediate(second);
        }

        [Test]
        public void UpgradeConsumesPartFromStorageAndUnlocksDroneRange()
        {
            inventory.AddItem(servoDrive);
            storage.DepositBackpack(inventory);

            ExpeditionLocationData distant =
                ScriptableObject.CreateInstance<ExpeditionLocationData>();
            SerializedObject serializedLocation = new SerializedObject(distant);
            serializedLocation.FindProperty("requiredDroneUpgradeLevel").intValue = 2;
            serializedLocation.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(systems.CanDroneReach(distant), Is.False);
            Assert.That(
                systems.TryUpgrade(StationSystemType.Drone, inventory, storage),
                Is.True);
            Assert.That(storage.Count, Is.Zero);
            Assert.That(systems.GetUpgradeLevel(StationSystemType.Drone), Is.EqualTo(2));
            Assert.That(systems.CanDroneReach(distant), Is.True);

            Object.DestroyImmediate(distant);
        }

        [Test]
        public void UpgradesAreSequentialAndConsumeLevelCost()
        {
            inventory.AddItem(servoDrive);
            inventory.AddItem(servoDrive);
            inventory.AddItem(servoDrive);
            inventory.AddItem(signalRelay);

            Assert.That(
                systems.TryUpgradeTo(
                    StationSystemType.Drone,
                    3,
                    inventory,
                    storage),
                Is.False,
                "Level 3 must not be installed before level 2.");

            float energyBefore = energy.CurrentEnergy;
            Assert.That(
                systems.CanUpgradeTo(
                    StationSystemType.Drone,
                    2,
                    inventory,
                    storage,
                    out string levelTwoReason),
                Is.True,
                levelTwoReason);
            Assert.That(
                systems.TryUpgradeTo(
                    StationSystemType.Drone,
                    2,
                    inventory,
                    storage),
                Is.True,
                "Level 2 should consume its configured part and energy.");
            Assert.That(systems.GetUpgradeLevel(StationSystemType.Drone), Is.EqualTo(2));
            Assert.That(inventory.CountItem("servo_drive_01"), Is.EqualTo(2));
            Assert.That(energy.CurrentEnergy, Is.EqualTo(energyBefore - 100f));

            Assert.That(
                systems.CanUpgradeTo(
                    StationSystemType.Drone,
                    3,
                    inventory,
                    storage,
                    out string reason),
                Is.True,
                reason);
            Assert.That(
                systems.TryUpgradeTo(
                    StationSystemType.Drone,
                    3,
                    inventory,
                    storage),
                Is.True,
                "Level 3 should consume every configured item and 200 energy.");
            Assert.That(systems.GetUpgradeLevel(StationSystemType.Drone), Is.EqualTo(3));
            Assert.That(inventory.CountItem("servo_drive_01"), Is.Zero);
            Assert.That(inventory.CountItem("nera_signal_relay_02"), Is.Zero);
            Assert.That(energy.CurrentEnergy, Is.EqualTo(energyBefore - 300f));
        }

        [Test]
        public void ConfiguredChargeThresholdBlocksSystemStart()
        {
            StationSystemDefinition laboratory =
                systems.GetDefinition(StationSystemType.Laboratory);
            Assert.That(laboratory, Is.Not.Null);
            Assert.That(
                energy.Config.StationObjectCutoffs.Count,
                Is.EqualTo(systems.Config.StationObjects.Count),
                "Every configured station object needs an energy cutoff entry.");
            bool hasSecondTurretCutoff = false;
            foreach (StationObjectPowerCutoff cutoff
                     in energy.Config.StationObjectCutoffs)
            {
                if (cutoff.SystemType == StationSystemType.Turret &&
                    cutoff.ObjectId == "station_turret_02")
                {
                    hasSecondTurretCutoff = true;
                    break;
                }
            }
            Assert.That(
                hasSecondTurretCutoff,
                Is.True,
                "Repeated systems must resolve their cutoff by object id.");

            energy.RestoreState(energy.TotalCapacity * 0.2f, true);

            Assert.That(
                systems.CanStart(
                    StationSystemType.Laboratory,
                    out string lowPowerReason),
                Is.False);
            float configuredThreshold =
                energy.Config.GetMinimumChargePercent(
                    StationSystemType.Laboratory);
            Assert.That(
                lowPowerReason,
                Does.Contain($"{configuredThreshold:0}%"));

            energy.RestoreState(energy.TotalCapacity * 0.3f, true);

            Assert.That(
                systems.CanStart(
                    StationSystemType.Laboratory,
                    out string recoveredReason),
                Is.True,
                recoveredReason);
        }

        [Test]
        public void TurretInstancesKeepIndependentUpgradeLevels()
        {
            const string firstTurret = "station_turret_01";
            const string secondTurret = "station_turret_02";
            systems.RegisterObject(
                StationSystemType.Turret,
                firstTurret,
                1,
                true);
            systems.RegisterObject(
                StationSystemType.Turret,
                secondTurret,
                0,
                false);

            Assert.That(
                systems.GetUpgradeLevel(
                    StationSystemType.Turret,
                    firstTurret,
                    1),
                Is.EqualTo(1));
            Assert.That(
                systems.GetUpgradeLevel(
                    StationSystemType.Turret,
                    secondTurret,
                    0),
                Is.Zero);

            inventory.AddItem(servoDrive);
            Assert.That(
                systems.TryUpgradeTo(
                    StationSystemType.Turret,
                    secondTurret,
                    0,
                    1,
                    inventory,
                    storage),
                Is.True);

            Assert.That(
                systems.GetUpgradeLevel(
                    StationSystemType.Turret,
                    firstTurret,
                    1),
                Is.EqualTo(1),
                "Upgrading turret 2 must not modify turret 1.");
            Assert.That(
                systems.GetUpgradeLevel(
                    StationSystemType.Turret,
                    secondTurret,
                    0),
                Is.EqualTo(1));
            Assert.That(
                systems.IsRequestedActive(
                    StationSystemType.Turret,
                    secondTurret,
                    0,
                    false),
                Is.True,
                "Installing level 1 must activate that turret instance.");
            Assert.That(
                systems.SetRequestedActive(
                    StationSystemType.Turret,
                    false,
                    secondTurret,
                    0,
                    false),
                Is.True);
            Assert.That(
                systems.IsRequestedActive(
                    StationSystemType.Turret,
                    firstTurret,
                    1,
                    true),
                Is.True,
                "Stopping turret 2 must not stop turret 1.");
        }

        [Test]
        public void TurretInstancesUseTheirOwnConfiguredParts()
        {
            const string thirdTurret = "station_turret_03";
            StationSystemDefinition configuredTurret =
                systems.Config.Find(
                    StationSystemType.Turret,
                    thirdTurret);
            Assert.That(configuredTurret, Is.Not.Null);
            Assert.That(
                systems.Config.FindByObjectId(thirdTurret),
                Is.SameAs(configuredTurret));

            inventory.AddItem(servoDrive);
            Assert.That(
                systems.CanUpgradeTo(
                    StationSystemType.Turret,
                    thirdTurret,
                    0,
                    1,
                    inventory,
                    storage,
                    out string reason),
                Is.False);
            Assert.That(reason, Does.Contain("Blue IO Shard"));

            ItemData ioShard = CreateItem(
                "io_blue_shard_01",
                ItemType.EngineeringPart);
            inventory.AddItem(ioShard);
            inventory.AddItem(ioShard);
            Assert.That(
                systems.TryUpgradeTo(
                    StationSystemType.Turret,
                    thirdTurret,
                    0,
                    1,
                    inventory,
                    storage),
                Is.True);
            Assert.That(
                inventory.CountItem("servo_drive_01"),
                Is.EqualTo(1),
                "Turret 3 must not consume turret 2's configured part.");
            Assert.That(
                inventory.CountItem("io_blue_shard_01"),
                Is.Zero);

            Object.DestroyImmediate(ioShard);
        }

        [Test]
        public void RestoreKeepsPerObjectTurretProgress()
        {
            systems.Restore(
                null,
                null,
                new[]
                {
                    new StationObjectSystemState(
                        StationSystemType.Turret,
                        "station_turret_03",
                        2,
                        false)
                });

            Assert.That(
                systems.GetUpgradeLevel(
                    StationSystemType.Turret,
                    "station_turret_03",
                    0),
                Is.EqualTo(2));
            Assert.That(
                systems.IsRequestedActive(
                    StationSystemType.Turret,
                    "station_turret_03",
                    0,
                    false),
                Is.False);
        }

        [Test]
        public void UpgradeLevelsMatchStationDesign()
        {
            StationSystemsConfig config = systems.Config;
            Assert.That(
                config.Find(StationSystemType.Battery).InitialLevel,
                Is.EqualTo(1));
            Assert.That(
                config.Find(StationSystemType.Battery).MaxLevel,
                Is.EqualTo(2));
            Assert.That(
                config.Find(StationSystemType.Drone).InitialLevel,
                Is.EqualTo(1));
            Assert.That(
                config.Find(StationSystemType.Drone).MaxLevel,
                Is.EqualTo(3));
            Assert.That(
                config.Find(StationSystemType.Antenna).InitialLevel,
                Is.Zero);
            Assert.That(
                config.Find(
                    StationSystemType.Turret,
                    "station_turret_01").InitialLevel,
                Is.EqualTo(1));
            Assert.That(
                config.Find(
                    StationSystemType.Turret,
                    "station_turret_02").InitialLevel,
                Is.Zero);
        }

        [Test]
        public void RestoreMigratesNonLockedSystemsToTheirConfiguredStartingLevel()
        {
            systems.Restore(
                new[]
                {
                    new System.Collections.Generic.KeyValuePair<
                        StationSystemType, int>(
                            StationSystemType.Battery, 0),
                    new System.Collections.Generic.KeyValuePair<
                        StationSystemType, int>(
                            StationSystemType.Drone, 0)
                },
                null);

            Assert.That(
                systems.GetUpgradeLevel(StationSystemType.Battery),
                Is.EqualTo(1));
            Assert.That(
                systems.GetUpgradeLevel(StationSystemType.Drone),
                Is.EqualTo(1));
        }

        [TestCase(
            "Assets/_Project/NERA/Prefabs/StationUpgrade/P_StationTurret_Stages.prefab",
            StationSystemType.Turret,
            0,
            3)]
        [TestCase(
            "Assets/_Project/NERA/Prefabs/StationUpgrade/P_StationBattery_Stages.prefab",
            StationSystemType.Battery,
            1,
            2)]
        [TestCase(
            "Assets/_Project/NERA/Prefabs/StationUpgrade/P_StationDrone_Stages.prefab",
            StationSystemType.Drone,
            1,
            3)]
        [TestCase(
            "Assets/_Project/NERA/Prefabs/StationUpgrade/P_StationAntenna_Stages.prefab",
            StationSystemType.Antenna,
            0,
            3)]
        public void UpgradeStagePrefabHasExpectedStructure(
            string prefabPath,
            StationSystemType expectedType,
            int initialStage,
            int maximumStage)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);

            StationUpgradeStageController stageController =
                prefab.GetComponent<StationUpgradeStageController>();
            StationObjectIdentity identity =
                prefab.GetComponent<StationObjectIdentity>();
            Assert.That(stageController, Is.Not.Null);
            Assert.That(identity, Is.Not.Null);
            Assert.That(identity.SystemType, Is.EqualTo(expectedType));
            Assert.That(stageController.SystemType, Is.EqualTo(expectedType));
            Assert.That(stageController.ObjectId, Is.EqualTo(identity.ObjectId));
            Assert.That(stageController.MaxStage, Is.EqualTo(maximumStage));

            for (int stage = 0; stage <= maximumStage; stage++)
            {
                Transform stageRoot = prefab.transform.Find($"Stage_{stage}");
                Assert.That(stageRoot, Is.Not.Null, $"Missing Stage_{stage}");
                Assert.That(
                    stageRoot.gameObject.activeSelf,
                    Is.EqualTo(stage == initialStage),
                    $"Unexpected active state for Stage_{stage}");
            }
        }

        [Test]
        public void UpgradeStageControllerTracksInstalledLevel()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/NERA/Prefabs/StationUpgrade/" +
                "P_StationTurret_Stages.prefab");
            GameObject instance = Object.Instantiate(prefab);
            StationUpgradeStageController stageController =
                instance.GetComponent<StationUpgradeStageController>();

            systems.Restore(
                new[]
                {
                    new System.Collections.Generic.KeyValuePair<
                        StationSystemType, int>(
                            StationSystemType.Turret, 2)
                },
                null);
            stageController.RefreshVisuals();

            Assert.That(stageController.CurrentStage, Is.EqualTo(2));
            Assert.That(
                instance.transform.Find("Stage_0").gameObject.activeSelf,
                Is.False);
            Assert.That(
                instance.transform.Find("Stage_2").gameObject.activeSelf,
                Is.True);
            Assert.That(
                instance.transform.Find("Stage_3").gameObject.activeSelf,
                Is.False);

            Object.DestroyImmediate(instance);
        }

        [Test]
        public void UpgradeStageControllerBindsWhenSystemsIsCreatedLater()
        {
            Object.DestroyImmediate(systems);
            systems = null;
            SetSingleton(typeof(StationSystemsController), null);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/NERA/Prefabs/StationUpgrade/" +
                "P_StationTurret_Stages.prefab");
            GameObject instance = Object.Instantiate(prefab);
            StationUpgradeStageController stageController =
                instance.GetComponent<StationUpgradeStageController>();
            MethodInfo stageOnDisable =
                typeof(StationUpgradeStageController).GetMethod(
                    "OnDisable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo stageOnEnable =
                typeof(StationUpgradeStageController).GetMethod(
                    "OnEnable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(stageOnDisable, Is.Not.Null);
            Assert.That(stageOnEnable, Is.Not.Null);
            stageOnDisable.Invoke(stageController, null);
            stageOnEnable.Invoke(stageController, null);

            systems = stationRoot.AddComponent<StationSystemsController>();
            MethodInfo systemsAwake =
                typeof(StationSystemsController).GetMethod(
                    "Awake",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(systemsAwake, Is.Not.Null);
            systemsAwake.Invoke(systems, null);
            systems.Restore(
                new[]
                {
                    new System.Collections.Generic.KeyValuePair<
                        StationSystemType, int>(
                            StationSystemType.Turret, 2)
                },
                null);

            Assert.That(stageController.CurrentStage, Is.EqualTo(2));
            Assert.That(
                instance.transform.Find("Stage_2").gameObject.activeSelf,
                Is.True);

            stageOnDisable.Invoke(stageController, null);
            Object.DestroyImmediate(instance);
        }

        [Test]
        public void ComputerAndPowerSourcesCannotBeStoppedFromTerminal()
        {
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Computer, false),
                Is.False);
            Assert.That(
                systems.SetRequestedActive(StationSystemType.SolarPanel, false),
                Is.False);
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Battery, false),
                Is.False);
        }

        [Test]
        public void PhysicalInteractionStartsTheSameRequestedStateAsTerminal()
        {
            StationDeviceInteractable device = CreateStationDevice(
                StationSystemType.Laboratory,
                out MaintainableObject maintenance);
            Assert.That(
                typeof(IInteractable).IsAssignableFrom(
                    typeof(MaintainableObject)),
                Is.False);

            Assert.That(
                systems.SetRequestedActive(
                    StationSystemType.Laboratory,
                    false),
                Is.True);
            Assert.That(device.GetPrompt().IsVisible, Is.True);

            device.CompleteInteraction(playerRoot);

            Assert.That(
                systems.IsRequestedActive(StationSystemType.Laboratory),
                Is.True);
            Assert.That(
                device.GetPrompt().IsVisible,
                Is.False,
                "A healthy running device must not display Operational.");
        }

        [Test]
        public void PhysicalStartDoesNotChangeToggleWithoutPower()
        {
            StationDeviceInteractable device = CreateStationDevice(
                StationSystemType.Laboratory,
                out _);
            systems.SetRequestedActive(
                StationSystemType.Laboratory,
                false);
            energy.SetGridEnabled(false);

            InteractionPrompt prompt = device.GetPrompt();
            Assert.That(prompt.IsVisible, Is.True);
            Assert.That(prompt.IsAvailable, Is.True);

            device.CompleteInteraction(playerRoot);

            Assert.That(
                systems.IsRequestedActive(StationSystemType.Laboratory),
                Is.False,
                "A failed physical start must not move the terminal toggle.");
        }

        [Test]
        public void MaintenanceActionRepairsBeforeManualStart()
        {
            StationDeviceInteractable device = CreateStationDevice(
                StationSystemType.Laboratory,
                out MaintainableObject maintenance);
            systems.SetRequestedActive(
                StationSystemType.Laboratory,
                false);
            maintenance.SetCondition(0.5f);

            Assert.That(
                device.GetPrompt().ActionText,
                Is.EqualTo("Service Device"));

            device.CompleteInteraction(playerRoot);

            Assert.That(maintenance.Condition, Is.EqualTo(1f));
            Assert.That(
                systems.IsRequestedActive(StationSystemType.Laboratory),
                Is.False,
                "Repair and start are two separate physical actions.");
            Assert.That(device.GetPrompt().IsVisible, Is.True);
        }

        [Test]
        public void MaintenanceLookupUsesStableObjectIdBeforeRole()
        {
            MaintainableObject wrong = CreateMaintainable(
                "wrong_antenna",
                MaintenanceRole.Antenna,
                0f);
            MaintainableObject antenna = CreateMaintainable(
                "station_antenna",
                MaintenanceRole.Antenna,
                1f);

            Assert.That(wrong.IsOperational, Is.False);
            Assert.That(
                systems.IsMaintenanceReady(
                    StationSystemType.Antenna,
                    "station_antenna"),
                Is.True,
                "A different object with the same role must be ignored.");

            antenna.SetCondition(0f);
            Assert.That(
                systems.IsMaintenanceReady(
                    StationSystemType.Antenna,
                    "station_antenna"),
                Is.False);

            antenna.gameObject.SetActive(false);
            Assert.That(
                systems.IsMaintenanceReady(
                    StationSystemType.Antenna,
                    "station_antenna"),
                Is.True,
                "A missing exact ID must not fall back to another device role.");
        }

        [Test]
        public void MaintenancePrefabsExposeStableIdsThroughDeviceInteraction()
        {
            StationSystemsConfig config = StationSystemsConfig.LoadDefault();
            Assert.That(
                config.Find(
                    StationSystemType.SolarPanel,
                    "station_solar_01"),
                Is.Not.Null);

            GameObject antennaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/NERA/Prefabs/StationUpgrade/" +
                "P_StationAntenna_Stages.prefab");
            MaintainableObject[] antennaMaintenance =
                antennaPrefab.GetComponentsInChildren<MaintainableObject>(true);
            StationDeviceInteractable[] antennaInteractions =
                antennaPrefab.GetComponentsInChildren<StationDeviceInteractable>(true);
            Assert.That(antennaMaintenance, Has.Length.EqualTo(1));
            Assert.That(antennaInteractions, Has.Length.EqualTo(1));
            foreach (MaintainableObject maintainable in antennaMaintenance)
                Assert.That(maintainable.ObjectId, Is.EqualTo("station_antenna"));
            foreach (StationDeviceInteractable interaction in antennaInteractions)
                Assert.That(interaction.ObjectId, Is.EqualTo("station_antenna"));

            GameObject turretPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/NERA/Prefabs/Station/P_StationTurret.prefab");
            Assert.That(
                turretPrefab.GetComponent<StationDeviceInteractable>(),
                Is.Not.Null);
            Assert.That(
                turretPrefab.GetComponent<StationTurretController>().TurretId,
                Is.Empty,
                "A staged turret receives its ID from the stage root instance.");
        }

        private StationDeviceInteractable CreateStationDevice(
            StationSystemType type,
            out MaintainableObject maintenance)
        {
            GameObject deviceRoot = new GameObject($"Test_{type}_Device");
            deviceRoot.transform.SetParent(stationRoot.transform);
            StationObjectIdentity identity =
                deviceRoot.AddComponent<StationObjectIdentity>();
            identity.Configure(type, string.Empty);
            maintenance = deviceRoot.AddComponent<MaintainableObject>();
            StationDeviceInteractable device =
                deviceRoot.AddComponent<StationDeviceInteractable>();
            return device;
        }

        private MaintainableObject CreateMaintainable(
            string objectId,
            MaintenanceRole role,
            float condition)
        {
            GameObject deviceRoot = new GameObject($"Test_{objectId}");
            deviceRoot.transform.SetParent(stationRoot.transform);
            StationObjectIdentity identity =
                deviceRoot.AddComponent<StationObjectIdentity>();
            StationSystemType type = role switch
            {
                MaintenanceRole.SolarPanel => StationSystemType.SolarPanel,
                MaintenanceRole.Antenna => StationSystemType.Antenna,
                MaintenanceRole.Turret => StationSystemType.Turret,
                _ => default
            };
            identity.Configure(type, objectId);
            MaintainableObject maintainable =
                deviceRoot.AddComponent<MaintainableObject>();
            SerializedObject serialized = new SerializedObject(maintainable);
            serialized.FindProperty("role").enumValueIndex = (int)role;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            maintainable.SetCondition(condition);
            return maintainable;
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

        private static void SetSingleton(System.Type controllerType, object value)
        {
            PropertyInfo instanceProperty = controllerType.GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public);
            instanceProperty?.GetSetMethod(true)?.Invoke(null, new[] { value });
        }
    }
}
