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
        public void StationConfigDefinesFivePhysicalUpgradeObjects()
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

            foreach (StationSystemDefinition definition in
                     config.StationObjects.Where(item =>
                         item.SystemType == StationSystemType.Turret ||
                         item.SystemType == StationSystemType.Antenna ||
                         item.SystemType == StationSystemType.Drone ||
                         item.SystemType == StationSystemType.Battery))
            {
                Assert.That(definition.ObjectId, Is.Not.Empty);
                Assert.That(definition.Slots, Is.Not.Empty, definition.ObjectId);
                Assert.That(definition.BaseStats, Is.Not.Empty, definition.ObjectId);
            }
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
        public void RestoreKeepsPhysicalPartsByObjectAndSlot()
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
                Is.EqualTo("emitter_damage_01"));
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
