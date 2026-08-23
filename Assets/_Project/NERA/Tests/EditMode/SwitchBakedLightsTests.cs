using System.Reflection;
using NERA.Energy;
using NERA.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NERA.Tests
{
    public sealed class SwitchBakedLightsTests
    {
        private GameObject energyRoot;
        private GameObject lightingRoot;
        private EnergySystemController energy;
        private StationEnvironmentController environment;
        private StationWeatherController weather;
        private StationEnvironmentConfig environmentConfig;
        private SwitchBakedLights lighting;
        private Texture2D normalColor;
        private Texture2D normalDirection;
        private Texture2D warningColor;
        private Texture2D warningDirection;
        private Texture2D emergencyColor;
        private Texture2D emergencyDirection;
        private Light normalLight;
        private Light warningLight;
        private Light emergencyLight;
        private LightmapData[] previousLightmaps;
        private LightmapsMode previousLightmapsMode;

        [SetUp]
        public void SetUp()
        {
            previousLightmaps = LightmapSettings.lightmaps;
            previousLightmapsMode = LightmapSettings.lightmapsMode;
            SetEnergySingleton(null);

            energyRoot = new GameObject("Test_StationEnergy");
            energy = energyRoot.AddComponent<EnergySystemController>();
            SetEnergySingleton(energy);
            energy.RegisterBattery(
                "test_battery",
                1000f,
                1000f,
                100f,
                100f);
            energy.SetGridEnabled(true);

            environment = energyRoot.AddComponent<StationEnvironmentController>();
            weather = energyRoot.AddComponent<StationWeatherController>();
            environmentConfig =
                ScriptableObject.CreateInstance<StationEnvironmentConfig>();
            weather.Configure(environmentConfig);
            SerializedObject serializedEnvironment =
                new SerializedObject(environment);
            serializedEnvironment.FindProperty("weatherController")
                .objectReferenceValue = weather;
            serializedEnvironment.ApplyModifiedPropertiesWithoutUndo();
            SetSingleton(typeof(StationEnvironmentController), environment);

            lightingRoot = new GameObject("Test_StationLighting");
            lighting = lightingRoot.AddComponent<SwitchBakedLights>();

            normalColor = new Texture2D(1, 1);
            normalDirection = new Texture2D(1, 1);
            warningColor = new Texture2D(1, 1);
            warningDirection = new Texture2D(1, 1);
            emergencyColor = new Texture2D(1, 1);
            emergencyDirection = new Texture2D(1, 1);

            normalLight = CreateLight("Normal Light");
            warningLight = CreateLight("Warning Light");
            emergencyLight = CreateLight("Emergency Light");

            SerializedObject serialized = new SerializedObject(lighting);
            ConfigurePreset(
                serialized,
                "normalOperation",
                normalColor,
                normalDirection,
                normalLight);
            ConfigurePreset(
                serialized,
                "lowEnergyWarning",
                warningColor,
                warningDirection,
                warningLight);
            ConfigurePreset(
                serialized,
                "backupPowerEmergency",
                emergencyColor,
                emergencyDirection,
                emergencyLight);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            InvokeLifecycle("Awake");
            InvokeLifecycle("OnEnable");
        }

        [TearDown]
        public void TearDown()
        {
            if (lighting != null)
                InvokeLifecycle("OnDisable");

            LightmapSettings.lightmapsMode = previousLightmapsMode;
            LightmapSettings.lightmaps = previousLightmaps;

            Object.DestroyImmediate(lightingRoot);
            Object.DestroyImmediate(energyRoot);
            Object.DestroyImmediate(normalColor);
            Object.DestroyImmediate(normalDirection);
            Object.DestroyImmediate(warningColor);
            Object.DestroyImmediate(warningDirection);
            Object.DestroyImmediate(emergencyColor);
            Object.DestroyImmediate(emergencyDirection);
            Object.DestroyImmediate(environmentConfig);
            SetEnergySingleton(null);
            SetSingleton(typeof(StationEnvironmentController), null);
        }

        [Test]
        public void StationChargeSelectsNormalWarningAndEmergencyPresets()
        {
            energy.RestoreState(energy.TotalCapacity, 100f, true);
            AssertActivePreset(
                SwitchBakedLights.StationLightingMode.Normal,
                normalColor,
                normalLight);

            float warningCharge = energy.TotalCapacity *
                energy.Config.DefaultConsumerMinimumCharge01;
            energy.RestoreState(warningCharge, 100f, true);
            AssertActivePreset(
                SwitchBakedLights.StationLightingMode.LowEnergyWarning,
                warningColor,
                warningLight);

            energy.RestoreState(0f, 50f, true);
            AssertActivePreset(
                SwitchBakedLights.StationLightingMode.BackupPowerEmergency,
                emergencyColor,
                emergencyLight);
        }

        [Test]
        public void ExhaustedBackupKeepsEmergencyPresetButDisablesItsLights()
        {
            energy.RestoreState(0f, 0f, true);

            Assert.That(
                lighting.CurrentMode,
                Is.EqualTo(
                    SwitchBakedLights.StationLightingMode.BackupPowerEmergency));
            Assert.That(LightmapSettings.lightmaps[0].lightmapColor,
                Is.SameAs(emergencyColor));
            Assert.That(normalLight.enabled, Is.False);
            Assert.That(warningLight.enabled, Is.False);
            Assert.That(emergencyLight.enabled, Is.False);
        }

        [Test]
        public void DisabledBatteryUsesEmergencyAndReenablingRestoresNormal()
        {
            energy.RestoreState(energy.TotalCapacity, 100f, true);
            energy.SetGridEnabled(false);

            AssertActivePreset(
                SwitchBakedLights.StationLightingMode.BackupPowerEmergency,
                emergencyColor,
                emergencyLight);

            energy.SetGridEnabled(true);

            AssertActivePreset(
                SwitchBakedLights.StationLightingMode.Normal,
                normalColor,
                normalLight);
        }

        [Test]
        public void SandstormUsesWarningUntilWeatherClears()
        {
            energy.RestoreState(energy.TotalCapacity, 100f, true);

            environment.SetWeather(StationWeather.Sandstorm);
            AssertActivePreset(
                SwitchBakedLights.StationLightingMode.LowEnergyWarning,
                warningColor,
                warningLight);

            environment.SetWeather(StationWeather.Clear);
            AssertActivePreset(
                SwitchBakedLights.StationLightingMode.Normal,
                normalColor,
                normalLight);
        }

        [Test]
        public void MissingLightmapsDoNotBlockLightSourceSwitching()
        {
            energy.RestoreState(energy.TotalCapacity, 100f, true);
            ClearPresetArray("lowEnergyWarning", "lightmapColors");
            ClearPresetArray("lowEnergyWarning", "lightmapDirections");
            InvokeLifecycle("Awake");

            lighting.SetWarningLighting();

            Assert.That(
                lighting.CurrentMode,
                Is.EqualTo(
                    SwitchBakedLights.StationLightingMode.LowEnergyWarning));
            Assert.That(LightmapSettings.lightmaps, Has.Length.EqualTo(1));
            Assert.That(
                LightmapSettings.lightmaps[0].lightmapColor,
                Is.SameAs(normalColor));
            Assert.That(normalLight.enabled, Is.False);
            Assert.That(warningLight.enabled, Is.True);
            Assert.That(emergencyLight.enabled, Is.False);
        }

        [Test]
        public void MissingLightSourcesDoNotBlockLightmapSwitching()
        {
            energy.RestoreState(energy.TotalCapacity, 100f, true);
            ClearPresetArray("lowEnergyWarning", "lightSources");
            InvokeLifecycle("Awake");

            lighting.SetWarningLighting();

            Assert.That(
                lighting.CurrentMode,
                Is.EqualTo(
                    SwitchBakedLights.StationLightingMode.LowEnergyWarning));
            Assert.That(LightmapSettings.lightmaps, Has.Length.EqualTo(1));
            Assert.That(
                LightmapSettings.lightmaps[0].lightmapColor,
                Is.SameAs(warningColor));
            Assert.That(normalLight.enabled, Is.False);
            Assert.That(warningLight.enabled, Is.False);
            Assert.That(emergencyLight.enabled, Is.False);
        }

        [Test]
        public void MissingDirectionMapsUseAvailableColorMaps()
        {
            energy.RestoreState(energy.TotalCapacity, 100f, true);
            ClearPresetArray("lowEnergyWarning", "lightmapDirections");
            InvokeLifecycle("Awake");

            lighting.SetWarningLighting();

            Assert.That(
                lighting.CurrentMode,
                Is.EqualTo(
                    SwitchBakedLights.StationLightingMode.LowEnergyWarning));
            Assert.That(
                LightmapSettings.lightmapsMode,
                Is.EqualTo(LightmapsMode.NonDirectional));
            Assert.That(
                LightmapSettings.lightmaps[0].lightmapColor,
                Is.SameAs(warningColor));
            Assert.That(warningLight.enabled, Is.True);
        }

        [Test]
        public void ManualModeRemainsUntilAutomaticControlIsResumed()
        {
            energy.RestoreState(energy.TotalCapacity, 100f, true);
            lighting.SetWarningLighting();

            InvokeLifecycle("Update");

            AssertActivePreset(
                SwitchBakedLights.StationLightingMode.LowEnergyWarning,
                warningColor,
                warningLight);

            lighting.ResumeAutomaticStationControl();

            AssertActivePreset(
                SwitchBakedLights.StationLightingMode.Normal,
                normalColor,
                normalLight);

            environment.SetWeather(StationWeather.Sandstorm);
            lighting.SetNormalLighting();

            InvokeLifecycle("Update");

            AssertActivePreset(
                SwitchBakedLights.StationLightingMode.Normal,
                normalColor,
                normalLight);

            lighting.ResumeAutomaticStationControl();

            AssertActivePreset(
                SwitchBakedLights.StationLightingMode.LowEnergyWarning,
                warningColor,
                warningLight);
        }

        private Light CreateLight(string objectName)
        {
            GameObject lightObject = new GameObject(objectName);
            lightObject.transform.SetParent(lightingRoot.transform);
            return lightObject.AddComponent<Light>();
        }

        private void AssertActivePreset(
            SwitchBakedLights.StationLightingMode expectedMode,
            Texture2D expectedColor,
            Light expectedLight)
        {
            Assert.That(lighting.CurrentMode, Is.EqualTo(expectedMode));
            Assert.That(LightmapSettings.lightmaps, Has.Length.EqualTo(1));
            Assert.That(
                LightmapSettings.lightmaps[0].lightmapColor,
                Is.SameAs(expectedColor));
            Assert.That(
                normalLight.enabled,
                Is.EqualTo(expectedLight == normalLight));
            Assert.That(
                warningLight.enabled,
                Is.EqualTo(expectedLight == warningLight));
            Assert.That(
                emergencyLight.enabled,
                Is.EqualTo(expectedLight == emergencyLight));
        }

        private static void ConfigurePreset(
            SerializedObject serialized,
            string presetName,
            Texture2D color,
            Texture2D direction,
            Light lightSource)
        {
            SerializedProperty preset = serialized.FindProperty(presetName);
            SetSingleReference(
                preset.FindPropertyRelative("lightmapColors"),
                color);
            SetSingleReference(
                preset.FindPropertyRelative("lightmapDirections"),
                direction);
            SetSingleReference(
                preset.FindPropertyRelative("lightSources"),
                lightSource);
        }

        private static void SetSingleReference(
            SerializedProperty array,
            Object value)
        {
            array.arraySize = 1;
            array.GetArrayElementAtIndex(0).objectReferenceValue = value;
        }

        private void ClearPresetArray(string presetName, string arrayName)
        {
            SerializedObject serialized = new SerializedObject(lighting);
            SerializedProperty preset = serialized.FindProperty(presetName);
            preset.FindPropertyRelative(arrayName).arraySize = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void InvokeLifecycle(string methodName)
        {
            MethodInfo method = typeof(SwitchBakedLights).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(lighting, null);
        }

        private static void SetEnergySingleton(EnergySystemController value)
        {
            SetSingleton(typeof(EnergySystemController), value);
        }

        private static void SetSingleton(System.Type type, object value)
        {
            PropertyInfo instanceProperty = type.GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public);
            instanceProperty?.GetSetMethod(true)?.Invoke(
                null,
                new object[] { value });
        }
    }
}
