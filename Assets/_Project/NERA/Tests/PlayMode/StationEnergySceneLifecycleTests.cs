using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NERA.Core;
using NERA.Drone;
using NERA.Energy;
using NERA.Expeditions;
using NERA.Interaction;
using NERA.Inventory;
using NERA.Items;
using NERA.Research;
using NERA.Quests;
using NERA.Save;
using NERA.Station;
using NERA.UI;
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
        public IEnumerator BootRemainsInMenuUntilAStartActionIsRequested()
        {
            SceneManager.LoadScene("Boot");
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Boot"));
            Assert.That(
                Object.FindFirstObjectByType<MainMenuController>(),
                Is.Not.Null);
            Assert.That(
                Object.FindFirstObjectByType<BootInitializer>(),
                Is.Null);
        }

        [UnityTest]
        public IEnumerator LaboratoryIsUnavailableUntilGridStarts()
        {
            SceneManager.LoadScene("MainScene");
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
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            StationEnvironmentController environment =
                StationEnvironmentController.Instance;
            BootInitializer runtime = BootInitializer.Instance;

            Assert.That(energy, Is.Not.Null);
            Assert.That(environment, Is.Not.Null);
            Assert.That(runtime, Is.Not.Null);
            Assert.That(
                SceneManager.GetSceneByName("MainScene").isLoaded,
                Is.True,
                "MainScene must remain loaded while gameplay content is active.");
            Assert.That(
                runtime.gameObject.scene.name,
                Is.EqualTo("MainScene"),
                "RuntimeRoot must stay in MainScene, not DontDestroyOnLoad.");

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
            int initialBatteryCount = batteries.Length;
            int initialSolarPanelCount = solarPanels.Length;
            float initialCapacity = energy.TotalCapacity;
            Assert.That(initialBatteryCount, Is.GreaterThan(0));
            Assert.That(initialCapacity, Is.GreaterThan(0f));
            Assert.That(
                energy.CurrentGeneration,
                Is.EqualTo(
                    energy.Config.ClearDayGeneration * initialSolarPanelCount)
                    .Within(0.01f)
            );

            Assert.That(
                runtime.LoadGameplayScene("Expedition_01", string.Empty),
                Is.True);
            yield return WaitForScene("Expedition_01");

            Assert.That(
                runtime.LoadGameplayScene("Player_Station", "Station_Start"),
                Is.True);
            yield return WaitForScene("Player_Station");
            yield return null;

            Assert.That(SceneManager.GetSceneByName("MainScene").isLoaded, Is.True);
            Assert.That(
                SceneManager.GetSceneByName("Expedition_01").isLoaded,
                Is.False);

            environment.SetTime(12f);
            environment.SetWeather(StationWeather.Clear);
            energy.AdvanceSimulation(0.1f);

            solarPanels = Object.FindObjectsByType<SolarPanelInteractable>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            batteries = Object.FindObjectsByType<StationBattery>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            Assert.That(
                batteries.Length,
                Is.EqualTo(initialBatteryCount),
                "Returning to the station duplicated or lost battery sources.");
            Assert.That(
                solarPanels.Length,
                Is.EqualTo(initialSolarPanelCount),
                "Returning to the station duplicated or lost solar sources.");
            Assert.That(energy.TotalCapacity, Is.EqualTo(initialCapacity));
            Assert.That(
                energy.CurrentGeneration,
                Is.EqualTo(
                    energy.Config.ClearDayGeneration * initialSolarPanelCount)
                    .Within(0.01f)
            );
        }

        [UnityTest]
        public IEnumerator StationTabsAndSystemTogglesAreIndependent()
        {
            SceneManager.LoadScene("MainScene");
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
            systems.ResetSystems();

            Camera[] previewCameras =
                terminal.GetComponentsInChildren<Camera>(true);
            Camera mapPreviewCamera = Array.Find(
                previewCameras,
                camera => camera.name == "MapUICamera");
            Camera stationPreviewCamera = Array.Find(
                previewCameras,
                camera => camera.name == "StationUICamera");
            Assert.That(mapPreviewCamera, Is.Not.Null);
            Assert.That(stationPreviewCamera, Is.Not.Null);
            Assert.That(mapPreviewCamera.enabled, Is.False);
            Assert.That(stationPreviewCamera.enabled, Is.False);

            energy.RestoreState(energy.TotalCapacity, true);
            systems.SetCriticalSystemActive(StationSystemType.Computer, true);
            terminal.Open();
            terminal.ShowStation();
            Assert.That(stationPreviewCamera.enabled, Is.True);
            Assert.That(mapPreviewCamera.enabled, Is.False);

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

            energy.RestoreState(
                energy.TotalCapacity *
                energy.Config.GetMinimumCharge01(
                    StationSystemType.Laboratory) *
                0.5f,
                true);
            yield return null;
            Assert.That(
                systems.IsRequestedActive(StationSystemType.Laboratory),
                Is.True,
                "Automatic load shedding must preserve the user's preference.");
            Assert.That(offButton.gameObject.activeSelf, Is.True);
            Assert.That(offButton.interactable, Is.False,
                "The system cannot be restarted below its configured threshold.");
            Assert.That(powerHandle.anchoredPosition.x, Is.EqualTo(-25f).Within(0.1f));
            Assert.That(
                powerStatus.GetType().GetProperty("text")?.GetValue(powerStatus),
                Is.EqualTo("Low Power"));
            Assert.That(
                stationStatusText.GetType().GetProperty("text")
                    ?.GetValue(stationStatusText)?.ToString(),
                Does.Contain("Status - LOW POWER"));

            energy.RestoreState(energy.TotalCapacity, true);
            yield return null;
            Assert.That(onButton.gameObject.activeSelf, Is.True);
            Assert.That(onButton.interactable, Is.True);
            Assert.That(
                powerStatus.GetType().GetProperty("text")?.GetValue(powerStatus),
                Is.EqualTo("Active"));

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
                candidate => candidate.name == "SM_Turret_1");
            Transform secondTurret = Array.Find(
                stationScreen.GetComponentsInChildren<Transform>(true),
                candidate => candidate.name == "SM_Turret_2");
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
            Assert.That(
                firstTurret.Find("Stage_1").gameObject.activeSelf,
                Is.True,
                "The first preview turret must show its starting level.");
            Assert.That(
                secondTurret.Find("Stage_0").gameObject.activeSelf,
                Is.True,
                "A locked preview turret must show level zero.");

            systems.Restore(
                null,
                null,
                new[]
                {
                    new StationObjectSystemState(
                        StationSystemType.Turret,
                        "station_turret_02",
                        2,
                        true)
                });
            yield return null;
            Assert.That(
                secondTurret.Find("Stage_0").gameObject.activeSelf,
                Is.False);
            Assert.That(
                secondTurret.Find("Stage_2").gameObject.activeSelf,
                Is.True,
                "The station preview must switch immediately when the " +
                "selected turret upgrade level changes.");

            terminal.ShowNextScreen();
            Assert.That(terminal.ActiveScreenIndex, Is.EqualTo(2));
            terminal.ShowPreviousScreen();
            Assert.That(terminal.ActiveScreenIndex, Is.EqualTo(1));
            Assert.That(statusPanel.gameObject.activeSelf, Is.True,
                "Returning to Station must restore the status tab.");
            Assert.That(upgradePanel.gameObject.activeSelf, Is.False);

            terminal.ShowMap();
            Assert.That(mapPreviewCamera.enabled, Is.True);
            Assert.That(stationPreviewCamera.enabled, Is.False);
            terminal.ShowPreviousScreen();
            Assert.That(terminal.ActiveScreenIndex, Is.EqualTo(3),
                "Previous-page navigation must wrap from Map to Storage.");
            Assert.That(mapPreviewCamera.enabled, Is.False);
            Assert.That(stationPreviewCamera.enabled, Is.False);
            terminal.ShowNextScreen();
            Assert.That(terminal.ActiveScreenIndex, Is.EqualTo(0),
                "Next-page navigation must wrap from Storage to Map.");
            Assert.That(mapPreviewCamera.enabled, Is.True);
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

            ItemCatalogData catalog =
                Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData equipment = catalog != null
                ? catalog.Find("energy_pistol_01")
                : null;
            Library.LibraryController library =
                Library.LibraryController.Instance;
            Assert.That(equipment, Is.Not.Null);
            Assert.That(library, Is.Not.Null);
            library.RegisterKnownItem(equipment);

            terminal.ShowLibrary();
            Transform libraryScreen = terminal.transform.Find("LibraryScreen");
            libraryScreen.Find("EquipmentButton").GetComponent<Button>()
                .onClick.Invoke();
            Transform equipmentSlot = libraryScreen.Find(
                "EquipmentSlot/background_Slot_01");
            Assert.That(equipmentSlot, Is.Not.Null);
            Assert.That(
                equipmentSlot.Find("RuntimeIcon"),
                Is.Null,
                "Library list slots must remain text-only.");
            equipmentSlot.GetComponent<Button>().onClick.Invoke();
            Component libraryInfoName = libraryScreen.Find(
                    "background_Screen_Lybrary_Info/Text_Name")
                .GetComponent("TextMeshProUGUI");
            Assert.That(
                libraryInfoName.GetType().GetProperty("text")
                    ?.GetValue(libraryInfoName),
                Is.EqualTo(equipment.DisplayName),
                "Clicking a text-only Library slot must show item details.");

            terminal.Close();
            Assert.That(mapPreviewCamera.enabled, Is.False);
            Assert.That(stationPreviewCamera.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator CriticalSystemTogglesCloseTerminalAndCutPower()
        {
            SceneManager.LoadScene("MainScene");
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
            Assert.That(
                terminal.IsOpen,
                Is.False,
                "Computer shutdown must close the terminal.");
            Assert.That(
                systems.IsRequestedActive(StationSystemType.Computer),
                Is.False,
                "Computer must be inactive after pressing its active toggle.");
            Assert.That(energy.GridEnabled, Is.True);

            systems.SetCriticalSystemActive(StationSystemType.Computer, true);
            terminal.Open();
            terminal.ShowStation();
            stationScreen.SelectSystem(StationSystemType.Battery);
            onButton.onClick.Invoke();

            Assert.That(
                terminal.IsOpen,
                Is.False,
                "Battery shutdown must close the terminal.");
            Assert.That(
                energy.GridEnabled,
                Is.False,
                "Battery shutdown must disable the energy grid.");
            Assert.That(
                systems.IsRequestedActive(StationSystemType.Battery),
                Is.False,
                "Battery must be inactive after pressing its active toggle.");
        }

        [UnityTest]
        public IEnumerator BatteryUpgradeAppliesStageCapacityImmediately()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            StationSystemsController systems = StationSystemsController.Instance;
            StationStorageController storage = StationStorageController.Instance;
            PlayerInventory inventory =
                Object.FindFirstObjectByType<PlayerInventory>();
            ItemCatalogData catalog =
                Resources.Load<ItemCatalogData>("ItemCatalog_Default");

            Assert.That(energy, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);
            Assert.That(inventory, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);

            systems.ResetSystems();
            yield return null;
            Assert.That(energy.TotalCapacity, Is.EqualTo(1000f));

            StationUpgradeLevelDefinition upgrade =
                systems.Config.GetUpgradeDefinition(
                    StationSystemType.Battery,
                    "station_battery",
                    2);
            Assert.That(upgrade, Is.Not.Null);
            foreach (StationUpgradeItemRequirement requirement in
                     upgrade.RequiredItems)
            {
                for (int count = 0; count < requirement.Count; count++)
                {
                    Assert.That(
                        inventory.AddItem(requirement.Item),
                        Is.True,
                        $"Could not add '{requirement.ItemId}' for the test upgrade.");
                }
            }

            energy.RestoreState(750f, true);
            float expectedEnergy = 750f - upgrade.EnergyCost;

            Assert.That(
                systems.TryUpgrade(
                    StationSystemType.Battery,
                    inventory,
                    storage),
                Is.True);
            Assert.That(
                energy.TotalCapacity,
                Is.EqualTo(2000f),
                "Stage_2 capacity must be active in the same upgrade call.");
            Assert.That(
                energy.CurrentEnergy,
                Is.EqualTo(expectedEnergy),
                "Increasing capacity must not add free stored energy.");

            StationUpgradeStageController[] stages =
                Object.FindObjectsByType<StationUpgradeStageController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            StationUpgradeStageController batteryStages = Array.Find(
                stages,
                stage =>
                    stage.SystemType == StationSystemType.Battery &&
                    stage.ObjectId == "station_battery");
            Assert.That(batteryStages, Is.Not.Null);
            Assert.That(batteryStages.CurrentStage, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator MainExpeditionQuestRunsFromRuntimeSignals()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            QuestController quests = QuestController.Instance;
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            QuestHUDController questHud =
                Object.FindFirstObjectByType<QuestHUDController>(
                    FindObjectsInactive.Include);
            Assert.That(quests, Is.Not.Null);
            Assert.That(discovery, Is.Not.Null);
            Assert.That(questHud, Is.Not.Null);

            quests.ResetProgress();
            discovery.RestoreDiscovered(Array.Empty<string>());
            Assert.That(questHud.IsVisible, Is.False);

            Assert.That(
                quests.Report(
                    QuestSignalType.LocationEntered,
                    "Player_Station"),
                Is.True);
            Assert.That(
                quests.FindActive("main.restore_battery"),
                Is.Not.Null,
                "Restoring station power is the first one-time main quest.");
            Assert.That(
                quests.Report(
                    QuestSignalType.StationSystemActivated,
                    "station_battery",
                    "BATTERY"),
                Is.True);
            Assert.That(
                quests.IsCompleted("main.restore_battery"),
                Is.True);
            Assert.That(questHud.IsVisible, Is.False);

            Assert.That(discovery.Discover("Expedition_01"), Is.True);
            Assert.That(
                quests.FindActive("main.expedition_01")?.CurrentStageIndex,
                Is.Zero);
            Assert.That(
                questHud.DisplayedMainText,
                Does.Contain("ОСНОВНОЕ ЗАДАНИЕ"));
            Assert.That(
                questHud.DisplayedMainText,
                Does.Contain("Отправляйтесь в Ancient Outpost"));
            Assert.That(questHud.DisplayedSideText, Is.Empty);

            quests.ReportDeviceCondition(
                "test_solar_panel",
                "Test Solar Panel",
                0.3f);
            Assert.That(
                questHud.DisplayedSideText,
                Does.Contain("Очистите Test Solar Panel"));

            quests.ReportStationFault(
                "test_turret",
                "Test Turret",
                "EnemySabotage");
            Assert.That(
                questHud.DisplayedSideText,
                Does.Contain("Перезапустите Test Turret"),
                "The higher-priority side quest must be displayed.");

            quests.Report(
                QuestSignalType.StationSystemActivated,
                "test_turret",
                "Test Turret");
            Assert.That(
                questHud.DisplayedSideText,
                Does.Contain("Очистите Test Solar Panel"),
                "HUD must fall back to the next active side quest.");

            quests.Report(QuestSignalType.LocationEntered, "Expedition_01");
            quests.Report(QuestSignalType.EnemyEncountered, "io_blue_weak");
            quests.Report(QuestSignalType.ItemCollected, "io_blue_shard_01");
            quests.Report(QuestSignalType.LocationEntered, "Player_Station");
            quests.Report(
                QuestSignalType.ResearchAnalyzed,
                "research_io_blue_shard_01");

            Assert.That(quests.IsCompleted("main.expedition_01"), Is.True);
            Assert.That(questHud.DisplayedMainText, Is.Empty);
            Assert.That(questHud.IsVisible, Is.True);

            quests.ReportDeviceCondition(
                "test_solar_panel",
                "Test Solar Panel",
                1f);
            Assert.That(quests.ActiveQuests.Count, Is.Zero);
            Assert.That(questHud.DisplayedSideText, Is.Empty);
            Assert.That(questHud.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator FirstPhysicalPowerRestoreCompletesBatteryQuest()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            QuestController quests = QuestController.Instance;
            StationSystemsController systems =
                StationSystemsController.Instance;
            StationPowerController power = StationPowerController.Instance;
            PowerRestoreInteractable restore =
                Object.FindFirstObjectByType<PowerRestoreInteractable>();

            Assert.That(quests, Is.Not.Null);
            Assert.That(systems, Is.Not.Null);
            Assert.That(power, Is.Not.Null);
            Assert.That(restore, Is.Not.Null);

            quests.ResetProgress();
            systems.ResetSystems();
            power.SetState(StationPowerState.Offline);

            Assert.That(
                systems.IsRequestedActive(StationSystemType.Battery),
                Is.False,
                "A new game must start with the station battery disabled.");

            Assert.That(
                systems.SetCriticalSystemActive(
                    StationSystemType.Battery,
                    true),
                Is.True);
            quests.ResetProgress();
            Assert.That(
                quests.Report(
                    QuestSignalType.LocationEntered,
                    "Player_Station"),
                Is.True);
            Assert.That(
                quests.FindActive("main.restore_battery"),
                Is.Not.Null);
            Assert.That(
                systems.IsRequestedActive(StationSystemType.Battery),
                Is.True,
                "The test reproduces an old save with RequestedActive=true.");
            Assert.That(power.IsPowered, Is.False);

            restore.CompleteInteraction(null);

            Assert.That(power.IsPowered, Is.True);
            Assert.That(
                quests.IsCompleted("main.restore_battery"),
                Is.True,
                "The first physical power restore must complete the quest " +
                "even when RequestedActive was already true.");
        }

        [UnityTest]
        public IEnumerator DroneCanSurveySecondLocationAfterRecharge()
        {
            SceneManager.LoadScene("MainScene");
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
            systems.ResetSystems();
            int droneLevel =
                systems.GetUpgradeLevel(StationSystemType.Drone);
            ExpeditionLocationData first = discovery.KnownLocations
                .FirstOrDefault(
                    location =>
                        location != null &&
                        location.DiscoverySource ==
                            NERA.Locations.DiscoverySource.Drone &&
                        location.RequiredDroneUpgradeLevel <= droneLevel);
            ExpeditionLocationData second = discovery.KnownLocations
                .FirstOrDefault(
                    location =>
                        location != null &&
                        location != first &&
                        location.DiscoverySource ==
                            NERA.Locations.DiscoverySource.Drone &&
                        location.RequiredDroneUpgradeLevel == droneLevel + 1);
            if (first == null || second == null)
            {
                Assert.Ignore(
                    "This upgrade scenario needs one currently reachable " +
                    "location and one location unlocked by the next Drone level.");
            }

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
            SceneManager.LoadScene("MainScene");
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
            for (int level = 1; level <= 3; level++)
            {
                Transform iconRoot = stationScreen.transform.Find(
                    $"background_Upgrade/Slot_LVL_{level}/Image_Icon");
                Assert.That(
                    iconRoot,
                    Is.Not.Null,
                    $"Upgrade level {level} icon layer was not created.");
                Image icon = iconRoot.GetComponent<Image>();
                Assert.That(icon, Is.Not.Null);
                Assert.That(icon.raycastTarget, Is.False);
                Assert.That(icon.preserveAspect, Is.True);
            }
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
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            InventoryLabHUDController hud = InventoryLabHUDController.Instance;
            PlayerInventory inventory =
                Object.FindFirstObjectByType<PlayerInventory>();

            Assert.That(hud, Is.Not.Null);
            Assert.That(inventory, Is.Not.Null);
            Assert.That(inventory.Config, Is.Not.Null);

            Transform content = FindDescendant(
                hud.transform.Find("InventoryScreen"),
                "background_Screen_Storage_Slot_Invent");
            Assert.That(content, Is.Not.Null);

            Assert.That(content.childCount, Is.EqualTo(inventory.BackpackCapacity));
            for (int i = 0; i < inventory.BackpackCapacity; i++)
            {
                Transform spawnPoint = content.Find($"Slot_{i + 1}");
                Assert.That(spawnPoint, Is.Not.Null);
                Assert.That(spawnPoint.gameObject.activeSelf, Is.True);
                Assert.That(
                    spawnPoint.GetComponent<InventorySlotView>(),
                    Is.Null,
                    "Slot_N must remain a spawn point, not an inventory slot.");
                Assert.That(
                    GetSpawnedInventorySlot(spawnPoint),
                    Is.Not.Null,
                    "P_InventorySlot was not spawned inside Slot_N.");
            }
        }

        [UnityTest]
        public IEnumerator InventoryScreenSupportsAllSlotsAndDropsSelectedItem()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            InventoryLabHUDController hud = InventoryLabHUDController.Instance;
            PlayerInventory inventory =
                Object.FindFirstObjectByType<PlayerInventory>();
            ItemCatalogData catalog =
                Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData item = catalog != null
                ? catalog.Find("servo_drive_01")
                : null;

            Assert.That(hud, Is.Not.Null);
            Assert.That(inventory, Is.Not.Null);
            Assert.That(item, Is.Not.Null);
            Assert.That(item.WorldPrefab, Is.Not.Null);
            Assert.That(inventory.BackpackCapacity, Is.EqualTo(8));
            Assert.That(PlayerInventory.AnomalyCapacity, Is.EqualTo(4));
            Assert.That(PlayerInventory.QuickAccessCapacity, Is.EqualTo(4));
            Assert.That(PlayerInventory.ActiveQuickAccessCapacity, Is.EqualTo(4));

            Transform inventoryScreen = hud.transform.Find("InventoryScreen");
            Transform backpackRoot = FindDescendant(
                inventoryScreen,
                "background_Screen_Storage_Slot_Invent");
            Transform anomalyRoot = FindDescendant(
                inventoryScreen,
                "background_Screen_Storage_Slot_Invent_Anomaly");
            Transform quickRoot = hud.transform.Find("Slot_Invent_Equipment");
            Button dropButton = FindDescendant(
                inventoryScreen,
                "DropButton").GetComponent<Button>();

            Assert.That(backpackRoot.childCount, Is.EqualTo(8));
            Assert.That(anomalyRoot.childCount, Is.EqualTo(4));
            Assert.That(quickRoot.childCount, Is.EqualTo(4));

            foreach (Transform root in new[]
                     {
                         backpackRoot,
                         anomalyRoot,
                         quickRoot
                     })
            {
                for (int index = 0; index < root.childCount; index++)
                {
                    InventorySlotView view =
                        GetSpawnedInventorySlot(root.GetChild(index));
                    Assert.That(
                        view,
                        Is.Not.Null,
                        $"{root.name} Slot_{index + 1} has no P_InventorySlot.");
                    Assert.That(
                        view.GetComponent<InventorySlotDropTarget>(),
                        Is.Not.Null,
                        $"{root.name} slot {index + 1} has no drop target.");
                }
            }

            inventory.RestoreItemInstances(Array.Empty<ItemInstance>());
            Assert.That(inventory.AddItem(item), Is.True);
            hud.OpenInventory();
            Assert.That(inventoryScreen.gameObject.activeSelf, Is.True);

            Transform sourceSlot =
                GetSpawnedInventorySlot(backpackRoot.GetChild(0)).transform;
            Transform destinationSlot =
                GetSpawnedInventorySlot(backpackRoot.GetChild(7)).transform;
            LaboratoryInventoryItemDrag sourceDrag =
                sourceSlot.GetComponent<LaboratoryInventoryItemDrag>();
            InventorySlotDropTarget destination =
                destinationSlot.GetComponent<InventorySlotDropTarget>();
            Assert.That(sourceDrag, Is.Not.Null);
            Assert.That(sourceDrag.Item, Is.EqualTo(item));
            Assert.That(EventSystem.current, Is.Not.Null);

            PointerEventData dropEvent =
                new PointerEventData(EventSystem.current)
                {
                    pointerDrag = sourceSlot.gameObject
                };
            destination.OnDrop(dropEvent);
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 0),
                Is.Null);
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 7),
                Is.EqualTo(item));

            destinationSlot.GetComponent<Button>().onClick.Invoke();
            Assert.That(dropButton.interactable, Is.True);
            dropButton.onClick.Invoke();
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 7),
                Is.Null);
            Assert.That(inventory.Count, Is.Zero);

            WorldItem[] worldItems = Object.FindObjectsByType<WorldItem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            WorldItem droppedItem = Array.Find(
                worldItems,
                worldItem => worldItem != null &&
                             worldItem.ItemData == item &&
                             worldItem.name.StartsWith("Dropped_"));
            Assert.That(
                droppedItem,
                Is.Not.Null,
                "DropButton did not create the selected world item.");

            Object.Destroy(droppedItem.gameObject);
            hud.CloseAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LaboratoryScreenUsesUnifiedInventoryAndWorkflows()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            InventoryLabHUDController hud = InventoryLabHUDController.Instance;
            PlayerInventory inventory =
                Object.FindFirstObjectByType<PlayerInventory>();
            ResearchController research = ResearchController.Instance;
            LaboratoryWorkstationController workstation =
                LaboratoryWorkstationController.Instance;
            ItemCatalogData catalog =
                Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData pistol = catalog != null
                ? catalog.Find("energy_pistol_01")
                : null;
            ItemData integrator = catalog != null
                ? catalog.Find("io_integrator_01")
                : null;
            ItemData anomaly = catalog != null
                ? catalog.Find("io_blue_shard_01")
                : null;
            ItemData record = catalog != null
                ? catalog.Find("ancient_record_02")
                : null;

            Assert.That(hud, Is.Not.Null);
            Assert.That(inventory, Is.Not.Null);
            Assert.That(research, Is.Not.Null);
            Assert.That(workstation, Is.Not.Null);
            Assert.That(pistol, Is.Not.Null);
            Assert.That(integrator, Is.Not.Null);
            Assert.That(anomaly, Is.Not.Null);
            Assert.That(record, Is.Not.Null);

            inventory.RestoreItemInstances(Array.Empty<ItemInstance>());
            research.RestoreAnalyzed(Array.Empty<string>());
            research.RestoreLoadedItem(null, null);
            workstation.RestoreItems(
                Array.Empty<ItemInstance>(),
                Array.Empty<ItemInstance>());
            Assert.That(inventory.AddItem(pistol), Is.True, "Pistol was not added.");
            Assert.That(
                inventory.AddItem(integrator),
                Is.True,
                "IO Integrator was not added.");
            Assert.That(inventory.AddItem(anomaly), Is.True, "Anomaly was not added.");
            Assert.That(inventory.AddItem(record), Is.True, "Record was not added.");

            EnergySystemController energy = EnergySystemController.Instance;
            Assert.That(energy, Is.Not.Null);
            energy.RestoreState(energy.TotalCapacity, true);
            StationSystemsController.Instance.SetRequestedActive(
                StationSystemType.Laboratory,
                true);

            hud.OpenLaboratory(inventory.gameObject);
            yield return null;

            Transform laboratory = hud.transform.Find("LaboratoryScreen");
            LaboratoryScreenController screen =
                laboratory.GetComponent<LaboratoryScreenController>();
            Transform sharedInventory =
                laboratory.Find("Inventory_and_info_Screen");
            Transform powerScreen = laboratory.Find("PowerScreen");
            Transform scanScreen = laboratory.Find("ScanScreen");
            Transform upgradeScreen = laboratory.Find("UpgradeScreen");

            Assert.That(laboratory.gameObject.activeSelf, Is.True, "LaboratoryScreen is closed.");
            Assert.That(screen, Is.Not.Null);
            Assert.That(sharedInventory.gameObject.activeSelf, Is.True, "Shared inventory is hidden.");
            Assert.That(scanScreen.gameObject.activeSelf, Is.True, "Scan screen is not the default.");
            Assert.That(powerScreen.gameObject.activeSelf, Is.False);
            Assert.That(upgradeScreen.gameObject.activeSelf, Is.False);

            AssertLaboratoryInventoryGroup(
                sharedInventory,
                "background_Screen_Storage_Slot_Invent",
                8);
            AssertLaboratoryInventoryGroup(
                sharedInventory,
                "background_Screen_Storage_Slot_Invent_Anomaly",
                4);
            AssertLaboratoryInventoryGroup(
                sharedInventory,
                "background_Screen_Storage_Slot_Invent_Equipment",
                4);

            laboratory.Find("PowerMapButton").GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(powerScreen.gameObject.activeSelf, Is.True, "Power tab did not open.");
            Assert.That(scanScreen.gameObject.activeSelf, Is.False);
            Assert.That(upgradeScreen.gameObject.activeSelf, Is.False);
            Assert.That(sharedInventory.gameObject.activeSelf, Is.True, "Shared inventory hid after tab switch.");

            laboratory.Find("NextButton").GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(screen.ActiveModeIndex, Is.EqualTo(1));
            Assert.That(scanScreen.gameObject.activeSelf, Is.True);
            laboratory.Find("BackButton").GetComponent<Button>()
                .onClick.Invoke();
            Assert.That(screen.ActiveModeIndex, Is.EqualTo(0));
            yield return null;
            Canvas.ForceUpdateCanvases();

            LaboratoryInventoryItemDrag pistolDrag =
                FindPlayerInventoryDrag(laboratory, pistol);
            Transform powerSlotRoot = FindDescendant(
                powerScreen,
                "Slot_01");
            InventorySlotView powerSlot =
                GetSpawnedInventorySlot(powerSlotRoot);
            Assert.That(pistolDrag, Is.Not.Null);
            Assert.That(powerSlot, Is.Not.Null);
            Button inventorySlotButton =
                pistolDrag.GetComponent<Button>();
            Assert.That(inventorySlotButton, Is.Not.Null);
            ClickThroughUi(inventorySlotButton);
            Component laboratoryInfoName = FindDescendant(
                    sharedInventory,
                    "Text_Name")
                .GetComponent("TextMeshProUGUI");
            Assert.That(
                laboratoryInfoName.GetType().GetProperty("text")
                    ?.GetValue(laboratoryInfoName)?.ToString(),
                Is.EqualTo(pistol.DisplayName),
                "Laboratory inventory slot did not select its item.");

            Canvas.ForceUpdateCanvases();
            DropThroughUi(
                pistolDrag,
                powerSlot.GetComponent<LaboratoryItemDropSlot>());

            Assert.That(
                workstation.GetChargingItem(0)?.ItemData,
                Is.SameAs(pistol));
            Transform progressTransform = FindDescendant(
                powerScreen,
                "Text_progress_01");
            Component progress = progressTransform.GetComponent(
                "TextMeshProUGUI");
            Assert.That(progressTransform.gameObject.activeSelf, Is.True, "Charge progress is hidden.");
            Assert.That(
                progress.GetType().GetProperty("text")?.GetValue(progress)
                    ?.ToString(),
                Does.EndWith("%"));

            FindDescendant(powerScreen, "DropButton")
                .GetComponent<Button>().onClick.Invoke();
            Assert.That(workstation.GetChargingItem(0), Is.Null);
            Assert.That(inventory.Contains(pistol.ItemId), Is.True, "Power Drop did not return pistol.");

            laboratory.Find("UpgradeMapButton").GetComponent<Button>()
                .onClick.Invoke();
            LaboratoryInventoryItemDrag rejectedPistol =
                FindPlayerInventoryDrag(laboratory, pistol);
            LaboratoryInventoryItemDrag upgradeIntegrator =
                FindPlayerInventoryDrag(laboratory, integrator);
            LaboratoryInventoryItemDrag upgradeAnomaly =
                FindPlayerInventoryDrag(laboratory, anomaly);
            Transform upgradeSlot01 = upgradeScreen.transform.Find(
                "background_Screen_Storage_Slot/Slot_01");
            Transform upgradeSlot02 = upgradeScreen.transform.Find(
                "background_Screen_Storage_Slot/Slot_02");
            GetSpawnedInventorySlot(upgradeSlot01)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(rejectedPistol);
            Assert.That(
                workstation.GetUpgradeItem(0),
                Is.Null,
                "Ordinary weapons must not enter the integration slot.");
            GetSpawnedInventorySlot(upgradeSlot01)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(upgradeIntegrator);
            GetSpawnedInventorySlot(upgradeSlot02)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(upgradeAnomaly);

            Assert.That(
                workstation.GetUpgradeItem(0)?.ItemData,
                Is.SameAs(integrator));
            Assert.That(
                workstation.GetUpgradeItem(1)?.ItemData,
                Is.SameAs(anomaly));
            Assert.That(
                FindDescendant(upgradeScreen, "UpgradeButton")
                    .GetComponent<Button>().interactable,
                Is.False,
                "Synthesis is intentionally reserved for the next mechanic.");

            FindDescendant(upgradeScreen, "DropButton")
                .GetComponent<Button>().onClick.Invoke();
            Assert.That(workstation.GetUpgradeItem(0), Is.Null);
            Assert.That(workstation.GetUpgradeItem(1), Is.Null);

            laboratory.Find("ScanMapButton").GetComponent<Button>()
                .onClick.Invoke();
            LaboratoryInventoryItemDrag scanAnomaly =
                FindPlayerInventoryDrag(laboratory, anomaly);
            InventorySlotView scanSlot = GetSpawnedInventorySlot(
                scanScreen.transform.Find(
                    "background_Screen_Storage_Slot/Slot"));
            scanSlot.GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(scanAnomaly);
            Assert.That(research.LoadedItem, Is.SameAs(anomaly));

            LaboratoryInventoryItemDrag scanRecord =
                FindPlayerInventoryDrag(laboratory, record);
            Assert.That(scanRecord, Is.Not.Null);
            scanRecord.OnPointerDown(
                new PointerEventData(EventSystem.current));
            Assert.That(
                laboratoryInfoName.GetType().GetProperty("text")
                    ?.GetValue(laboratoryInfoName)?.ToString(),
                Is.EqualTo(record.DisplayName),
                "Starting a drag did not update laboratory item info.");
            scanSlot.GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(scanRecord);
            Assert.That(
                research.LoadedItem,
                Is.SameAs(record),
                "A different item type did not replace the loaded sample.");
            Assert.That(
                inventory.GetItem(
                    InventorySlotGroup.Anomaly,
                    0),
                Is.SameAs(anomaly),
                "The replaced anomaly did not return to its nearest typed slot.");

            Button scanButton = FindDescendant(
                scanScreen,
                "ScanButton").GetComponent<Button>();
            Button scanDrop = FindDescendant(
                scanScreen,
                "DropButton").GetComponent<Button>();
            Transform scanProgressTransform = FindDescendant(
                scanScreen,
                "Text_progress");
            Component scanProgressText =
                scanProgressTransform.GetComponent("TextMeshProUGUI");
            Assert.That(scanProgressTransform, Is.Not.Null);
            Assert.That(scanProgressText, Is.Not.Null);
            Assert.That(
                scanProgressTransform.gameObject.activeSelf,
                Is.False,
                "Scan progress must stay hidden before scanning.");
            Assert.That(scanButton.interactable, Is.True, "Scan button stayed disabled.");
            scanButton.onClick.Invoke();
            yield return null;

            Assert.That(
                research.State,
                Is.EqualTo(ResearchController.ResearchState.Analyzing));
            Assert.That(scanDrop.interactable, Is.False);
            Assert.That(
                scanSlot.LaboratoryDrag.enabled,
                Is.False,
                "The sample must not be draggable while scanning.");
            Assert.That(
                scanProgressTransform.gameObject.activeSelf,
                Is.True,
                "Scan progress did not appear.");
            Assert.That(
                scanProgressText.GetType().GetProperty("text")
                    ?.GetValue(scanProgressText)?.ToString(),
                Does.Match(@"^Progress - \d+%$"));

            research.AdvanceAnalysis(
                record.ResearchDefinition.AnalysisDuration * 0.5f);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(
                scanProgressTransform.gameObject.activeSelf,
                Is.True);
            Assert.That(
                scanProgressText.GetType().GetProperty("text")
                    ?.GetValue(scanProgressText)?.ToString(),
                Is.Not.EqualTo("Progress - 0%"),
                "Scan percentage did not change.");

            research.AdvanceAnalysis(999f);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(scanDrop.interactable, Is.True, "Scan Drop stayed disabled.");
            Assert.That(scanSlot.LaboratoryDrag.enabled, Is.True, "Scanned sample stayed locked.");
            Assert.That(
                scanProgressTransform.gameObject.activeSelf,
                Is.False,
                "Scan progress stayed visible after completion.");
            scanDrop.onClick.Invoke();
            Assert.That(research.LoadedItem, Is.Null);
            Assert.That(inventory.Contains(anomaly.ItemId), Is.True);
            Assert.That(
                inventory.Contains(record.ItemId),
                Is.True,
                "Scan Drop did not return the replacement sample.");

            LaboratoryInventoryItemDrag firstAnomalyScan =
                FindPlayerInventoryDrag(laboratory, anomaly);
            scanSlot.GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(firstAnomalyScan);
            Assert.That(research.LoadedItem, Is.SameAs(anomaly));
            Assert.That(research.LoadedItemInstance.IsScanned, Is.False);
            Assert.That(scanButton.interactable, Is.True);
            scanButton.onClick.Invoke();
            research.AdvanceAnalysis(999f);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(research.LoadedItemInstance.IsScanned, Is.True);
            scanDrop.onClick.Invoke();

            laboratory.Find("UpgradeMapButton").GetComponent<Button>()
                .onClick.Invoke();
            LaboratoryInventoryItemDrag synthesizedIntegrator =
                FindPlayerInventoryDrag(laboratory, integrator);
            LaboratoryInventoryItemDrag synthesizedAnomaly =
                FindPlayerInventoryDrag(laboratory, anomaly);
            Assert.That(synthesizedIntegrator, Is.Not.Null);
            Assert.That(synthesizedAnomaly, Is.Not.Null);
            GetSpawnedInventorySlot(upgradeSlot01)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(synthesizedIntegrator);
            GetSpawnedInventorySlot(upgradeSlot02)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(synthesizedAnomaly);

            Button synthesisButton =
                FindDescendant(upgradeScreen, "UpgradeButton")
                    .GetComponent<Button>();
            Assert.That(
                synthesisButton.interactable,
                Is.True,
                "UpgradeButton stayed disabled for an analyzed IO shard.");
            synthesisButton.onClick.Invoke();

            ItemInstance synthesizedTool =
                workstation.GetUpgradeItem(0);
            Assert.That(synthesizedTool, Is.Not.Null);
            Assert.That(
                synthesizedTool.IntegratedAnomaly,
                Is.SameAs(anomaly));
            Assert.That(synthesizedTool.AnomalyCharges, Is.EqualTo(1));
            Assert.That(synthesizedTool.IsFullyCharged, Is.True);
            Assert.That(
                workstation.GetUpgradeItem(1),
                Is.Null,
                "Synthesis did not consume the IO shard.");

            FindDescendant(upgradeScreen, "DropButton")
                .GetComponent<Button>().onClick.Invoke();
            Assert.That(inventory.Contains(integrator.ItemId), Is.True);
            Assert.That(inventory.Contains(anomaly.ItemId), Is.False);

            ItemInstance equippedIntegrator =
                inventory.QuickAccessItemInstances.FirstOrDefault(
                    instance => instance?.ItemData == integrator);
            PlayerEquipmentController equipmentController =
                inventory.GetComponent<PlayerEquipmentController>();
            Assert.That(equippedIntegrator, Is.Not.Null);
            Assert.That(equipmentController, Is.Not.Null);
            Assert.That(
                equipmentController.TryUseIntegratedAnomaly(
                    equippedIntegrator),
                Is.True,
                "R activation failed for the IO Integrator.");
            Assert.That(equippedIntegrator.Charge, Is.Zero);
            Assert.That(equippedIntegrator.IntegratedAnomaly, Is.Null);

            laboratory.Find("PowerMapButton").GetComponent<Button>()
                .onClick.Invoke();
            LaboratoryInventoryItemDrag dischargedIntegrator =
                FindPlayerInventoryDrag(laboratory, integrator);
            powerSlot.GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(dischargedIntegrator);
            Assert.That(
                workstation.GetChargingItem(0)?.ItemData,
                Is.SameAs(integrator));
            workstation.AdvanceCharging(10f);
            Assert.That(
                workstation.GetChargingItem(0)?.IsFullyCharged,
                Is.True,
                "The IO Integrator did not recharge.");
            FindDescendant(powerScreen, "DropButton")
                .GetComponent<Button>().onClick.Invoke();

            Assert.That(
                inventory.AddItem(anomaly),
                Is.True,
                "A second shard was not added.");
            ItemInstance secondAnomalyInstance =
                inventory.AnomalyItemInstances.First(
                    instance => instance?.ItemData == anomaly);
            Assert.That(
                secondAnomalyInstance.IsScanned,
                Is.False,
                "A new instance must not inherit scan state from its type.");

            laboratory.Find("UpgradeMapButton").GetComponent<Button>()
                .onClick.Invoke();
            LaboratoryInventoryItemDrag rechargedIntegrator =
                FindPlayerInventoryDrag(laboratory, integrator);
            LaboratoryInventoryItemDrag secondAnalyzedAnomaly =
                FindPlayerInventoryDrag(laboratory, anomaly);
            GetSpawnedInventorySlot(upgradeSlot01)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(rechargedIntegrator);
            GetSpawnedInventorySlot(upgradeSlot02)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(secondAnalyzedAnomaly);
            Assert.That(
                synthesisButton.interactable,
                Is.False,
                "An unscanned second shard incorrectly inherited access.");

            FindDescendant(upgradeScreen, "DropButton")
                .GetComponent<Button>().onClick.Invoke();
            laboratory.Find("ScanMapButton").GetComponent<Button>()
                .onClick.Invoke();
            LaboratoryInventoryItemDrag secondAnomalyScan =
                FindPlayerInventoryDrag(laboratory, anomaly);
            scanSlot.GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(secondAnomalyScan);
            Assert.That(
                research.LoadedItemInstance,
                Is.SameAs(secondAnomalyInstance));
            Assert.That(scanButton.interactable, Is.True);
            scanButton.onClick.Invoke();
            research.AdvanceAnalysis(999f);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(secondAnomalyInstance.IsScanned, Is.True);
            Assert.That(
                research.AnalyzedResearchIds.Count,
                Is.EqualTo(2),
                "The known anomaly type must not create a duplicate research id.");
            scanDrop.onClick.Invoke();

            laboratory.Find("UpgradeMapButton").GetComponent<Button>()
                .onClick.Invoke();
            rechargedIntegrator =
                FindPlayerInventoryDrag(laboratory, integrator);
            secondAnalyzedAnomaly =
                FindPlayerInventoryDrag(laboratory, anomaly);
            GetSpawnedInventorySlot(upgradeSlot01)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(rechargedIntegrator);
            GetSpawnedInventorySlot(upgradeSlot02)
                .GetComponent<LaboratoryItemDropSlot>()
                .ItemDropped.Invoke(secondAnalyzedAnomaly);
            Assert.That(
                synthesisButton.interactable,
                Is.True,
                "The second shard stayed locked after its own scan.");
            synthesisButton.onClick.Invoke();
            Assert.That(
                workstation.GetUpgradeItem(0)?.IntegratedAnomaly,
                Is.SameAs(anomaly));
            Assert.That(workstation.GetUpgradeItem(1), Is.Null);

            hud.CloseAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DevicesPanelMatchesTypedStationStorageCapacities()
        {
            SceneManager.LoadScene("MainScene");
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
            SceneManager.LoadScene("MainScene");
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
            SceneManager.LoadScene("MainScene");
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
            InventorySlotView destinationView =
                GetSpawnedInventorySlot(destinationRoot);
            Assert.That(
                destinationView,
                Is.Not.Null,
                "Storage Slot_1 did not spawn P_InventorySlot.");
            LaboratoryItemDropSlot destination =
                destinationView.GetComponent<LaboratoryItemDropSlot>();
            Assert.That(source, Is.Not.Null, "Occupied inventory slot has no drag source.");
            Assert.That(destination, Is.Not.Null, "Storage slot has no drop target.");
            Transform destinationIconRoot = destination.transform.Find("Icon");
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

            source.OnPointerDown(pointer);
            Component storageInfoName = FindDescendant(
                    storageScreenRoot,
                    "Text_Name")
                .GetComponent("TextMeshProUGUI");
            Assert.That(
                storageInfoName.GetType().GetProperty("text")
                    ?.GetValue(storageInfoName)?.ToString(),
                Is.EqualTo(item.DisplayName),
                "Starting a storage drag did not update item info.");
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
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            PlayerInventory inventory = Object.FindFirstObjectByType<PlayerInventory>();
            StationStorageController storage = StationStorageController.Instance;
            BootInitializer runtime = BootInitializer.Instance;
            ItemCatalogData catalog = Resources.Load<ItemCatalogData>("ItemCatalog_Default");
            ItemData servoDrive = catalog != null ? catalog.Find("servo_drive_01") : null;
            Assert.That(inventory, Is.Not.Null);
            Assert.That(storage, Is.Not.Null);
            Assert.That(runtime, Is.Not.Null);
            Assert.That(servoDrive, Is.Not.Null);

            int storedBefore = storage.Count;
            Assert.That(inventory.AddItem(servoDrive), Is.True);

            Assert.That(
                runtime.LoadGameplayScene("Expedition_01", string.Empty),
                Is.True);
            yield return WaitForScene("Expedition_01");
            Assert.That(
                runtime.LoadGameplayScene("Player_Station", "Station_Start"),
                Is.True);
            yield return WaitForScene("Player_Station");
            yield return null;

            Assert.That(inventory.Contains("servo_drive_01"), Is.True);
            Assert.That(storage.Count, Is.EqualTo(storedBefore));
        }

        [UnityTest]
        public IEnumerator InteractionTargetUsesAimCameraWithoutCombatAim()
        {
            SceneManager.LoadScene("MainScene");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            PlayerInteractionController interaction =
                Object.FindFirstObjectByType<PlayerInteractionController>();
            PlayerFollowCamera followCamera =
                Object.FindFirstObjectByType<PlayerFollowCamera>();
            LaboratoryTableInteractable laboratory =
                Object.FindFirstObjectByType<LaboratoryTableInteractable>();

            Assert.That(interaction, Is.Not.Null);
            Assert.That(followCamera, Is.Not.Null);
            Assert.That(laboratory, Is.Not.Null);

            MethodInfo setCurrentInteractable =
                typeof(PlayerInteractionController).GetMethod(
                    "SetCurrentInteractable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(setCurrentInteractable, Is.Not.Null);

            interaction.enabled = false;

            setCurrentInteractable.Invoke(
                interaction,
                new object[] { laboratory });
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.That(followCamera.IsInteractionFocused, Is.True);
            Assert.That(followCamera.IsAimCameraActive, Is.True);
            Assert.That(
                followCamera.IsAiming,
                Is.False,
                "Interaction focus must not enable combat aim or weapon locomotion.");

            setCurrentInteractable.Invoke(
                interaction,
                new object[] { null });
            yield return new WaitForSecondsRealtime(0.5f);

            Assert.That(followCamera.IsInteractionFocused, Is.False);
            Assert.That(followCamera.IsAimCameraActive, Is.False);
            Assert.That(
                followCamera.GetDistance(),
                Is.EqualTo(followCamera.GetTargetDistance()).Within(0.05f));
        }

        private static int CountDirectSlotButtons(Transform root)
        {
            Assert.That(root, Is.Not.Null);
            int count = 0;
            for (int index = 0; index < root.childCount; index++)
            {
                Transform spawnPoint = root.GetChild(index);
                if (spawnPoint.name.StartsWith("Slot_", StringComparison.Ordinal) &&
                    GetSpawnedInventorySlot(spawnPoint)?.Button != null)
                    count++;
            }
            return count;
        }

        private static InventorySlotView GetSpawnedInventorySlot(
            Transform spawnPoint)
        {
            if (spawnPoint == null)
                return null;

            for (int index = 0; index < spawnPoint.childCount; index++)
            {
                InventorySlotView view =
                    spawnPoint.GetChild(index).GetComponent<InventorySlotView>();
                if (view != null)
                    return view;
            }

            return null;
        }

        private static void AssertLaboratoryInventoryGroup(
            Transform sharedInventory,
            string groupName,
            int expectedCount)
        {
            Transform root = FindDescendant(sharedInventory, groupName);
            Assert.That(root, Is.Not.Null);
            Assert.That(root.childCount, Is.EqualTo(expectedCount));
            for (int index = 0; index < expectedCount; index++)
            {
                Transform spawnPoint = root.GetChild(index);
                Assert.That(
                    GetSpawnedInventorySlot(spawnPoint),
                    Is.Not.Null,
                    $"{groupName}/{spawnPoint.name} has no P_InventorySlot.");
            }
        }

        private static LaboratoryInventoryItemDrag FindPlayerInventoryDrag(
            Transform root,
            ItemData item)
        {
            foreach (LaboratoryInventoryItemDrag drag in
                     root.GetComponentsInChildren<
                         LaboratoryInventoryItemDrag>(true))
            {
                if (drag.Item == item &&
                    drag.SourceIndex >= 0 &&
                    !drag.IsLaboratorySource &&
                    !drag.IsChargingSource &&
                    !drag.IsUpgradeSource &&
                    !drag.IsStationStorageSource)
                {
                    return drag;
                }
            }

            return null;
        }

        private static void DropThroughUi(
            LaboratoryInventoryItemDrag source,
            LaboratoryItemDropSlot destination)
        {
            Assert.That(source, Is.Not.Null);
            Assert.That(destination, Is.Not.Null);
            Assert.That(EventSystem.current, Is.Not.Null);

            RectTransform destinationRect =
                (RectTransform)destination.transform;
            Canvas canvas = destination.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas.renderMode ==
                RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;
            Vector2 screenPoint =
                RectTransformUtility.WorldToScreenPoint(
                    eventCamera,
                    destinationRect.TransformPoint(
                        destinationRect.rect.center));
            PointerEventData pointer =
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    position = screenPoint,
                    pointerDrag = source.gameObject
                };

            source.OnBeginDrag(pointer);
            var hits =
                new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            RaycastResult dropHit = hits.Find(hit =>
                hit.gameObject.GetComponentInParent<
                    LaboratoryItemDropSlot>() == destination);
            string raycastStack = string.Join(
                "\n",
                hits.ConvertAll(hit =>
                    GetHierarchyPath(hit.gameObject.transform)));
            Graphic destinationGraphic =
                destination.GetComponent<Graphic>();
            CanvasGroup destinationGroup =
                destination.GetComponent<CanvasGroup>();
            string destinationState =
                $"path={GetHierarchyPath(destination.transform)}, " +
                $"active={destination.gameObject.activeInHierarchy}, " +
                $"rect={destinationRect.rect}, " +
                $"world={destinationRect.position}, " +
                $"screen={screenPoint}, " +
                $"graphicEnabled={destinationGraphic != null && destinationGraphic.enabled}, " +
                $"raycastTarget={destinationGraphic != null && destinationGraphic.raycastTarget}, " +
                $"depth={(destinationGraphic != null ? destinationGraphic.depth : -999)}, " +
                $"groupBlocks={destinationGroup != null && destinationGroup.blocksRaycasts}, " +
                $"groupInteractable={destinationGroup != null && destinationGroup.interactable}";
            Assert.That(
                dropHit.gameObject,
                Is.Not.Null,
                "Laboratory slot is blocked from UI raycasts. " +
                destinationState + "\nHits:\n" + raycastStack);

            ExecuteEvents.ExecuteHierarchy(
                dropHit.gameObject,
                pointer,
                ExecuteEvents.dropHandler);
            source.OnEndDrag(pointer);
        }

        private static void ClickThroughUi(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(EventSystem.current, Is.Not.Null);

            RectTransform rect = (RectTransform)button.transform;
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas.renderMode ==
                RenderMode.ScreenSpaceOverlay
                    ? null
                    : canvas.worldCamera;
            Vector2 screenPoint =
                RectTransformUtility.WorldToScreenPoint(
                    eventCamera,
                    rect.TransformPoint(rect.rect.center));
            PointerEventData pointer =
                new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    position = screenPoint
                };

            var hits =
                new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            RaycastResult clickHit = hits.Find(hit =>
                hit.gameObject.GetComponentInParent<Button>() == button);
            string raycastStack = string.Join(
                "\n",
                hits.ConvertAll(hit =>
                    GetHierarchyPath(hit.gameObject.transform)));
            Assert.That(
                clickHit.gameObject,
                Is.Not.Null,
                "Laboratory inventory slot cannot receive clicks. Hits:\n" +
                raycastStack);
            ExecuteEvents.ExecuteHierarchy(
                clickHit.gameObject,
                pointer,
                ExecuteEvents.pointerClickHandler);
        }

        private static Transform FindDescendant(
            Transform root,
            string objectName)
        {
            if (root == null)
                return null;
            if (root.name == objectName)
                return root;

            for (int index = 0; index < root.childCount; index++)
            {
                Transform found = FindDescendant(
                    root.GetChild(index),
                    objectName);
                if (found != null)
                    return found;
            }

            return null;
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
            while (SceneManager.GetActiveScene().name != sceneName ||
                   (BootInitializer.Instance != null &&
                    BootInitializer.Instance.IsLoading))
            {
                yield return null;
            }
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
