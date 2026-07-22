using System;
using System.Collections;
using NERA.Core;
using NERA.Drone;
using NERA.Energy;
using NERA.Expeditions;
using NERA.Inventory;
using NERA.Items;
using NERA.Research;
using NERA.Save;
using NERA.Station;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NERA.Tests
{
    public sealed class StationEnergySceneLifecycleTests
    {
        [UnityTearDown]
        public IEnumerator TearDownPersistentBootRoot()
        {
            BootInitializer boot =
                Object.FindFirstObjectByType<BootInitializer>();
            if (boot != null)
                Object.Destroy(boot.gameObject);

            yield return null;
        }

        [UnityTest]
        public IEnumerator LaboratoryIsUnavailableUntilGridStarts()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            LaboratoryTableInteractable laboratory =
                UnityEngine.Object.FindFirstObjectByType<LaboratoryTableInteractable>();

            Assert.That(energy, Is.Not.Null);
            Assert.That(laboratory, Is.Not.Null);

            energy.RestoreState(energy.TotalCapacity, false);
            Assert.That(laboratory.GetPrompt().IsAvailable, Is.False);

            energy.SetGridEnabled(true);
            Assert.That(laboratory.GetPrompt().IsAvailable, Is.True);
        }

        [UnityTest]
        public IEnumerator ReturningToStationDoesNotDuplicateEnergySources()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            StationEnvironmentController environment =
                StationEnvironmentController.Instance;

            Assert.That(energy, Is.Not.Null);
            Assert.That(environment, Is.Not.Null);

            environment.SetTime(12f);
            environment.SetWeather(StationWeather.Clear);
            energy.AdvanceSimulation(0.1f);

            Assert.That(energy.TotalCapacity, Is.EqualTo(1000f));
            Assert.That(
                energy.CurrentGeneration,
                Is.EqualTo(energy.Config.ClearDayGeneration).Within(0.01f)
            );

            SceneManager.LoadScene("Expedition_01");
            yield return WaitForScene("Expedition_01");

            SceneManager.LoadScene("Player_Station");
            yield return WaitForScene("Player_Station");
            yield return null;

            environment.SetTime(12f);
            environment.SetWeather(StationWeather.Clear);
            energy.AdvanceSimulation(0.1f);

            Assert.That(energy.TotalCapacity, Is.EqualTo(1000f));
            Assert.That(
                energy.CurrentGeneration,
                Is.EqualTo(energy.Config.ClearDayGeneration).Within(0.01f)
            );
        }

        [UnityTest]
        public IEnumerator DroneCanSurveySecondLocationAfterRecharge()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            DroneScanController drone = DroneScanController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;

            Assert.That(energy, Is.Not.Null);
            Assert.That(discovery, Is.Not.Null);
            Assert.That(drone, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);
            Assert.That(discovery.KnownLocations.Count, Is.GreaterThanOrEqualTo(2));

            ExpeditionLocationData first = discovery.KnownLocations[0];
            ExpeditionLocationData second = discovery.KnownLocations[1];
            discovery.RestoreDiscovered(Array.Empty<string>());
            energy.RestoreState(energy.TotalCapacity, true);
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Drone, true),
                Is.True);
            drone.RefreshAvailability();
            yield return null;

            Assert.That(drone.LaunchScan(first), Is.True);
            drone.AdvanceScan(first.DroneScanDuration);
            Assert.That(discovery.IsDiscovered(first), Is.True);
            Assert.That(drone.IsCharging, Is.True);
            Assert.That(drone.CanLaunchScan(second), Is.False);

            drone.AdvanceRecharge(energy.Config.DroneRechargeDuration);

            Assert.That(drone.State, Is.EqualTo(DroneState.Ready));
            Assert.That(drone.IsCharging, Is.False);
            Assert.That(drone.LaunchScan(second), Is.False,
                "A distant expedition must remain locked before the drone upgrade.");

            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>();
            ItemCatalogData catalog = Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData servoDrive = catalog != null ? catalog.Find("servo_drive_01") : null;
            Assert.That(systems, Is.Not.Null);
            Assert.That(inventory, Is.Not.Null);
            Assert.That(servoDrive, Is.Not.Null);
            Assert.That(inventory.AddItem(servoDrive), Is.True);
            Assert.That(
                systems.TryUpgrade(
                    StationSystemType.Drone,
                    inventory,
                    StationStorageController.Instance),
                Is.True);

            Assert.That(drone.LaunchScan(second), Is.True);
            Assert.That(drone.ScanLocation, Is.EqualTo(second));
        }

        [UnityTest]
        public IEnumerator DroneCannotBeStoppedWhileScanningAndStopButtonIsHidden()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            DroneScanController drone = DroneScanController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            Terminal.StationPanelController panel =
                Object.FindFirstObjectByType<Terminal.StationPanelController>(
                    FindObjectsInactive.Include);

            Assert.That(energy, Is.Not.Null);
            Assert.That(discovery, Is.Not.Null);
            Assert.That(drone, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);
            Assert.That(panel, Is.Not.Null);
            Assert.That(discovery.KnownLocations.Count, Is.GreaterThan(0));

            ExpeditionLocationData location = discovery.KnownLocations[0];
            discovery.RestoreDiscovered(Array.Empty<string>());
            energy.RestoreState(energy.TotalCapacity, true);
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Drone, true),
                Is.True);
            drone.RefreshAvailability();
            Assert.That(drone.LaunchScan(location), Is.True);

            Transform systemsTabTransform = panel.transform.Find("SystemsTabButton");
            Transform droneSlotTransform = Array.Find(
                panel.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name == "Slot_Dron");
            Assert.That(systemsTabTransform, Is.Not.Null, "SystemsTabButton not found.");
            Assert.That(droneSlotTransform, Is.Not.Null, "Slot_Dron not found.");
            Button systemsTabButton = systemsTabTransform.GetComponent<Button>();
            Button droneSlotButton = droneSlotTransform.GetComponent<Button>();
            Assert.That(systemsTabButton, Is.Not.Null, "Systems tab has no Button.");
            Assert.That(droneSlotButton, Is.Not.Null, "Drone slot has no Button.");
            systemsTabButton.onClick.Invoke();
            droneSlotButton.onClick.Invoke();
            panel.RefreshAll();

            Button powerButton = Array.Find(
                panel.GetComponentsInChildren<Button>(true),
                button => button.name == "Stop\\StartTabButton");
            Assert.That(powerButton, Is.Not.Null, "Stop/Start button not found.");
            Assert.That(powerButton.gameObject.activeSelf, Is.False);
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Drone, false),
                Is.False);
            Assert.That(
                systems.IsRequestedActive(StationSystemType.Drone),
                Is.True);
            Assert.That(drone.State, Is.EqualTo(DroneState.Scanning));
        }

        [UnityTest]
        public IEnumerator BackpackUsesConfiguredAuthoredSlotPoints()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            InventoryLabHUDController hud = InventoryLabHUDController.Instance;
            PlayerInventory inventory =
                Object.FindFirstObjectByType<PlayerInventory>();

            Assert.That(hud, Is.Not.Null);
            Assert.That(inventory, Is.Not.Null);
            Assert.That(inventory.Config, Is.Not.Null);

            Transform content = hud.transform.Find(
                "InventoryPanel/Backpack/Scroll View/Viewport/Content"
            );
            Assert.That(content, Is.Not.Null);

            for (int i = 0; i < InventoryConfig.MaxBackpackCapacity; i++)
            {
                Transform spawnPoint = content.Find($"Slot_{i + 1}");
                Assert.That(spawnPoint, Is.Not.Null);
                Assert.That(
                    spawnPoint.gameObject.activeSelf,
                    Is.EqualTo(i < inventory.BackpackCapacity)
                );

                if (i < inventory.BackpackCapacity)
                {
                    Assert.That(
                        spawnPoint.GetComponentInChildren<InventorySlotView>(true),
                        Is.Not.Null
                    );
                }
            }
        }

        [UnityTest]
        public IEnumerator DevicesPanelMatchesTypedStationStorageCapacities()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            StationStorageController storage = StationStorageController.Instance;
            Terminal.StationPanelController panel =
                Object.FindFirstObjectByType<Terminal.StationPanelController>(
                    FindObjectsInactive.Include);

            Assert.That(storage, Is.Not.Null);
            Assert.That(panel, Is.Not.Null);
            Assert.That(storage.BackpackSlots.Count, Is.EqualTo(10));
            Assert.That(storage.QuickAccessSlots.Count, Is.EqualTo(4));
            Assert.That(storage.AnomalySlots.Count, Is.EqualTo(7));

            Transform devices = panel.transform.Find("DevicesPanel");
            Assert.That(devices, Is.Not.Null);
            Assert.That(
                CountDirectSlotButtons(devices.Find("Background_Backpack")),
                Is.EqualTo(storage.BackpackSlots.Count));
            Assert.That(
                CountDirectSlotButtons(devices.Find("Background_QuickAccess")),
                Is.EqualTo(storage.QuickAccessSlots.Count));
            Assert.That(
                CountDirectSlotButtons(devices.Find("Background_Anomaly")),
                Is.EqualTo(storage.AnomalySlots.Count));
        }

        [UnityTest]
        public IEnumerator DevicesTabShowsInventoryAndOtherTabsHideIt()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            Terminal.StationPanelController panel =
                Object.FindFirstObjectByType<Terminal.StationPanelController>(
                    FindObjectsInactive.Include);
            InventoryLabHUDController inventoryHud = InventoryLabHUDController.Instance;
            Assert.That(panel, Is.Not.Null);
            Assert.That(inventoryHud, Is.Not.Null);

            Button devicesButton = panel.transform.Find("DevicesTabButton")
                .GetComponent<Button>();
            Button statusButton = panel.transform.Find("StatusTabButton")
                .GetComponent<Button>();
            Transform inventoryPanel = inventoryHud.transform.Find("InventoryPanel");

            devicesButton.onClick.Invoke();
            Assert.That(
                panel.transform.Find("DevicesPanel").gameObject.activeSelf,
                Is.True);
            Assert.That(inventoryPanel.gameObject.activeSelf, Is.True);

            statusButton.onClick.Invoke();
            Assert.That(inventoryPanel.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator InventoryItemCanBeDraggedIntoStationStorageThroughUi()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>();
            StationStorageController storage = StationStorageController.Instance;
            Terminal.StationPanelController panel =
                Object.FindFirstObjectByType<Terminal.StationPanelController>(
                    FindObjectsInactive.Include);
            ItemCatalogData catalog = Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData item = catalog != null ? catalog.Find("servo_drive_01") : null;

            Assert.That(inventory, Is.Not.Null);
            Assert.That(storage, Is.Not.Null);
            Assert.That(panel, Is.Not.Null);
            Assert.That(item, Is.Not.Null);
            inventory.RestoreItemInstances(Array.Empty<ItemInstance>());
            storage.ResetStorage();
            Assert.That(inventory.AddItem(item), Is.True);

            Transform terminalScreen = panel.transform.parent;
            terminalScreen.gameObject.SetActive(true);
            CanvasGroup terminalCanvasGroup = terminalScreen.GetComponent<CanvasGroup>();
            terminalCanvasGroup.alpha = 1f;
            terminalCanvasGroup.interactable = true;
            terminalCanvasGroup.blocksRaycasts = true;
            panel.transform.Find("DevicesTabButton").GetComponent<Button>().onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();

            LaboratoryInventoryItemDrag source = null;
            foreach (LaboratoryInventoryItemDrag drag in
                     InventoryLabHUDController.Instance.GetComponentsInChildren<
                         LaboratoryInventoryItemDrag>(true))
            {
                if (drag.Item == item && !drag.IsStationStorageSource)
                {
                    source = drag;
                    break;
                }
            }

            LaboratoryItemDropSlot destination = panel.transform
                .Find("DevicesPanel/Background_Backpack/Slot_1")
                .GetComponent<LaboratoryItemDropSlot>();
            Image destinationIcon = destination.transform.Find("Icon")
                .GetComponent<Image>();
            Assert.That(source, Is.Not.Null, "Occupied inventory slot has no drag source.");
            Assert.That(destination, Is.Not.Null, "Storage slot has no drop target.");
            Assert.That(destinationIcon, Is.Not.Null);
            Assert.That(source.SourceGroup, Is.EqualTo(InventorySlotGroup.Backpack));
            Assert.That(source.SourceIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(source.IsStationStorageSource, Is.False);
            Assert.That(source.IsLaboratorySource, Is.False);
            Assert.That(source.IsChargingSource, Is.False);
            Assert.That(storage.BackpackSlots.Count, Is.EqualTo(10));
            Assert.That(
                PlayerInventory.GetSlotGroup(item.ItemType),
                Is.EqualTo(InventorySlotGroup.Backpack));
            Assert.That(
                inventory.GetItemInstance(source.SourceGroup, source.SourceIndex)?.ItemData,
                Is.SameAs(item));
            Assert.That(
                destination.ItemDropped,
                Is.Not.Null,
                "Storage slot was created without StationPanelController callback.");

            RectTransform destinationRect = (RectTransform)destination.transform;
            Canvas canvas = destination.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                destinationRect.TransformPoint(destinationRect.rect.center));
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = screenPoint,
                pointerDrag = source.gameObject
            };

            source.OnBeginDrag(pointer);
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            RaycastResult dropHit = hits.Find(hit =>
                hit.gameObject.GetComponentInParent<LaboratoryItemDropSlot>() != null);
            string raycastStack = string.Join(
                "\n",
                hits.ConvertAll(hit => GetHierarchyPath(hit.gameObject.transform)));
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            string raycastContext =
                $"point={screenPoint}, screen={Screen.width}x{Screen.height}, " +
                $"destinationActive={destination.gameObject.activeInHierarchy}, " +
                $"canvasActive={canvas.gameObject.activeInHierarchy}, " +
                $"canvasEnabled={canvas.enabled}, " +
                $"raycasterEnabled={raycaster != null && raycaster.enabled}";
            Assert.That(
                dropHit.gameObject,
                Is.Not.Null,
                "No storage drop target is reachable by the UI raycaster. " +
                raycastContext + " Hits:\n" + raycastStack);
            Assert.That(
                dropHit.gameObject.GetComponentInParent<LaboratoryItemDropSlot>(),
                Is.SameAs(destination));

            bool dropEventReached = false;
            destination.ItemDropped += _ => dropEventReached = true;
            ExecuteEvents.ExecuteHierarchy(
                dropHit.gameObject,
                pointer,
                ExecuteEvents.dropHandler);
            source.OnEndDrag(pointer);

            Assert.That(dropEventReached, Is.True, "Storage drop callback was not invoked.");
            Assert.That(storage.Count, Is.EqualTo(1));
            Assert.That(destinationIcon.enabled, Is.True);
            Assert.That(destinationIcon.sprite, Is.SameAs(item.Icon));
            Assert.That(
                inventory.GetItemInstance(source.SourceGroup, source.SourceIndex),
                Is.Null);
        }

        [UnityTest]
        public IEnumerator ReturningToStationDoesNotMovePlayerItemsIntoStorage()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>();
            StationStorageController storage = StationStorageController.Instance;
            ItemCatalogData catalog = Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData servoDrive = catalog != null ? catalog.Find("servo_drive_01") : null;
            Assert.That(inventory, Is.Not.Null);
            Assert.That(storage, Is.Not.Null);
            Assert.That(servoDrive, Is.Not.Null);

            int storedBefore = storage.Count;
            Assert.That(inventory.AddItem(servoDrive), Is.True);

            SceneManager.LoadScene("Expedition_01");
            yield return WaitForScene("Expedition_01");
            SceneManager.LoadScene("Player_Station");
            yield return WaitForScene("Player_Station");
            yield return null;

            Assert.That(inventory.Contains("servo_drive_01"), Is.True);
            Assert.That(storage.Count, Is.EqualTo(storedBefore));
        }

        private static int CountDirectSlotButtons(Transform root)
        {
            Assert.That(root, Is.Not.Null);
            int count = 0;
            foreach (Button button in root.GetComponentsInChildren<Button>(true))
            {
                if (button.transform.parent == root &&
                    button.name.StartsWith("Slot_", StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        private static string GetHierarchyPath(Transform target)
        {
            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = target.name + "/" + path;
            }

            return path;
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            while (SceneManager.GetActiveScene().name != sceneName)
                yield return null;
        }

        private static IEnumerator DisablePersistenceForTest()
        {
            SaveGameController save =
                Object.FindFirstObjectByType<SaveGameController>();
            if (save != null)
                Object.Destroy(save);

            yield return null;
        }
    }
}
