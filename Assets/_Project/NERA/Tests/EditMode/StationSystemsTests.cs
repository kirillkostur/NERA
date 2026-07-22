using NERA.Expeditions;
using NERA.Inventory;
using NERA.Items;
using NERA.Station;
using NUnit.Framework;
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
        private PlayerInventory inventory;
        private ItemData servoDrive;

        [SetUp]
        public void SetUp()
        {
            stationRoot = new GameObject("Test_StationSystems");
            storage = stationRoot.AddComponent<StationStorageController>();
            systems = stationRoot.AddComponent<StationSystemsController>();

            playerRoot = new GameObject("Test_StationPlayer");
            inventory = playerRoot.AddComponent<PlayerInventory>();

            servoDrive = ScriptableObject.CreateInstance<ItemData>();
            SerializedObject serializedItem = new SerializedObject(servoDrive);
            serializedItem.FindProperty("itemId").stringValue = "servo_drive_01";
            serializedItem.FindProperty("displayName").stringValue = "Servo Drive";
            serializedItem.FindProperty("itemType").enumValueIndex =
                (int)ItemType.EngineeringPart;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(servoDrive);
            Object.DestroyImmediate(playerRoot);
            Object.DestroyImmediate(stationRoot);
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
            serializedLocation.FindProperty("requiredDroneUpgradeLevel").intValue = 1;
            serializedLocation.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(systems.CanDroneReach(distant), Is.False);
            Assert.That(
                systems.TryUpgrade(StationSystemType.Drone, inventory, storage),
                Is.True);
            Assert.That(storage.Count, Is.Zero);
            Assert.That(systems.GetUpgradeLevel(StationSystemType.Drone), Is.EqualTo(1));
            Assert.That(systems.CanDroneReach(distant), Is.True);

            Object.DestroyImmediate(distant);
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
