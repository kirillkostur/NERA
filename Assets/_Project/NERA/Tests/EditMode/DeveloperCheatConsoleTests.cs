using System.Linq;
using NERA.Development;
using NERA.Items;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Tests
{
    public sealed class DeveloperCheatConsoleTests
    {
        private const string PrefabPath =
            "Assets/_Project/NERA/Prefabs/Developer/" +
            "P_DeveloperCheatConsole.prefab";
        private const string MainScenePath =
            "Assets/_Project/NERA/Scenes/MainScene.unity";

[Test]
        public void CheatConsolePrefabContainsStaticConfiguredControls()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);
            Assert.That(prefab, Is.Not.Null);

            DeveloperCheatConsoleController controller =
                prefab.GetComponent<DeveloperCheatConsoleController>();
            Assert.That(controller, Is.Not.Null);

            var serialized = new SerializedObject(controller);
            Assert.That(
                serialized.FindProperty("windowRoot").objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("timerButton").objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("expeditionDropdownButton")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("signalDropdownButton")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("inventoryDropdownButton")
                    .objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("expeditionButtons").arraySize,
                Is.EqualTo(8));
            Assert.That(
                serialized.FindProperty("signalButtons").arraySize,
                Is.EqualTo(12));
            Assert.That(
                serialized.FindProperty("inventoryGroupButtons").arraySize,
                Is.EqualTo(6));
            Assert.That(
                serialized.FindProperty("inventoryGroupRoots").arraySize,
                Is.EqualTo(6));
            Assert.That(
                serialized.FindProperty("itemButtons").arraySize,
                Is.EqualTo(27));
            Assert.That(
                serialized.FindProperty("inventoryItems").arraySize,
                Is.EqualTo(27));
            Assert.That(
                serialized.FindProperty("stationEnableButtons").arraySize,
                Is.EqualTo(7));
            Assert.That(
                serialized.FindProperty("stationDisableButtons").arraySize,
                Is.EqualTo(7));
            Assert.That(
                serialized.FindProperty("batteryChargeButtons").arraySize,
                Is.EqualTo(5));

            Assert.That(
                serialized.FindProperty("spawnIoButtons").arraySize,
                Is.EqualTo(5));
            Assert.That(
                serialized.FindProperty("ioEnemyPrefabs").arraySize,
                Is.EqualTo(5));
            Assert.That(
                serialized.FindProperty("killIoButton").objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("languageButton").objectReferenceValue,
                Is.Not.Null);

            for (int index = 0; index < 5; index++)
            {
                Assert.That(
                    serialized.FindProperty("spawnIoButtons")
                        .GetArrayElementAtIndex(index)
                        .objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    serialized.FindProperty("ioEnemyPrefabs")
                        .GetArrayElementAtIndex(index)
                        .objectReferenceValue,
                    Is.Not.Null);
            }

            GameObject expeditionDropdown = serialized
                .FindProperty("expeditionDropdownRoot")
                .objectReferenceValue as GameObject;
            GameObject signalDropdown = serialized
                .FindProperty("signalDropdownRoot")
                .objectReferenceValue as GameObject;
            GameObject inventoryDropdown = serialized
                .FindProperty("inventoryDropdownRoot")
                .objectReferenceValue as GameObject;
            Assert.That(expeditionDropdown, Is.Not.Null);
            Assert.That(signalDropdown, Is.Not.Null);
            Assert.That(inventoryDropdown, Is.Not.Null);
            Assert.That(expeditionDropdown.activeSelf, Is.False);
            Assert.That(signalDropdown.activeSelf, Is.False);
            Assert.That(inventoryDropdown.activeSelf, Is.False);

            SerializedProperty groupRoots =
                serialized.FindProperty("inventoryGroupRoots");
            for (int index = 0; index < groupRoots.arraySize; index++)
            {
                GameObject group = groupRoots.GetArrayElementAtIndex(index)
                    .objectReferenceValue as GameObject;
                Assert.That(group, Is.Not.Null);
                Assert.That(group.activeSelf, Is.False);
            }

            Button languageButton = serialized.FindProperty("languageButton")
                .objectReferenceValue as Button;
            RectTransform languageRect =
                languageButton.GetComponent<RectTransform>();
            Assert.That(languageRect.anchoredPosition,
                Is.EqualTo(new Vector2(30f, -1002f)));
            Assert.That(languageRect.sizeDelta,
                Is.EqualTo(new Vector2(220f, 48f)));

            Button timerButton = serialized.FindProperty("timerButton")
                .objectReferenceValue as Button;
            RectTransform timerRect =
                timerButton.GetComponent<RectTransform>();
            Assert.That(timerRect.anchoredPosition,
                Is.EqualTo(new Vector2(1260f, -72f)));
            Assert.That(timerRect.sizeDelta,
                Is.EqualTo(new Vector2(170f, 48f)));

            SerializedProperty batteryChargeButtons =
                serialized.FindProperty("batteryChargeButtons");
            for (int index = 0;
                 index < batteryChargeButtons.arraySize;
                 index++)
            {
                Button chargeButton = batteryChargeButtons
                    .GetArrayElementAtIndex(index)
                    .objectReferenceValue as Button;
                Assert.That(chargeButton, Is.Not.Null);
                RectTransform chargeRect =
                    chargeButton.GetComponent<RectTransform>();
                Assert.That(
                    chargeRect.anchoredPosition,
                    Is.EqualTo(new Vector2(
                        1820f,
                        -(150f + index * 56f))));
                Assert.That(
                    chargeRect.sizeDelta,
                    Is.EqualTo(new Vector2(70f, 44f)));
                Assert.That(
                    chargeButton.GetComponentInChildren<Text>(true).text,
                    Is.EqualTo($"{index * 25}%"));
            }


            SerializedProperty ioButtons =
                serialized.FindProperty("spawnIoButtons");
            Color[] ioColors = Enumerable.Range(0, ioButtons.arraySize)
                .Select(index =>
                    (ioButtons.GetArrayElementAtIndex(index)
                        .objectReferenceValue as Button).image.color)
                .ToArray();
            Assert.That(ioColors.Distinct().Count(), Is.EqualTo(5));
            for (int index = 0; index < ioButtons.arraySize; index++)
            {
                Button ioButton = ioButtons.GetArrayElementAtIndex(index)
                    .objectReferenceValue as Button;
                Assert.That(ioButton.GetComponent<RectTransform>().sizeDelta,
                    Is.EqualTo(new Vector2(52f, 52f)));
            }

            Button terminalEnable = serialized
                .FindProperty("stationEnableButtons")
                .GetArrayElementAtIndex(6)
                .objectReferenceValue as Button;
            Assert.That(
                terminalEnable.GetComponent<RectTransform>()
                    .anchoredPosition.x,
                Is.GreaterThan(timerRect.anchoredPosition.x));

            string[] labels = prefab
                .GetComponentsInChildren<Text>(true)
                .Select(text => text.text)
                .ToArray();
            Assert.That(labels, Does.Contain("ДОМОЙ"));
            Assert.That(labels, Does.Contain("ЭКСПЕДИЦИИ"));
            Assert.That(labels, Does.Contain("СИГНАЛЫ"));
            Assert.That(labels, Does.Contain("ДЕТАЛИ В ИНВЕНТАРЬ"));
            Assert.That(labels, Does.Contain("ТАЙМЕР+"));
            Assert.That(labels, Does.Contain("ЭКСПЕДИЦИЯ 8"));
            Assert.That(labels, Does.Contain("СИГНАЛ 12"));
            Assert.That(labels, Does.Not.Contain("ЗАСПАВНИТЬ IO"));
            Assert.That(labels, Does.Contain("УБИТЬ IO"));
            Assert.That(labels, Does.Contain("ДРОН:"));
            Assert.That(labels, Does.Contain("АНТЕННА:"));
            Assert.That(labels, Does.Contain("ПАНЕЛЬ:"));
            Assert.That(labels, Does.Contain("БАТАРЕЯ:"));
            Assert.That(labels, Does.Contain("ТУРЕЛИ:"));
            Assert.That(labels, Does.Contain("СНАРЯЖЕНИЕ:"));
            Assert.That(labels, Does.Contain("ENERGY PISTOL"));
            Assert.That(labels, Does.Contain("IO INTEGRATOR"));
            Assert.That(labels, Does.Contain("ТЕРМИНАЛ"));
            Assert.That(labels, Does.Contain("ЯЗЫК"));
            Assert.That(labels.Count(label => label == "ВКЛ"), Is.EqualTo(7));
            Assert.That(labels.Count(label => label == "ВЫКЛ"), Is.EqualTo(7));
            Assert.That(labels.Count(label => label == "+1"), Is.EqualTo(27));

            string[] orderedItemIds = Enumerable
                .Range(0, serialized.FindProperty("inventoryItems").arraySize)
                .Select(index =>
                    (serialized.FindProperty("inventoryItems")
                        .GetArrayElementAtIndex(index)
                        .objectReferenceValue as ItemData)?.ItemId)
                .ToArray();
            Assert.That(
                orderedItemIds,
                Is.EqualTo(new[]
                {
                    "item_advanced_stabilizer_01",
                    "item_capacitor_01",
                    "item_power_core_01",
                    "item_propulsion_01",
                    "item_sensor_array_01",
                    "item_antenna_array_01",
                    "item_calibration_module_01",
                    "item_signal_amplifier_01",
                    "item_signal_processor_01",
                    "item_solar_cells_01",
                    "item_solar_dust_repeller_01",
                    "item_solar_mppt_controller_01",
                    "item_solar_tracker_01",
                    "item_energy_cells_01",
                    "item_cooling_system_01",
                    "item_power_bus_01",
                    "item_power_controller_01",
                    "item_power_converter_01",
                    "item_voltage_regulator_01",
                    "item_chassis_01",
                    "item_cooling_01",
                    "item_emitter_damage_01",
                    "item_sensor_01",
                    "item_servo_01",
                    "item_servo_drive_01",
                    "energy_pistol_01",
                    "io_integrator_01"
                }));
        }

        [Test]
        public void MainSceneReferencesCheatConsolePrefab()
        {
            string[] dependencies = AssetDatabase.GetDependencies(
                MainScenePath,
                true);
            Assert.That(dependencies, Does.Contain(PrefabPath));
        }
    

[Test]
        public void TimedStationSystemsExposeDeveloperSkipContract()
        {
            System.Type[] timedSystemTypes =
            {
                typeof(NERA.Drone.DroneScanController),
                typeof(NERA.Antenna.AntennaController),
                typeof(NERA.Energy.LaboratoryWorkstationController),
                typeof(NERA.Research.ResearchController),
                typeof(NERA.Maintenance.MaintainableObject)
            };

            foreach (System.Type systemType in timedSystemTypes)
            {
                Assert.That(
                    typeof(IDeveloperProgressSkippable)
                        .IsAssignableFrom(systemType),
                    Is.True,
                    systemType.FullName);
            }
        }


[Test]
        public void BatteryChargeCheatsSetMainChargeAndRefillReserve()
        {
            Assert.That(
                Object.FindFirstObjectByType<
                    NERA.Energy.EnergySystemController>(),
                Is.Null);

            var energyObject = new GameObject("EnergySystem_Test");
            try
            {
                var energy = energyObject.AddComponent<
                    NERA.Energy.EnergySystemController>();
                if (NERA.Energy.EnergySystemController.Instance != energy)
                {
                    typeof(NERA.Energy.EnergySystemController)
                        .GetMethod(
                            "Awake",
                            System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic)
                        ?.Invoke(energy, null);
                }

                Assert.That(
                    energy.RegisterBattery(
                        "station_battery",
                        1000f,
                        1000f,
                        100f,
                        20f),
                    Is.True);

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    PrefabPath);
                DeveloperCheatConsoleController controller =
                    prefab.GetComponent<DeveloperCheatConsoleController>();

                controller.SetMainBatteryCharge(0);
                Assert.That(energy.CurrentEnergy, Is.EqualTo(0f));
                Assert.That(energy.CurrentBackupReserve, Is.EqualTo(100f));

                energy.RestoreState(0f, 0f, true);
                controller.SetMainBatteryCharge(25);
                Assert.That(energy.CurrentEnergy, Is.EqualTo(250f));
                Assert.That(energy.CurrentBackupReserve, Is.EqualTo(100f));

                controller.SetMainBatteryCharge(50);
                Assert.That(energy.CurrentEnergy, Is.EqualTo(500f));
                Assert.That(energy.CurrentBackupReserve, Is.EqualTo(100f));

                controller.SetMainBatteryCharge(75);
                Assert.That(energy.CurrentEnergy, Is.EqualTo(750f));
                Assert.That(energy.CurrentBackupReserve, Is.EqualTo(100f));

                controller.SetMainBatteryCharge(100);

                Assert.That(energy.CurrentEnergy, Is.EqualTo(1000f));
                Assert.That(energy.CurrentBackupReserve, Is.EqualTo(100f));
            }
            finally
            {
                Object.DestroyImmediate(energyObject);
            }
        }
}
}
