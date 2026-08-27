using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NERA.Energy;
using NERA.Interaction;
using NERA.Inventory;
using NERA.Items;
using NERA.Maintenance;
using NERA.Station;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using NeraInteractionMode = NERA.Interaction.InteractionMode;

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

        [SetUp]
        public void SetUp()
        {
            SetSingleton(typeof(EnergySystemController), null);
            SetSingleton(typeof(StationStorageController), null);
            SetSingleton(typeof(StationSystemsController), null);
            SetSingleton(typeof(StationUpgradeModeController), null);

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
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(playerRoot);
            Object.DestroyImmediate(stationRoot);
            SetSingleton(typeof(EnergySystemController), null);
            SetSingleton(typeof(StationStorageController), null);
            SetSingleton(typeof(StationSystemsController), null);
            SetSingleton(typeof(StationUpgradeModeController), null);
        }

        [Test]
        public void StationConfigDefinesSixPhysicalUpgradeObjects()
        {
            StationSystemsConfig config = systems.Config;
            Assert.That(
                config.StationObjects.Count(
                    item => item.SystemType == StationSystemType.Turret),
                Is.EqualTo(2));
            Assert.That(
                config.StationObjects.Count(
                    item => item.SystemType == StationSystemType.Antenna),
                Is.EqualTo(1));
            Assert.That(
                config.StationObjects.Count(
                    item => item.SystemType == StationSystemType.Drone),
                Is.EqualTo(1));
            Assert.That(
                config.StationObjects.Count(
                    item => item.SystemType == StationSystemType.Battery),
                Is.EqualTo(1));
            Assert.That(
                config.StationObjects.Count(
                    item => item.SystemType == StationSystemType.SolarPanel),
                Is.EqualTo(1));

            foreach (StationSystemDefinition definition in
                     config.StationObjects.Where(item =>
                         item.SystemType == StationSystemType.Turret ||
                         item.SystemType == StationSystemType.Antenna ||
                         item.SystemType == StationSystemType.Drone ||
                         item.SystemType == StationSystemType.Battery ||
                         item.SystemType == StationSystemType.SolarPanel))
            {
                Assert.That(definition.ObjectId, Is.Not.Empty);
                Assert.That(definition.Slots, Is.Not.Empty, definition.ObjectId);
                Assert.That(definition.BaseStats, Is.Not.Empty, definition.ObjectId);
            }
        }

        [Test]
        public void TurretAimToleranceIsCodeOnlyAndStatIdsRemainStable()
        {
            foreach (StationSystemDefinition turret in
                     systems.Config.StationObjects.Where(
                         item => item.SystemType == StationSystemType.Turret))
            {
                Assert.That(
                    turret.BaseStats.Select(stat => (int)stat.Stat),
                    Is.All.Not.EqualTo(15),
                    turret.ObjectId);
            }

            Assert.That(
                System.Enum.IsDefined(typeof(StationObjectStat), 15),
                Is.False);
            Assert.That((int)StationObjectStat.BatteryCharge, Is.EqualTo(16));
            Assert.That(
                (int)StationObjectStat.FlightEnergyConsumption,
                Is.EqualTo(17));
            Assert.That((int)StationObjectStat.BackupReserve, Is.EqualTo(18));
            Assert.That((int)StationObjectStat.PowerOutput, Is.EqualTo(19));
            Assert.That((int)StationObjectStat.DustTolerance, Is.EqualTo(20));
        }

        [Test]
        public void SolarPanelPartsImproveGenerationAndDustTolerance()
        {
            StationSystemDefinition panel = systems.Config.Find(
                StationSystemType.SolarPanel,
                "station_solar_01");
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.Slots, Has.Count.EqualTo(4));
            Assert.That(
                panel.BaseStats.Select(stat => stat.Stat),
                Is.EquivalentTo(new[]
                {
                    StationObjectStat.Generation,
                    StationObjectStat.DustTolerance
                }));

            ItemData cells = LoadPart("Item_cells_01.asset");
            ItemData dustProtection = LoadPart("Item_dust_repeller_01.asset");
            ItemData optimizer = LoadPart("Item_mppt_controller_01.asset");
            ItemData tracker = LoadPart("Item_tracker_01.asset");
            var requests = new[]
            {
                new StationPartInstallRequest("Slot_1", cells),
                new StationPartInstallRequest("Slot_2", dustProtection),
                new StationPartInstallRequest("Slot_3", optimizer),
                new StationPartInstallRequest("Slot_4", tracker)
            };

            Assert.That(
                systems.TryInstallParts(
                    StationSystemType.SolarPanel,
                    panel.ObjectId,
                    requests,
                    out string reason),
                Is.True,
                reason);
            Assert.That(
                systems.GetStat(
                    StationSystemType.SolarPanel,
                    panel.ObjectId,
                    StationObjectStat.Generation),
                Is.EqualTo(63.25f).Within(0.001f));
            Assert.That(
                systems.GetStat(
                    StationSystemType.SolarPanel,
                    panel.ObjectId,
                    StationObjectStat.DustTolerance),
                Is.EqualTo(35f));
        }

        [Test]
        public void DustToleranceRetainsPartOfDirtyPanelOutput()
        {
            Assert.That(
                SolarPowerSource.CalculateConditionOutputMultiplier(0f, 0f),
                Is.Zero);
            Assert.That(
                SolarPowerSource.CalculateConditionOutputMultiplier(0.5f, 0f),
                Is.EqualTo(0.5f));
            Assert.That(
                SolarPowerSource.CalculateConditionOutputMultiplier(0f, 35f),
                Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(
                SolarPowerSource.CalculateConditionOutputMultiplier(1f, 35f),
                Is.EqualTo(1f));
        }

        [TestCase(StationSystemType.SolarPanel, true)]
        [TestCase(StationSystemType.Antenna, true)]
        [TestCase(StationSystemType.Turret, true)]
        [TestCase(StationSystemType.Drone, true)]
        [TestCase(StationSystemType.Battery, false)]
        [TestCase(StationSystemType.Terminal, false)]
        [TestCase(StationSystemType.Laboratory, false)]
        public void ConditionIsShownOnlyForOutdoorSystems(
            StationSystemType type,
            bool expected)
        {
            Assert.That(
                StationSystemsController.UsesCondition(type),
                Is.EqualTo(expected));
        }

        [Test]
        public void OutdoorDroneHasItsOwnWeatherMaintenanceRole()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/NERA/Prefabs/Station/Station_Drone.prefab");
            MaintainableObject maintenance =
                prefab?.GetComponent<MaintainableObject>();

            Assert.That(prefab, Is.Not.Null);
            Assert.That(maintenance, Is.Not.Null);
            Assert.That(maintenance.Role, Is.EqualTo(MaintenanceRole.Drone));
            Assert.That(maintenance.ExposedToWeather, Is.True);
        }

        [Test]
        public void BatteryUsesSevenSlotsAndOnlyItsThreeUsefulStats()
        {
            StationSystemDefinition battery = systems.Config.Find(
                StationSystemType.Battery,
                "station_battery");
            Assert.That(battery, Is.Not.Null);
            Assert.That(battery.Slots, Has.Count.EqualTo(7));
            Assert.That(
                battery.BaseStats.Select(stat => stat.Stat),
                Is.EquivalentTo(new[]
                {
                    StationObjectStat.Capacity,
                    StationObjectStat.BackupReserve,
                    StationObjectStat.PowerOutput
                }));

            string searchRoot =
                "Assets/_Project/NERA/Configs/Items/Item_EngineeringPart/Battery";
            string[] guids = AssetDatabase.FindAssets(
                "t:ItemData",
                new[] { searchRoot });
            Assert.That(guids, Has.Length.EqualTo(6));

            var coveredSlots = new HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);
            var allowedStats = new HashSet<StationObjectStat>
            {
                StationObjectStat.Capacity,
                StationObjectStat.BackupReserve,
                StationObjectStat.PowerOutput
            };
            foreach (string guid in guids)
            {
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(
                    AssetDatabase.GUIDToAssetPath(guid));
                Assert.That(item?.EngineeringPartDefinition, Is.Not.Null);
                foreach (EngineeringPartCompatibility compatibility in
                         item.EngineeringPartDefinition.CompatibleInstallations)
                {
                    Assert.That(
                        compatibility.SystemType,
                        Is.EqualTo(StationSystemType.Battery),
                        item.ItemId);
                    Assert.That(
                        battery.FindSlot(compatibility.SlotId),
                        Is.Not.Null,
                        $"{item.ItemId}/{compatibility.SlotId}");
                    coveredSlots.Add(compatibility.SlotId);
                    Assert.That(
                        compatibility.Modifiers,
                        Is.Not.Empty,
                        item.ItemId);
                    foreach (StationObjectStatModifierDefinition modifier in
                             compatibility.Modifiers)
                    {
                        Assert.That(allowedStats, Does.Contain(modifier.Stat));
                        Assert.That(modifier.Value, Is.GreaterThan(0f));
                    }
                }
            }

            Assert.That(
                coveredSlots,
                Is.EquivalentTo(battery.Slots.Select(slot => slot.SlotId)));
        }

        [Test]
        public void StationPowerPrioritiesProtectMoreImportantObjects()
        {
            StationSystemsConfig config = systems.Config;
            int terminal = config.Find(StationSystemType.Terminal).PowerPriority;
            int turret = config.Find(
                StationSystemType.Turret,
                "station_turret_01").PowerPriority;
            int antenna = config.Find(
                StationSystemType.Antenna,
                "station_antenna").PowerPriority;
            int laboratory =
                config.Find(StationSystemType.Laboratory).PowerPriority;
            int drone = config.Find(
                StationSystemType.Drone,
                "station_drone").PowerPriority;

            Assert.That(terminal, Is.GreaterThan(turret));
            Assert.That(turret, Is.GreaterThan(antenna));
            Assert.That(antenna, Is.GreaterThan(laboratory));
            Assert.That(laboratory, Is.GreaterThan(drone));
            Assert.That(
                config.Find(
                    StationSystemType.Turret,
                    "station_turret_02").PowerPriority,
                Is.EqualTo(turret));
        }

        [Test]
        public void NewEqualPriorityObjectTurnsOffPreviouslyPoweredObject()
        {
            energy.RegisterBattery(
                "test_battery",
                1000f,
                1000f,
                0f,
                2f);
            energy.RegisterConsumer(
                "turret_01_load",
                2f,
                0f,
                StationSystemType.Turret,
                "station_turret_01");
            energy.RegisterConsumer(
                "turret_02_load",
                2f,
                0f,
                StationSystemType.Turret,
                "station_turret_02");

            energy.SetConsumerActive("turret_01_load", true);
            energy.SetConsumerActive("turret_02_load", true);

            Assert.That(
                systems.IsRequestedActive(
                    StationSystemType.Turret,
                    "station_turret_01"),
                Is.False);
            Assert.That(
                systems.IsRequestedActive(
                    StationSystemType.Turret,
                    "station_turret_02"),
                Is.True);
            Assert.That(
                energy.IsConsumerPowered("turret_01_load"),
                Is.False);
            Assert.That(
                energy.IsConsumerPowered("turret_02_load"),
                Is.True);
            Assert.That(energy.CurrentConsumption, Is.EqualTo(2f));
        }

        [Test]
        public void EngineeringPartInstallationUsesConfiguredSlotAndStat()
        {
            ItemData emitter = LoadPart("Item_EmitterDamage_01.asset");
            EngineeringPartCompatibility compatibility =
                emitter.FindEngineeringCompatibility(
                    StationSystemType.Turret,
                    "station_turret_01",
                    "Slot_3");
            StationObjectStatModifierDefinition damageModifier =
                compatibility?.Modifiers.Single(modifier =>
                    modifier.Stat == StationObjectStat.Damage);
            float damageBeforeInstallation = systems.GetStat(
                StationSystemType.Turret,
                "station_turret_01",
                StationObjectStat.Damage);
            Assert.That(
                emitter.EngineeringPartDefinition?.InstalledVisualPrefab,
                Is.Not.Null,
                "Installed meshes must come from part config.");
            Assert.That(compatibility, Is.Not.Null);
            Assert.That(damageModifier, Is.Not.Null);
            Assert.That(damageModifier.Mode, Is.EqualTo(StationStatModifierMode.Add));
            Assert.That(
                emitter.FindEngineeringCompatibility(
                    StationSystemType.Turret,
                    "station_turret_01",
                    "Slot_2"),
                Is.Null);

            var requests = new[]
            {
                new StationPartInstallRequest("Slot_3", emitter)
            };
            Assert.That(
                systems.TryInstallParts(
                    StationSystemType.Turret,
                    "station_turret_01",
                    requests,
                    out string reason),
                Is.True,
                reason);
            Assert.That(
                systems.GetInstalledPartItemId(
                    StationSystemType.Turret,
                    "station_turret_01",
                    "slot_3"),
                Is.EqualTo(emitter.ItemId));
            Assert.That(
                systems.GetStat(
                    StationSystemType.Turret,
                    "station_turret_01",
                    StationObjectStat.Damage),
                Is.EqualTo(damageBeforeInstallation + damageModifier.Value));
            Assert.That(
                systems.TryInstallParts(
                    StationSystemType.Turret,
                    "station_turret_01",
                    requests,
                    out _),
                Is.False);
        }

        [Test]
        public void SlotDeclarationsRejectUnknownOrIncompatibleParts()
        {
            ItemData emitter = LoadPart("Item_EmitterDamage_01.asset");
            Assert.That(
                systems.TryInstallParts(
                    StationSystemType.Turret,
                    "station_turret_01",
                    new[]
                    {
                        new StationPartInstallRequest(
                            "Slot_Not_Configured",
                            emitter)
                    },
                    out string unknownReason),
                Is.False);
            Assert.That(unknownReason, Does.Contain("not declared"));

            Assert.That(
                systems.TryInstallParts(
                    StationSystemType.Turret,
                    "station_turret_01",
                    new[]
                    {
                        new StationPartInstallRequest("Slot_2", emitter)
                    },
                    out string incompatibleReason),
                Is.False);
            Assert.That(incompatibleReason, Does.Contain("does not fit"));
        }

        [Test]
        public void TurretsKeepIndependentInstalledParts()
        {
            ItemData emitter = LoadPart("Item_EmitterDamage_01.asset");
            EngineeringPartCompatibility compatibility =
                emitter.FindEngineeringCompatibility(
                    StationSystemType.Turret,
                    "station_turret_02",
                    "Slot_3");
            StationObjectStatModifierDefinition damageModifier =
                compatibility?.Modifiers.Single(modifier =>
                    modifier.Stat == StationObjectStat.Damage);
            float turretOneDamageBefore = systems.GetStat(
                StationSystemType.Turret,
                "station_turret_01",
                StationObjectStat.Damage);
            float turretTwoDamageBefore = systems.GetStat(
                StationSystemType.Turret,
                "station_turret_02",
                StationObjectStat.Damage);

            Assert.That(compatibility, Is.Not.Null);
            Assert.That(damageModifier, Is.Not.Null);
            Assert.That(damageModifier.Mode, Is.EqualTo(StationStatModifierMode.Add));
            Assert.That(
                systems.TryInstallParts(
                    StationSystemType.Turret,
                    "station_turret_02",
                    new[] { new StationPartInstallRequest("Slot_3", emitter) },
                    out string reason),
                Is.True,
                reason);

            Assert.That(
                systems.GetInstalledPartCount(
                    StationSystemType.Turret,
                    "station_turret_01"),
                Is.Zero);
            Assert.That(
                systems.GetInstalledPartCount(
                    StationSystemType.Turret,
                    "station_turret_02"),
                Is.EqualTo(1));
            Assert.That(
                systems.GetStat(
                    StationSystemType.Turret,
                    "station_turret_01",
                    StationObjectStat.Damage),
                Is.EqualTo(turretOneDamageBefore));
            Assert.That(
                systems.GetStat(
                    StationSystemType.Turret,
                    "station_turret_02",
                    StationObjectStat.Damage),
                Is.EqualTo(turretTwoDamageBefore + damageModifier.Value));
        }

        [Test]
        public void RestoreMigratesLegacyPhysicalPartIdByObjectAndSlot()
        {
            systems.Restore(
                new Dictionary<StationSystemType, bool>(),
                new[]
                {
                    new StationObjectSystemState(
                        StationSystemType.Turret,
                        "station_turret_02",
                        true,
                        new[]
                        {
                            new StationInstalledPartState(
                                "Slot_3",
                                "emitter_damage_01")
                        })
                });

            Assert.That(
                systems.GetInstalledPartItemId(
                    StationSystemType.Turret,
                    "station_turret_02",
                    "Slot_3"),
                Is.EqualTo("item_emitter_damage_01"));
            Assert.That(
                systems.GetInstalledPartCount(
                    StationSystemType.Turret,
                    "station_turret_01"),
                Is.Zero);
        }

        [Test]
        public void RestoreRejectsMissingAndIncompatiblePhysicalParts()
        {
            systems.Restore(
                new Dictionary<StationSystemType, bool>(),
                new[]
                {
                    new StationObjectSystemState(
                        StationSystemType.Turret,
                        "station_turret_01",
                        true,
                        new[]
                        {
                            new StationInstalledPartState(
                                "Slot_3",
                                "missing_part"),
                            new StationInstalledPartState(
                                "Slot_2",
                                "item_emitter_damage_01")
                        })
                });

            Assert.That(
                systems.GetInstalledPartCount(
                    StationSystemType.Turret,
                    "station_turret_01"),
                Is.Zero);
        }

        [Test]
        public void RestoreKeepsCriticalBatteryRequestedState()
        {
            systems.Restore(
                new Dictionary<StationSystemType, bool>(),
                new[]
                {
                    new StationObjectSystemState(
                        StationSystemType.Battery,
                        "station_battery",
                        true)
                });

            Assert.That(
                systems.IsRequestedActive(
                    StationSystemType.Battery,
                    "station_battery"),
                Is.True);
        }

        [Test]
        public void UpgradeInteractionServicesThenStartsThenOpensUpgrade()
        {
            GameObject target = new GameObject("Test_UpgradeableTurret");
            target.transform.SetParent(stationRoot.transform);
            StationObjectIdentity identity =
                target.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Turret,
                "station_turret_01");
            MaintainableObject maintenance =
                target.AddComponent<MaintainableObject>();
            target.AddComponent<StationObjectVisual>();
            StationUpgradeableObject upgradeable =
                target.AddComponent<StationUpgradeableObject>();

            maintenance.SetCondition(0.5f);
            Assert.That(
                systems.SetRequestedActive(
                    StationSystemType.Turret,
                    false,
                    "station_turret_01"),
                Is.True);

            InteractionPrompt servicePrompt = upgradeable.GetPrompt();
            Assert.That(servicePrompt.ActionText, Does.Contain("Service"));
            Assert.That(servicePrompt.Mode, Is.EqualTo(NeraInteractionMode.Hold));
            Assert.That(servicePrompt.HoldDuration, Is.GreaterThan(0f));
            upgradeable.CompleteInteraction(null);
            Assert.That(maintenance.IsCleaning, Is.True);

            InteractionPrompt cleaningPrompt = upgradeable.GetPrompt();
            Assert.That(cleaningPrompt.IsVisible, Is.False);
            Assert.That(cleaningPrompt.IsAvailable, Is.False);
            float conditionAtCleaningStart = maintenance.Condition;
            upgradeable.CompleteInteraction(null);
            Assert.That(maintenance.IsCleaning, Is.True);
            Assert.That(
                maintenance.Condition,
                Is.EqualTo(conditionAtCleaningStart));

            maintenance.AdvanceCleaning(
                maintenance.CleaningDurationSeconds);
            Assert.That(maintenance.Condition, Is.EqualTo(1f));
            Assert.That(
                systems.IsRequestedActive(
                    StationSystemType.Turret,
                    "station_turret_01"),
                Is.False,
                "Repair must not also start or open the upgrade screen.");

            InteractionPrompt startPrompt = upgradeable.GetPrompt();
            Assert.That(startPrompt.ActionText, Does.Contain("Start"));
            Assert.That(startPrompt.Mode, Is.EqualTo(NeraInteractionMode.Hold));
            Assert.That(startPrompt.HoldDuration, Is.GreaterThan(0f));
            upgradeable.CompleteInteraction(null);
            Assert.That(
                systems.IsRequestedActive(
                    StationSystemType.Turret,
                    "station_turret_01"),
                Is.True);
            InteractionPrompt upgradePrompt = upgradeable.GetPrompt();
            Assert.That(upgradePrompt.ActionText, Does.Contain("Configure"));
            Assert.That(upgradePrompt.Mode, Is.EqualTo(NeraInteractionMode.Press));
            Assert.That(upgradePrompt.HoldDuration, Is.Zero);
        }

        [Test]
        public void DeviceInteractionIsHiddenUntilCleaningFinishes()
        {
            GameObject target = new GameObject("Test_MaintainableDevice");
            target.transform.SetParent(stationRoot.transform);
            StationObjectIdentity identity =
                target.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Turret,
                "station_turret_01");
            MaintainableObject maintenance =
                target.AddComponent<MaintainableObject>();
            StationDeviceInteractable interactable =
                target.AddComponent<StationDeviceInteractable>();

            maintenance.SetCondition(0.5f);
            Assert.That(interactable.GetPrompt().IsVisible, Is.True);

            interactable.CompleteInteraction(null);
            Assert.That(maintenance.IsCleaning, Is.True);
            Assert.That(interactable.GetPrompt().IsVisible, Is.False);
            Assert.That(interactable.GetPrompt().IsAvailable, Is.False);

            float conditionAtCleaningStart = maintenance.Condition;
            interactable.CompleteInteraction(null);
            Assert.That(maintenance.IsCleaning, Is.True);
            Assert.That(
                maintenance.Condition,
                Is.EqualTo(conditionAtCleaningStart));

            maintenance.AdvanceCleaning(
                maintenance.CleaningDurationSeconds);
            Assert.That(maintenance.IsCleaning, Is.False);
            Assert.That(maintenance.Condition, Is.EqualTo(1f));
        }

        [Test]
        public void EmptyUpgradeFakesAreVisibleOnlyWhileUpgradeModeIsActive()
        {
            GameObject target = new GameObject("Test_UpgradeVisual");
            target.transform.SetParent(stationRoot.transform);
            StationObjectIdentity identity =
                target.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Turret,
                "station_turret_01");

            GameObject slotObject = new GameObject("Slot_N");
            slotObject.transform.SetParent(target.transform);
            StationUpgradeSlot slot =
                slotObject.AddComponent<StationUpgradeSlot>();
            GameObject fake = new GameObject("Fake");
            fake.transform.SetParent(slotObject.transform);
            slot.Configure("slot_id1", fake);

            StationObjectVisual visual =
                target.AddComponent<StationObjectVisual>();
            visual.Configure(true);
            visual.Refresh();

            Assert.That(fake.activeSelf, Is.False);

            visual.SetUpgradeModeActive(true);
            Assert.That(fake.activeSelf, Is.True);

            visual.SetUpgradeModeActive(false);
            Assert.That(fake.activeSelf, Is.False);
        }

        [Test]
        public void UpgradeFakeIsVisibleOnlyWhenCompatiblePartIsAvailable()
        {
            GameObject target = new GameObject("Test_AvailableUpgradeFake");
            target.transform.SetParent(stationRoot.transform);
            StationObjectIdentity identity =
                target.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Turret,
                "station_turret_01");

            GameObject slotObject = new GameObject("Slot_3");
            slotObject.transform.SetParent(target.transform);
            StationUpgradeSlot slot =
                slotObject.AddComponent<StationUpgradeSlot>();
            GameObject fake = new GameObject("Fake");
            fake.transform.SetParent(slotObject.transform);
            slot.Configure("Slot_3", fake);

            StationObjectVisual visual =
                target.AddComponent<StationObjectVisual>();
            visual.Configure(true);
            StationUpgradeableObject upgradeable =
                target.AddComponent<StationUpgradeableObject>();
            StationUpgradeModeController controller =
                stationRoot.AddComponent<StationUpgradeModeController>();
            SetInstanceField(controller, "activeObject", upgradeable);
            SetInstanceField(controller, "inventory", inventory);
            SetInstanceField(controller, "storage", storage);

            controller.RefreshAvailableSlotVisuals();
            upgradeable.SetUpgradeVisualsVisible(true);
            Assert.That(fake.activeSelf, Is.False);

            ItemData emitter = LoadPart("Item_EmitterDamage_01.asset");
            Assert.That(inventory.AddItem(emitter), Is.True);
            controller.RefreshAvailableSlotVisuals();
            Assert.That(fake.activeSelf, Is.True);

            Assert.That(storage.DepositBackpack(inventory), Is.EqualTo(1));
            controller.RefreshAvailableSlotVisuals();
            Assert.That(fake.activeSelf, Is.True,
                "A compatible storage part must also expose the Fake.");

            storage.ResetStorage();
            controller.RefreshAvailableSlotVisuals();
            Assert.That(fake.activeSelf, Is.False);
        }

        [Test]
        public void StagedPartPreviewRemainsVisibleBeforeApply()
        {
            GameObject target = new GameObject("Test_StagedPartPreview");
            target.transform.SetParent(stationRoot.transform);
            StationObjectIdentity identity =
                target.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Turret,
                "station_turret_01");

            GameObject slotObject = new GameObject("Slot_3");
            slotObject.transform.SetParent(target.transform);
            StationUpgradeSlot slot =
                slotObject.AddComponent<StationUpgradeSlot>();
            GameObject fake = new GameObject("Fake");
            fake.transform.SetParent(slotObject.transform);
            slot.Configure("Slot_3", fake);

            StationObjectVisual visual =
                target.AddComponent<StationObjectVisual>();
            visual.Configure(true);
            StationUpgradeableObject upgradeable =
                target.AddComponent<StationUpgradeableObject>();
            StationUpgradeModeController controller =
                stationRoot.AddComponent<StationUpgradeModeController>();
            SetInstanceField(controller, "activeObject", upgradeable);
            SetInstanceField(controller, "inventory", inventory);
            SetInstanceField(controller, "storage", storage);

            ItemData emitter = LoadPart("Item_EmitterDamage_01.asset");
            Assert.That(inventory.AddItem(emitter), Is.True);
            upgradeable.SetUpgradeVisualsVisible(true);
            controller.RefreshAvailableSlotVisuals();

            Assert.That(controller.ToggleSlot(slot), Is.True);
            Assert.That(
                slotObject.transform.Find("Installed_item_emitter_damage_01"),
                Is.Not.Null,
                "The selected part must be previewed before Apply.");
            Assert.That(
                systems.GetInstalledPartItemId(
                    StationSystemType.Turret,
                    "station_turret_01",
                    "Slot_3"),
                Is.Empty,
                "Previewing must not commit the upgrade yet.");
        }

        [Test]
        public void FullyUpgradedObjectHidesAndBlocksUpgradeInteraction()
        {
            GameObject target = new GameObject("Test_FullyUpgradedTurret");
            target.transform.SetParent(stationRoot.transform);
            StationObjectIdentity identity =
                target.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Turret,
                "station_turret_01");
            target.AddComponent<StationObjectVisual>();
            StationUpgradeableObject upgradeable =
                target.AddComponent<StationUpgradeableObject>();

            StationSystemDefinition definition = systems.GetDefinition(
                StationSystemType.Turret,
                "station_turret_01");
            ItemCatalogData catalog = Resources.Load<ItemCatalogData>(
                "ItemCatalog_Default");
            Assert.That(catalog, Is.Not.Null);
            var installed = new List<StationInstalledPartState>();
            foreach (StationObjectSlotDefinition slot in definition.Slots)
            {
                ItemData compatiblePart = catalog.Items.FirstOrDefault(item =>
                    item != null &&
                    item.FindEngineeringCompatibility(
                        StationSystemType.Turret,
                        definition.ObjectId,
                        slot.SlotId) != null);
                Assert.That(
                    compatiblePart,
                    Is.Not.Null,
                    $"No catalog part fits {definition.ObjectId}/" +
                    slot.SlotId);
                installed.Add(new StationInstalledPartState(
                    slot.SlotId,
                    compatiblePart.ItemId));
            }
            systems.Restore(
                new Dictionary<StationSystemType, bool>(),
                new[]
                {
                    new StationObjectSystemState(
                        StationSystemType.Turret,
                        "station_turret_01",
                        true,
                        installed)
                });

            Assert.That(upgradeable.IsFullyUpgraded, Is.True);
            InteractionPrompt prompt = upgradeable.GetPrompt();
            Assert.That(prompt.IsVisible, Is.False);
            Assert.That(prompt.IsAvailable, Is.False);

            upgradeable.CompleteInteraction(playerRoot);
            Assert.That(StationUpgradeModeController.Instance, Is.Null);
        }

        [Test]
        public void ApplyingLastConfiguredPartsClosesUpgradeMode()
        {
            GameObject target = new GameObject("Test_CompleteUpgradeTurret");
            target.transform.SetParent(stationRoot.transform);
            StationObjectIdentity identity =
                target.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Turret,
                "station_turret_01");

            string[] slotIds =
            {
                "Slot_1", "Slot_2", "Slot_3",
                "Slot_4", "Slot_5", "Slot_6"
            };
            foreach (string slotId in slotIds)
            {
                GameObject slotObject = new GameObject(slotId);
                slotObject.transform.SetParent(target.transform);
                StationUpgradeSlot slot =
                    slotObject.AddComponent<StationUpgradeSlot>();
                GameObject fake = new GameObject("Fake");
                fake.transform.SetParent(slotObject.transform);
                slot.Configure(slotId, fake);
            }

            StationObjectVisual visual =
                target.AddComponent<StationObjectVisual>();
            visual.Configure(true);
            StationUpgradeableObject upgradeable =
                target.AddComponent<StationUpgradeableObject>();
            StationUpgradeModeController controller =
                stationRoot.AddComponent<StationUpgradeModeController>();
            SetInstanceField(controller, "activeObject", upgradeable);
            SetInstanceField(controller, "inventory", inventory);
            SetInstanceField(controller, "storage", storage);

            string[] partFiles =
            {
                "Item_Chassis_01.asset",
                "Item_Cooling_01.asset",
                "Item_EmitterDamage_01.asset",
                "Item_Sensor_01.asset",
                "Item_Servo_01.asset",
                "Item_ServoDrive_01.asset"
            };
            foreach (string partFile in partFiles)
                Assert.That(inventory.AddItem(LoadPart(partFile)), Is.True);

            foreach (string slotId in slotIds)
            {
                Assert.That(
                    controller.ToggleSlot(upgradeable.FindSlot(slotId)),
                    Is.True,
                    slotId);
            }

            controller.Apply();

            Assert.That(upgradeable.IsFullyUpgraded, Is.True);
            Assert.That(controller.IsOpen, Is.False);
            Assert.That(upgradeable.GetPrompt().IsVisible, Is.False);
        }

        [Test]
        public void InstalledUpgradePartKeepsInvisibleSlotHitbox()
        {
            GameObject target = new GameObject("Test_UpgradeHitbox");
            target.transform.SetParent(stationRoot.transform);
            StationObjectIdentity identity =
                target.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Turret,
                "station_turret_01");

            GameObject slotObject = new GameObject("Slot_3");
            slotObject.transform.SetParent(target.transform);
            StationUpgradeSlot slot =
                slotObject.AddComponent<StationUpgradeSlot>();
            GameObject fake = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fake.name = "Fake";
            fake.transform.SetParent(slotObject.transform);
            slot.Configure("Slot_3", fake);

            StationObjectVisual visual =
                target.AddComponent<StationObjectVisual>();
            visual.Configure(true);
            visual.SetUpgradeModeActive(true);
            slot.ShowPart(LoadPart("Item_EmitterDamage_01.asset"));

            Assert.That(fake.activeSelf, Is.True);
            Assert.That(fake.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(fake.GetComponent<Collider>().enabled, Is.True);

            visual.SetUpgradeModeActive(false);
            Assert.That(fake.activeSelf, Is.False);
        }

        [Test]
        public void TurretAimGateRejectsSidewaysTarget()
        {
            GameObject turretRoot = new GameObject("Test_TurretAimGate");
            turretRoot.transform.SetParent(stationRoot.transform);
            StationObjectIdentity identity =
                turretRoot.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Turret,
                "station_turret_01");

            Transform pivot = new GameObject("YawPivot").transform;
            pivot.SetParent(turretRoot.transform, false);
            Transform muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(pivot, false);

            StationTurretController turret =
                turretRoot.AddComponent<StationTurretController>();
            SerializedObject serializedTurret = new SerializedObject(turret);
            serializedTurret.FindProperty("yawPivot").objectReferenceValue = pivot;
            serializedTurret.FindProperty("muzzle").objectReferenceValue = muzzle;
            serializedTurret.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(turret.EffectiveAimTolerance, Is.EqualTo(5f));
            Assert.That(
                turret.IsMuzzleAimedAt(muzzle.position + Vector3.right * 10f),
                Is.False);

            pivot.rotation = Quaternion.Euler(0f, 90f, 0f);
            Assert.That(
                turret.IsMuzzleAimedAt(muzzle.position + Vector3.right * 10f),
                Is.True);
        }

        [Test]
        public void UpgradeSlotRaycastIgnoresOwnBodyButStopsAtForeignCollider()
        {
            GameObject targetRoot = new GameObject("Test_UpgradeClickTarget");
            targetRoot.transform.SetParent(stationRoot.transform);
            targetRoot.transform.position = Vector3.forward * 3f;
            StationObjectIdentity identity =
                targetRoot.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Turret,
                "station_turret_01");
            BoxCollider bodyCollider = targetRoot.AddComponent<BoxCollider>();
            bodyCollider.size = new Vector3(2f, 2f, 1f);

            GameObject slotObject = new GameObject("Slot_3");
            slotObject.transform.SetParent(targetRoot.transform, false);
            slotObject.transform.localPosition = Vector3.forward;
            BoxCollider slotCollider = slotObject.AddComponent<BoxCollider>();
            slotCollider.size = Vector3.one * 0.5f;
            StationUpgradeSlot slot =
                slotObject.AddComponent<StationUpgradeSlot>();
            slot.Configure("Slot_3", null);

            StationObjectVisual visual =
                targetRoot.AddComponent<StationObjectVisual>();
            visual.Configure(true);
            StationUpgradeableObject target =
                targetRoot.AddComponent<StationUpgradeableObject>();
            Physics.SyncTransforms();

            Ray ray = new Ray(Vector3.zero, Vector3.forward);
            Assert.That(
                Physics.Raycast(
                    ray,
                    out RaycastHit firstHit,
                    10f,
                    ~0,
                    QueryTriggerInteraction.Collide),
                Is.True);
            Assert.That(firstHit.collider, Is.EqualTo(bodyCollider));
            Assert.That(
                StationUpgradeModeController.FindSlotHit(ray, target, 10f),
                Is.EqualTo(slot));

            GameObject blocker = new GameObject("Test_ForeignBlocker");
            blocker.transform.SetParent(stationRoot.transform);
            blocker.transform.position = Vector3.forward;
            blocker.AddComponent<BoxCollider>();
            Physics.SyncTransforms();

            Assert.That(
                StationUpgradeModeController.FindSlotHit(ray, target, 10f),
                Is.Null,
                "Upgrade clicks must not pass through unrelated colliders.");
        }

        [Test]
        public void SessionEndReturnsUnappliedPartToInventory()
        {
            ItemData emitter = LoadPart("Item_EmitterDamage_01.asset");
            Assert.That(inventory.AddItem(emitter), Is.True);
            ItemInstance original = inventory.BackpackItemInstances[0];

            GameObject targetRoot = new GameObject("Test_UpgradeRollback");
            targetRoot.transform.SetParent(stationRoot.transform);
            StationObjectIdentity identity =
                targetRoot.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Turret,
                "station_turret_01");

            GameObject slotObject = new GameObject("Slot_3");
            slotObject.transform.SetParent(targetRoot.transform);
            StationUpgradeSlot slot =
                slotObject.AddComponent<StationUpgradeSlot>();
            slot.Configure("Slot_3", null);

            StationObjectVisual visual =
                targetRoot.AddComponent<StationObjectVisual>();
            visual.Configure(true);
            StationUpgradeableObject upgradeable =
                targetRoot.AddComponent<StationUpgradeableObject>();
            StationUpgradeModeController controller =
                stationRoot.AddComponent<StationUpgradeModeController>();
            SetInstanceField(controller, "activeObject", upgradeable);
            SetInstanceField(controller, "inventory", inventory);
            SetInstanceField(controller, "storage", storage);

            Assert.That(controller.ToggleSlot(slot), Is.True);
            Assert.That(inventory.Count, Is.Zero);

            Assert.That(controller.ToggleSlot(slot), Is.True);
            Assert.That(inventory.BackpackItemInstances, Does.Contain(original));

            Assert.That(controller.ToggleSlot(slot), Is.True);
            Assert.That(inventory.Count, Is.Zero);

            controller.PrepareForSessionEnd();

            Assert.That(inventory.Count, Is.EqualTo(1));
            Assert.That(inventory.BackpackItemInstances, Does.Contain(original));
            Assert.That(controller.IsOpen, Is.False);
        }

        [Test]
        public void UpgradeModeKeepsStagedPartWhenAllReturnSlotsAreFull()
        {
            ItemData emitter = LoadPart("Item_EmitterDamage_01.asset");
            Assert.That(inventory.AddItem(emitter), Is.True);
            ItemInstance original = inventory.BackpackItemInstances[0];

            GameObject targetRoot = new GameObject("Test_LosslessRollback");
            targetRoot.transform.SetParent(stationRoot.transform);
            StationObjectIdentity identity =
                targetRoot.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Turret,
                "station_turret_01");
            GameObject slotObject = new GameObject("Slot_3");
            slotObject.transform.SetParent(targetRoot.transform);
            StationUpgradeSlot slot =
                slotObject.AddComponent<StationUpgradeSlot>();
            slot.Configure("Slot_3", null);
            targetRoot.AddComponent<StationObjectVisual>().Configure(true);
            StationUpgradeableObject upgradeable =
                targetRoot.AddComponent<StationUpgradeableObject>();
            StationUpgradeModeController controller =
                stationRoot.AddComponent<StationUpgradeModeController>();
            SetInstanceField(controller, "activeObject", upgradeable);
            SetInstanceField(controller, "inventory", inventory);
            SetInstanceField(controller, "storage", storage);

            Assert.That(controller.ToggleSlot(slot), Is.True);
            while (inventory.AddItem(emitter))
            {
            }
            storage.ConfigureCapacities(1, 1, 1);
            Assert.That(
                storage.Deposit(ItemInstance.Create(emitter)),
                Is.True);

            LogAssert.Expect(
                LogType.Error,
                "Could not return staged part 'item_emitter_damage_01'.");
            LogAssert.Expect(
                LogType.Error,
                "Station upgrade mode cannot close because a staged part " +
                "has no free inventory or storage slot.");
            controller.Close();

            Assert.That(controller.IsOpen, Is.True);
            Assert.That(
                inventory.BackpackItemInstances.Any(item =>
                    ReferenceEquals(item, original)),
                Is.False);

            Assert.That(
                inventory.RemoveInstanceAt(
                    InventorySlotGroup.Backpack,
                    0,
                    out _),
                Is.True);
            controller.Close();

            Assert.That(controller.IsOpen, Is.False);
            Assert.That(
                inventory.BackpackItemInstances.Any(item =>
                    ReferenceEquals(item, original)),
                Is.True);
        }

        [Test]
        public void ExplicitDepositMovesInventoryInstanceIntoStationStorage()
        {
            ItemData part = LoadPart("Item_ServoDrive_01.asset");
            Assert.That(inventory.AddItem(part), Is.True);
            ItemInstance original = inventory.GetItemInstance(
                InventorySlotGroup.Backpack,
                0);

            Assert.That(storage.DepositBackpack(inventory), Is.EqualTo(1));
            Assert.That(storage.BackpackSlots, Does.Contain(original));
            Assert.That(inventory.Count, Is.Zero);
        }

        private static ItemData LoadPart(string fileName)
        {
            string searchRoot =
                "Assets/_Project/NERA/Configs/Items/Item_EngineeringPart";
            string itemName = Path.GetFileNameWithoutExtension(fileName);
            string[] guids = AssetDatabase.FindAssets(
                $"{itemName} t:ItemData",
                new[] { searchRoot });
            ItemData item = guids.Length > 0
                ? AssetDatabase.LoadAssetAtPath<ItemData>(
                    AssetDatabase.GUIDToAssetPath(guids[0]))
                : null;
            Assert.That(item, Is.Not.Null, fileName);
            return item;
        }

        private static void SetSingleton(System.Type controllerType, object value)
        {
            PropertyInfo instanceProperty = controllerType.GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public);
            instanceProperty?.GetSetMethod(true)?.Invoke(null, new[] { value });
        }

        private static void SetInstanceField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
