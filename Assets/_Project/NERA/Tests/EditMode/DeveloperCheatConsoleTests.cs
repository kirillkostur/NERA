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
                serialized.FindProperty("expeditionButtons").arraySize,
                Is.EqualTo(8));
            Assert.That(
                serialized.FindProperty("signalButtons").arraySize,
                Is.EqualTo(12));
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
                serialized.FindProperty("ioEnemyPrefab").objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("languageButton").objectReferenceValue,
                Is.Not.Null);
            Button languageButton = serialized.FindProperty("languageButton")
                .objectReferenceValue as Button;
            RectTransform languageRect =
                languageButton.GetComponent<RectTransform>();
            Assert.That(languageRect.anchoredPosition,
                Is.EqualTo(new Vector2(30f, -1002f)));
            Assert.That(languageRect.sizeDelta,
                Is.EqualTo(new Vector2(220f, 48f)));

            string[] labels = prefab
                .GetComponentsInChildren<Text>(true)
                .Select(text => text.text)
                .ToArray();
            Assert.That(labels, Does.Contain("ДОМОЙ"));
            Assert.That(labels, Does.Contain("ЭКСПЕДИЦИЯ 8"));
            Assert.That(labels, Does.Contain("СИГНАЛ 12"));
            Assert.That(labels, Does.Contain("ЗАСПАВНИТЬ IO"));
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
    }
}
