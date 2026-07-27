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

            StationBattery[] batteries = Object.FindObjectsByType<StationBattery>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            SolarPanelInteractable[] solarPanels =
                Object.FindObjectsByType<SolarPanelInteractable>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            Assert.That(batteries.Length, Is.EqualTo(2));
            Assert.That(energy.TotalCapacity, Is.EqualTo(2000f));
            Assert.That(
                energy.CurrentGeneration,
                Is.EqualTo(
                    energy.Config.ClearDayGeneration * solarPanels.Length)
                    .Within(0.01f)
            );

            SceneManager.LoadScene("Expedition_01");
            yield return WaitForScene("Expedition_01");

            SceneManager.LoadScene("Player_Station");
            yield return WaitForScene("Player_Station");
            yield return null;

            environment.SetTime(12f);
            environment.SetWeather(StationWeather.Clear);
            energy.AdvanceSimulation(0.1f);

            solarPanels = Object.FindObjectsByType<SolarPanelInteractable>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(energy.TotalCapacity, Is.EqualTo(2000f));
            Assert.That(
                energy.CurrentGeneration,
                Is.EqualTo(
                    energy.Config.ClearDayGeneration * solarPanels.Length)
                    .Within(0.01f)
            );
        }

        [UnityTest]
        public IEnumerator StationTabsAndSystemTogglesAreIndependent()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            Terminal.TerminalUIScreen terminal = Terminal.TerminalUIScreen.Instance;
            Terminal.TerminalStationScreenController stationScreen =
                Object.FindFirstObjectByType<
                    Terminal.TerminalStationScreenController>(
                    FindObjectsInactive.Include);

            Assert.That(energy, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);
            Assert.That(terminal, Is.Not.Null);
            Assert.That(stationScreen, Is.Not.Null);

            energy.RestoreState(energy.TotalCapacity, true);
            systems.SetCriticalSystemActive(StationSystemType.Computer, true);
            terminal.Open();
            terminal.ShowStation();

            Transform statusPanel = stationScreen.transform.Find(
                "background_Status");
            Transform upgradePanel = stationScreen.transform.Find(
                "background_Upgrade");
            Transform stationStatusTextTransform = Array.Find(
                statusPanel.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name == "Text_description");
            Assert.That(stationStatusTextTransform, Is.Not.Null);
            Component stationStatusText = stationStatusTextTransform
                .GetComponent("TextMeshProUGUI");
            Assert.That(stationStatusText, Is.Not.Null);
            Assert.That(statusPanel.gameObject.activeSelf, Is.True,
                "Station must open on the status tab before an object is selected.");
            Assert.That(upgradePanel.gameObject.activeSelf, Is.False);

            Transform powerSwitch = Array.Find(
                stationScreen.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name == "Toggle");
            Assert.That(powerSwitch, Is.Not.Null);
            Button onButton = powerSwitch.Find("OnButton").GetComponent<Button>();
            Button offButton = powerSwitch.Find("OffButton").GetComponent<Button>();
            RectTransform powerHandle =
                (RectTransform)powerSwitch.Find("Handle");
            Component powerStatus = powerSwitch.Find("Text_Status")
                .GetComponent("TextMeshProUGUI");

            stationScreen.SelectSystem(StationSystemType.Laboratory);
            yield return null;
            Assert.That(onButton.gameObject.activeSelf, Is.True);
            Assert.That(powerHandle.anchoredPosition.x, Is.EqualTo(25f).Within(0.1f));
            Assert.That(
                powerStatus.GetType().GetProperty("text")?.GetValue(powerStatus),
                Is.EqualTo("Active"));
            Assert.That(
                stationStatusText.GetType().GetProperty("text")
                    ?.GetValue(stationStatusText)?.ToString(),
                Does.Contain(
                    $"Consumption - {energy.Config.LaboratoryConsumption:0.0}"));
            onButton.onClick.Invoke();
            Assert.That(
                systems.IsRequestedActive(StationSystemType.Laboratory),
                Is.False);
            Assert.That(offButton.gameObject.activeSelf, Is.True);
            Assert.That(
                powerStatus.GetType().GetProperty("text")?.GetValue(powerStatus),
                Is.EqualTo("Inactive"));

            stationScreen.SelectSystem(StationSystemType.Charger);
            yield return null;
            Assert.That(onButton.gameObject.activeSelf, Is.True,
                "Charger must keep its own state when laboratory is stopped.");
            Assert.That(powerHandle.anchoredPosition.x, Is.EqualTo(25f).Within(0.1f),
                "Selecting an active system must move the handle to ON.");
            Assert.That(
                powerStatus.GetType().GetProperty("text")?.GetValue(powerStatus),
                Is.EqualTo("Active"));
            Assert.That(
                stationStatusText.GetType().GetProperty("text")
                    ?.GetValue(stationStatusText)?.ToString(),
                Does.Contain(
                    $"Consumption - {energy.Config.ItemChargingConsumption:0.0}"));
            stationScreen.SelectSystem(StationSystemType.Laboratory);
            yield return null;
            Assert.That(offButton.gameObject.activeSelf, Is.True);
            Assert.That(powerHandle.anchoredPosition.x, Is.EqualTo(-25f).Within(0.1f),
                "Returning to an inactive system must move the handle to OFF.");
            Assert.That(
                powerStatus.GetType().GetProperty("text")?.GetValue(powerStatus),
                Is.EqualTo("Inactive"));

            Button statusButton = stationScreen.transform.Find(
                "StatusMapButton").GetComponent<Button>();
            Button upgradeButton = stationScreen.transform.Find(
                "UpgradesMapButton").GetComponent<Button>();

            statusButton.onClick.Invoke();
            Assert.That(statusPanel.gameObject.activeSelf, Is.True);
            Assert.That(upgradePanel.gameObject.activeSelf, Is.False);

            upgradeButton.onClick.Invoke();
            Assert.That(statusPanel.gameObject.activeSelf, Is.False);
            Assert.That(upgradePanel.gameObject.activeSelf, Is.True);

            Transform batteryPreview = Array.Find(
                stationScreen.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name == "SM_Battery_Room");
            Assert.That(batteryPreview, Is.Not.Null);
            Assert.That(
                stationScreen.SelectPreviewObject(batteryPreview),
                Is.True);
            Assert.That(
                upgradePanel.Find("Slot_LVL_1").gameObject.activeSelf,
                Is.True);
            Assert.That(
                upgradePanel.Find("Slot_LVL_2").gameObject.activeSelf,
                Is.True);
            Assert.That(
                upgradePanel.Find("Slot_LVL_3").gameObject.activeSelf,
                Is.False,
                "Battery config contains only two upgrade levels.");

            Transform firstTurret = Array.Find(
                stationScreen.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name == "SM_Turret_0");
            Transform secondTurret = Array.Find(
                stationScreen.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name == "SM_Turret_1");
            Assert.That(firstTurret, Is.Not.Null);
            Assert.That(secondTurret, Is.Not.Null);

            Assert.That(stationScreen.SelectPreviewObject(firstTurret), Is.True);
            Assert.That(
                stationScreen.SelectedObjectId,
                Is.EqualTo("station_turret_01"));
            Assert.That(
                systems.GetUpgradeLevel(
                    StationSystemType.Turret,
                    stationScreen.SelectedObjectId,
                    1),
                Is.EqualTo(1));
            for (int level = 1; level <= 3; level++)
            {
                Transform slot = upgradePanel.Find($"Slot_LVL_{level}");
                Assert.That(slot.gameObject.activeSelf, Is.True);
                Component label = slot.Find("Text_info_LVL")
                    .GetComponent("TextMeshProUGUI");
                Assert.That(
                    label.GetType().GetProperty("text")?.GetValue(label),
                    Is.EqualTo($"TURRET {level}"));
            }

            Assert.That(stationScreen.SelectPreviewObject(secondTurret), Is.True);
            Assert.That(
                stationScreen.SelectedObjectId,
                Is.EqualTo("station_turret_02"));
            Assert.That(
                systems.GetUpgradeLevel(
                    StationSystemType.Turret,
                    stationScreen.SelectedObjectId,
                    0),
                Is.Zero,
                "The second turret must have independent starting progress.");

            terminal.ShowNextScreen();
            Assert.That(terminal.ActiveScreenIndex, Is.EqualTo(2));
            terminal.ShowPreviousScreen();
            Assert.That(terminal.ActiveScreenIndex, Is.EqualTo(1));
            Assert.That(statusPanel.gameObject.activeSelf, Is.True,
                "Returning to Station must restore the status tab.");
            Assert.That(upgradePanel.gameObject.activeSelf, Is.False);

            terminal.ShowMap();
            terminal.ShowPreviousScreen();
            Assert.That(terminal.ActiveScreenIndex, Is.EqualTo(3),
                "Previous-page navigation must wrap from Map to Storage.");
            terminal.ShowNextScreen();
            Assert.That(terminal.ActiveScreenIndex, Is.EqualTo(0),
                "Next-page navigation must wrap from Storage to Map.");
            terminal.ShowStation();

            Transform solar = Array.Find(
                stationScreen.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name == "SM_Solar_Panel");
            Assert.That(solar, Is.Not.Null);
            Assert.That(stationScreen.SelectPreviewObject(solar), Is.True);
            Assert.That(
                stationScreen.SelectedSystem,
                Is.EqualTo(StationSystemType.SolarPanel));
            Assert.That(
                stationStatusText.GetType().GetProperty("text")
                    ?.GetValue(stationStatusText)?.ToString(),
                Does.Not.Contain("Consumption -"),
                "A solar generator must not be presented as a consumer.");
            Assert.That(onButton.interactable, Is.False);
            Assert.That(offButton.interactable, Is.False,
                "Solar panel remains read-only from the computer.");
        }

        [UnityTest]
        public IEnumerator CriticalSystemTogglesCloseTerminalAndCutPower()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            Terminal.TerminalUIScreen terminal = Terminal.TerminalUIScreen.Instance;
            Terminal.TerminalStationScreenController stationScreen =
                Object.FindFirstObjectByType<
                    Terminal.TerminalStationScreenController>(
                    FindObjectsInactive.Include);
            Transform powerSwitch = Array.Find(
                stationScreen.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name == "Toggle");
            Button onButton = powerSwitch.Find("OnButton").GetComponent<Button>();

            energy.RestoreState(energy.TotalCapacity, true);
            systems.SetCriticalSystemActive(StationSystemType.Computer, true);
            terminal.Open();
            terminal.ShowStation();

            stationScreen.SelectSystem(StationSystemType.Computer);
            onButton.onClick.Invoke();
            Assert.That(terminal.IsOpen, Is.False);
            Assert.That(
                systems.IsRequestedActive(StationSystemType.Computer),
                Is.False);
            Assert.That(energy.GridEnabled, Is.True);

            systems.SetCriticalSystemActive(StationSystemType.Computer, true);
            terminal.Open();
            terminal.ShowStation();
            stationScreen.SelectSystem(StationSystemType.Battery);
            onButton.onClick.Invoke();

            Assert.That(terminal.IsOpen, Is.False);
            Assert.That(energy.GridEnabled, Is.False);
            Assert.That(
                systems.IsRequestedActive(StationSystemType.Battery),
                Is.False);
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
            Terminal.TerminalStationScreenController stationScreen =
                Object.FindFirstObjectByType<Terminal.TerminalStationScreenController>(
                    FindObjectsInactive.Include);

            Assert.That(energy, Is.Not.Null);
            Assert.That(discovery, Is.Not.Null);
            Assert.That(drone, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);
            Assert.That(stationScreen, Is.Not.Null);
            Assert.That(discovery.KnownLocations.Count, Is.GreaterThan(0));

            ExpeditionLocationData location = discovery.KnownLocations[0];
            discovery.RestoreDiscovered(Array.Empty<string>());
            energy.RestoreState(energy.TotalCapacity, true);
            Assert.That(
                systems.SetRequestedActive(StationSystemType.Drone, true),
                Is.True);
            drone.RefreshAvailability();
            Assert.That(drone.LaunchScan(location), Is.True);

            stationScreen.SelectSystem(StationSystemType.Drone);
            Transform powerSwitch = Array.Find(
                stationScreen.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name == "Toggle");
            Assert.That(powerSwitch, Is.Not.Null, "Drone power switch not found.");
            Button onButton = powerSwitch.Find("OnButton").GetComponent<Button>();
            Button offButton = powerSwitch.Find("OffButton").GetComponent<Button>();
            Assert.That(onButton.interactable, Is.False);
            Assert.That(offButton.interactable, Is.False);
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
                "InventoryScreen/ScanScreen/background_Screen_Storage_Slot_Invent"
            );
            Assert.That(content, Is.Not.Null);

            Assert.That(content.childCount, Is.EqualTo(inventory.BackpackCapacity));
            for (int i = 0; i < inventory.BackpackCapacity; i++)
            {
                Transform spawnPoint = content.Find($"Slot_{i + 1}");
                Assert.That(spawnPoint, Is.Not.Null);
                Assert.That(spawnPoint.gameObject.activeSelf, Is.True);
                Assert.That(
                    spawnPoint.GetComponent<InventorySlotView>(),
                    Is.Not.Null
                );
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
            Terminal.TerminalStorageScreenController storageScreen =
                Object.FindFirstObjectByType<Terminal.TerminalStorageScreenController>(
                    FindObjectsInactive.Include);

            Assert.That(storage, Is.Not.Null);
            Assert.That(storageScreen, Is.Not.Null);
            Assert.That(storage.BackpackSlots.Count, Is.EqualTo(16));
            Assert.That(storage.QuickAccessSlots.Count, Is.EqualTo(16));
            Assert.That(storage.AnomalySlots.Count, Is.EqualTo(16));

            Assert.That(
                CountDirectSlotButtons(storageScreen.transform.Find(
                    "background_Screen_Storage_Slot")),
                Is.EqualTo(storage.BackpackSlots.Count));
            Assert.That(
                CountDirectSlotButtons(storageScreen.transform.Find(
                    "background_Screen_Storage_Slot_Equipment")),
                Is.EqualTo(storage.QuickAccessSlots.Count));
            Assert.That(
                CountDirectSlotButtons(storageScreen.transform.Find(
                    "background_Screen_Storage_Slot_Anomaly")),
                Is.EqualTo(storage.AnomalySlots.Count));
        }

        [UnityTest]
        public IEnumerator DevicesTabShowsInventoryAndOtherTabsHideIt()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            Terminal.TerminalUIScreen terminal =
                Object.FindFirstObjectByType<Terminal.TerminalUIScreen>(
                    FindObjectsInactive.Include);
            InventoryLabHUDController inventoryHud = InventoryLabHUDController.Instance;
            Assert.That(terminal, Is.Not.Null);
            Assert.That(inventoryHud, Is.Not.Null);

            EnergySystemController.Instance.RestoreState(
                EnergySystemController.Instance.TotalCapacity,
                true);
            StationSystemsController.Instance.SetCriticalSystemActive(
                StationSystemType.Computer,
                true);
            terminal.Open();

            Button devicesButton = terminal.transform.Find("StorageButton")
                .GetComponent<Button>();
            Button statusButton = terminal.transform.Find("StationButton")
                .GetComponent<Button>();
            Transform storageScreen = terminal.transform.Find("StorageScreen");

            devicesButton.onClick.Invoke();
            Assert.That(storageScreen.gameObject.activeSelf, Is.True);
            Assert.That(
                storageScreen.Find("background_Screen_Storage_Slot_Invent")
                    .gameObject.activeSelf,
                Is.True);
            Assert.That(
                storageScreen.Find("background_Screen_Storage_Slot_Invent_Anomaly")
                    .gameObject.activeSelf,
                Is.True);
            Assert.That(
                storageScreen.Find("background_Screen_Storage_Slot_Invent_Equipment")
                    .gameObject.activeSelf,
                Is.True);

            statusButton.onClick.Invoke();
            Assert.That(storageScreen.gameObject.activeSelf, Is.False);
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
            Terminal.TerminalUIScreen terminal =
                Object.FindFirstObjectByType<Terminal.TerminalUIScreen>(
                    FindObjectsInactive.Include);
            ItemCatalogData catalog = Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData item = catalog != null ? catalog.Find("servo_drive_01") : null;

            Assert.That(inventory, Is.Not.Null);
            Assert.That(storage, Is.Not.Null);
            Assert.That(terminal, Is.Not.Null);
            Assert.That(item, Is.Not.Null);
            inventory.RestoreItemInstances(Array.Empty<ItemInstance>());
            storage.ResetStorage();
            Assert.That(inventory.AddItem(item), Is.True);

            EnergySystemController.Instance.RestoreState(
                EnergySystemController.Instance.TotalCapacity,
                true);
            StationSystemsController.Instance.SetCriticalSystemActive(
                StationSystemType.Computer,
                true);
            terminal.Open();
            terminal.transform.Find("StorageButton").GetComponent<Button>()
                .onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();

            Transform storageScreenRoot = terminal.transform.Find("StorageScreen");
            Assert.That(storageScreenRoot, Is.Not.Null, "StorageScreen not found.");
            LaboratoryInventoryItemDrag source = null;
            foreach (LaboratoryInventoryItemDrag drag in
                     storageScreenRoot.GetComponentsInChildren<
                         LaboratoryInventoryItemDrag>(true))
            {
                if (drag.Item == item && !drag.IsStationStorageSource)
                {
                    source = drag;
                    break;
                }
            }

            Transform destinationRoot = terminal.transform.Find(
                "StorageScreen/background_Screen_Storage_Slot/Slot_1");
            Assert.That(destinationRoot, Is.Not.Null, "Storage Slot_1 not found.");
            LaboratoryItemDropSlot destination =
                destinationRoot.GetComponent<LaboratoryItemDropSlot>();
            Assert.That(source, Is.Not.Null, "Occupied inventory slot has no drag source.");
            Assert.That(destination, Is.Not.Null, "Storage slot has no drop target.");
            Transform destinationIconRoot = destination.transform.Find("RuntimeIcon");
            Assert.That(destinationIconRoot, Is.Not.Null, "Storage icon not created.");
            Image destinationIcon = destinationIconRoot.GetComponent<Image>();
            Assert.That(destinationIcon, Is.Not.Null);
            Assert.That(source.SourceGroup, Is.EqualTo(InventorySlotGroup.Backpack));
            Assert.That(source.SourceIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(source.IsStationStorageSource, Is.False);
            Assert.That(source.IsLaboratorySource, Is.False);
            Assert.That(source.IsChargingSource, Is.False);
            Assert.That(storage.BackpackSlots.Count, Is.EqualTo(16));
            Assert.That(
                PlayerInventory.GetSlotGroup(item.ItemType),
                Is.EqualTo(InventorySlotGroup.Backpack));
            Assert.That(
                inventory.GetItemInstance(source.SourceGroup, source.SourceIndex)?.ItemData,
                Is.SameAs(item));
            Assert.That(
                destination.ItemDropped,
                Is.Not.Null,
                "Storage slot was created without terminal storage callback.");

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
