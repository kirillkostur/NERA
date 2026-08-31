using System.Reflection;
using NERA.Combat;
using NERA.Graphics;
using NERA.Drone;
using NERA.Energy;
using NERA.Maintenance;
using NERA.Quests;
using NERA.Station;
using NERA.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NERA.Tests
{
    public sealed class StationWeatherMaintenanceTests
    {
        private GameObject weatherRoot;
        private GameObject deviceRoot;
        private StationEnvironmentConfig config;
        private StationWeatherController weather;
        private MaintainableObject maintainable;

        [SetUp]
        public void SetUp()
        {
            SetWeatherSingleton(null);
            SetDroneSingleton(null);

            config = ScriptableObject.CreateInstance<StationEnvironmentConfig>();
            weatherRoot = new GameObject("Test_Weather");
            weather = weatherRoot.AddComponent<StationWeatherController>();
            weather.Configure(config);
            weather.SetAutomaticWeatherEnabled(false);
            SetWeatherSingleton(weather);

            deviceRoot = new GameObject("Test_OutdoorDevice");
            maintainable = deviceRoot.AddComponent<MaintainableObject>();
            SerializedObject serialized = new SerializedObject(maintainable);
            serialized.FindProperty("exposedToWeather").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            maintainable.SetCondition(1f);
        }

        [TearDown]
        public void TearDown()
        {
            SetWeatherSingleton(null);
            SetDroneSingleton(null);
            Object.DestroyImmediate(deviceRoot);
            Object.DestroyImmediate(weatherRoot);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void SandExposureFillsSandAndBreaksObjectOverStormDuration()
        {
            Assert.That(weather.StartSandstorm(10f), Is.True);

            maintainable.AdvanceSandExposure(5f, 10f);
            Assert.That(maintainable.Condition, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(maintainable.SandAmount, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(maintainable.Service(), Is.False,
                "Cleaning must be blocked while the sandstorm is active.");

            maintainable.AdvanceSandExposure(5f, 10f);
            Assert.That(maintainable.Condition, Is.Zero.Within(0.001f));
            Assert.That(maintainable.SandAmount, Is.EqualTo(1f).Within(0.001f));
            Assert.That(maintainable.IsOperational, Is.False);
        }

        [Test]
        public void ContaminationEventFiresOncePerDirtyCycle()
        {
            int eventCount = 0;
            MaintainableObject reported = null;
            MaintainableObject.AnyContaminated += HandleContaminated;
            try
            {
                Assert.That(weather.StartSandstorm(10f), Is.True);
                maintainable.AdvanceSandExposure(1f, 10f);
                maintainable.AdvanceSandExposure(1f, 10f);

                Assert.That(eventCount, Is.EqualTo(1));
                Assert.That(reported, Is.SameAs(maintainable));

                weather.StopSandstorm();
                maintainable.CleanInstantly();
                Assert.That(weather.StartSandstorm(10f), Is.True);
                maintainable.AdvanceSandExposure(1f, 10f);

                Assert.That(eventCount, Is.EqualTo(2));
            }
            finally
            {
                MaintainableObject.AnyContaminated -= HandleContaminated;
                weather.StopSandstorm();
            }

            void HandleContaminated(MaintainableObject value)
            {
                eventCount++;
                reported = value;
            }
        }

        [Test]
        public void DroneAwayForEntireSandstormDoesNotNeedCleaning()
        {
            DroneScanController drone = ConfigureDeviceAsDrone();
            SetDroneAway(drone, true);

            Assert.That(drone.IsAtStation, Is.False);
            Assert.That(weather.StartSandstorm(10f), Is.True);

            maintainable.AdvanceSandExposure(10f, 10f);
            weather.AdvanceSimulation(10f);

            Assert.That(maintainable.Condition, Is.EqualTo(1f));
            Assert.That(maintainable.NeedsService, Is.False);
        }

        [Test]
        public void DroneReturningDuringSandstormOnlyGetsRemainingExposure()
        {
            DroneScanController drone = ConfigureDeviceAsDrone();
            SetDroneAway(drone, true);
            Assert.That(weather.StartSandstorm(10f), Is.True);

            weather.AdvanceSimulation(6f);
            SetDroneAway(drone, false);
            Assert.That(drone.IsAtStation, Is.True);

            maintainable.AdvanceSandExposure(4f, 10f);
            weather.AdvanceSimulation(4f);

            Assert.That(
                maintainable.Condition,
                Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(maintainable.IsSandClogged, Is.False);
        }

        [Test]
        public void CleaningAfterStormRestoresConditionAndSandAmount()
        {
            SerializedObject serialized = new SerializedObject(maintainable);
            serialized.FindProperty("cleaningDurationSeconds").floatValue = 4f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            weather.StartSandstorm(7f);
            maintainable.AdvanceSandExposure(7f, 7f);
            weather.StopSandstorm();

            Assert.That(maintainable.Service(), Is.True);
            Assert.That(maintainable.IsCleaning, Is.True);
            Assert.That(maintainable.Condition, Is.Zero);

            maintainable.AdvanceCleaning(2f);
            Assert.That(maintainable.CleaningProgress01,
                Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(maintainable.Condition,
                Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(maintainable.SandAmount,
                Is.EqualTo(0.5f).Within(0.001f));

            maintainable.AdvanceCleaning(2f);
            Assert.That(maintainable.Condition, Is.EqualTo(1f));
            Assert.That(maintainable.SandAmount, Is.Zero);
            Assert.That(maintainable.IsOperational, Is.True);
            Assert.That(maintainable.IsCleaning, Is.False);
        }

        [Test]
        public void AutomaticSandstormUsesConfiguredChanceAndDurationRange()
        {
            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("automaticSandstormsEnabled").boolValue = true;
            serialized.FindProperty("sandstormChancePerRoll").floatValue = 1f;
            serialized.FindProperty("sandstormRollIntervalMinSeconds")
                .floatValue = 1f;
            serialized.FindProperty("sandstormRollIntervalMaxSeconds")
                .floatValue = 1f;
            serialized.FindProperty("sandstormDurationMinSeconds")
                .floatValue = 7f;
            serialized.FindProperty("sandstormDurationMaxSeconds")
                .floatValue = 7f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            weather.Configure(config);
            weather.SetAutomaticWeatherEnabled(true);
            weather.AdvanceSimulation(1f);

            Assert.That(weather.IsSandstormActive, Is.True);
            Assert.That(
                weather.ActiveSandstormDuration,
                Is.EqualTo(7f).Within(0.001f));
        }

        [Test]
        public void FogExclusionVolumeUsesOrientedBoxesForShelter()
        {
            var root = new GameObject("Test_FogExclusionVolume");
            try
            {
                root.transform.SetPositionAndRotation(
                    new Vector3(10f, 2f, -4f),
                    Quaternion.Euler(0f, 35f, 0f));
                root.transform.localScale = new Vector3(2f, 1.5f, 0.75f);
                BoxCollider box = root.AddComponent<BoxCollider>();
                box.center = new Vector3(0.5f, 1f, -0.25f);
                box.size = new Vector3(4f, 2f, 6f);
                FogExclusionVolume volume =
                    root.AddComponent<FogExclusionVolume>();

                Vector3 inside = root.transform.TransformPoint(box.center);
                Vector3 outside = root.transform.TransformPoint(
                    box.center + Vector3.right * 2.1f);

                Assert.That(volume.ContainsWorldPoint(inside), Is.True);
                Assert.That(volume.ContainsWorldPoint(outside), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SandstormDamagesPlayerOnlyOutsideFogShelterOnStation()
        {
            var shelterRoot = new GameObject("Test_StormShelter");
            var playerRoot = new GameObject("Test_StormPlayer");
            try
            {
                BoxCollider shelter = shelterRoot.AddComponent<BoxCollider>();
                shelter.center = new Vector3(0f, 1f, 0f);
                shelter.size = new Vector3(10f, 4f, 10f);
                shelter.isTrigger = true;
                shelterRoot.AddComponent<FogExclusionVolume>();

                Rigidbody locomotionBody =
                    playerRoot.AddComponent<Rigidbody>();
                locomotionBody.isKinematic = true;
                CapsuleCollider exposureCollider =
                    playerRoot.AddComponent<CapsuleCollider>();
                exposureCollider.center = Vector3.up;
                exposureCollider.height = 2f;

                var ragdollRoot = new GameObject("Test_RagdollBody");
                ragdollRoot.transform.SetParent(playerRoot.transform);
                ragdollRoot.AddComponent<CapsuleCollider>();
                Rigidbody ragdollBody =
                    ragdollRoot.AddComponent<Rigidbody>();
                ragdollBody.isKinematic = true;

                PlayerHealth health = playerRoot.AddComponent<PlayerHealth>();
                health.RestoreFullHealth();

                SerializedObject serialized = new SerializedObject(config);
                serialized.FindProperty("sandstormPlayerDamage").floatValue =
                    10f;
                serialized.FindProperty(
                    "sandstormPlayerDamageIntervalSeconds").floatValue = 1f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                weather.Configure(config);

                BindingFlags flags =
                    BindingFlags.Instance | BindingFlags.NonPublic;
                FieldInfo healthField = typeof(StationWeatherController)
                    .GetField("playerHealth", flags);
                FieldInfo colliderField = typeof(StationWeatherController)
                    .GetField("playerExposureCollider", flags);
                MethodInfo damageTick = typeof(StationWeatherController)
                    .GetMethod(
                        "AdvancePlayerSandstormDamage",
                        flags,
                        null,
                        new[] { typeof(float), typeof(bool) },
                        null);

                Assert.That(healthField, Is.Not.Null);
                Assert.That(colliderField, Is.Not.Null);
                Assert.That(damageTick, Is.Not.Null);
                healthField.SetValue(weather, health);
                colliderField.SetValue(weather, exposureCollider);

                Physics.SyncTransforms();
                float fullHealth = health.CurrentHealth;
                damageTick.Invoke(weather, new object[] { 2f, true });
                Assert.That(health.CurrentHealth, Is.EqualTo(fullHealth));

                playerRoot.transform.position =
                    new Vector3(100f, 0f, 100f);
                Physics.SyncTransforms();
                damageTick.Invoke(weather, new object[] { 1f, true });
                Assert.That(
                    health.CurrentHealth,
                    Is.EqualTo(fullHealth - 10f));

                damageTick.Invoke(weather, new object[] { 1f, false });
                Assert.That(
                    health.CurrentHealth,
                    Is.EqualTo(fullHealth - 10f));
            }
            finally
            {
                Object.DestroyImmediate(playerRoot);
                Object.DestroyImmediate(shelterRoot);
            }
        }

        [Test]
        public void DefaultEnvironmentConfigReferencesSandstormRendering()
        {
            StationEnvironmentConfig production =
                StationEnvironmentConfig.LoadDefault();

            Assert.That(production.SandstormRendererFeature, Is.Not.Null);
            Assert.That(production.VolumetricFogMaterial, Is.Not.Null);
            Assert.That(production.ToggleRendererFeature, Is.True);
            Assert.That(
                production.FogDensityProperty,
                Is.EqualTo("_DensityMultiplier"));
            Assert.That(
                production.ClearFogDensity,
                Is.Zero);
            Assert.That(
                production.SandstormFogDensity,
                Is.EqualTo(0.3f));
            Assert.That(
                production.SandstormPlayerDamage,
                Is.EqualTo(5f));
            Assert.That(
                production.SandstormPlayerDamageIntervalSeconds,
                Is.EqualTo(1f));
        }

        [Test]
        public void SandstormFadesDensityBeforeDisablingFeature()
        {
            StationEnvironmentConfig production =
                StationEnvironmentConfig.LoadDefault();
            bool originalState = production.SandstormRendererFeature.isActive;
            Material material = production.VolumetricFogMaterial;
            string propertyName = production.FogDensityProperty;

            try
            {
                weather.Configure(production);
                weather.StartSandstorm(10f);
                Assert.That(
                    production.SandstormRendererFeature.isActive,
                    Is.True);
                Assert.That(
                    material.GetFloat(propertyName),
                    Is.Zero.Within(0.001f));
                Assert.That(weather.IsFogTransitionActive, Is.True);

                weather.AdvanceSimulation(
                    production.SandstormFogFadeDurationSeconds * 0.5f);
                Assert.That(
                    material.GetFloat(propertyName),
                    Is.GreaterThan(0f).And.LessThan(0.3f));

                weather.AdvanceSimulation(
                    production.SandstormFogFadeDurationSeconds);
                Assert.That(
                    material.GetFloat(propertyName),
                    Is.EqualTo(0.3f).Within(0.001f));
                Assert.That(weather.IsFogTransitionActive, Is.False);

                weather.StopSandstorm();
                Assert.That(
                    production.SandstormRendererFeature.isActive,
                    Is.True,
                    "The pass must stay enabled while the fog fades out.");
                Assert.That(weather.IsFogTransitionActive, Is.True);

                weather.AdvanceSimulation(
                    production.SandstormFogFadeDurationSeconds * 0.5f);
                Assert.That(
                    material.GetFloat(propertyName),
                    Is.GreaterThan(0f).And.LessThan(0.3f));
                Assert.That(
                    production.SandstormRendererFeature.isActive,
                    Is.True);

                weather.AdvanceSimulation(
                    production.SandstormFogFadeDurationSeconds);
                Assert.That(
                    material.GetFloat(propertyName),
                    Is.Zero.Within(0.001f));
                Assert.That(
                    production.SandstormRendererFeature.isActive,
                    Is.False);
                Assert.That(weather.IsFogTransitionActive, Is.False);
            }
            finally
            {
                material.SetFloat(
                    propertyName,
                    production.ClearFogDensity);
                production.SandstormRendererFeature.SetActive(originalState);
                weather.Configure(config);
            }
        }

        [Test]
        public void QuestCompletionCanStartConfiguredSandstorm()
        {
            GameObject questRoot = new GameObject("Test_WeatherQuest");
            QuestDefinition definition =
                ScriptableObject.CreateInstance<QuestDefinition>();
            QuestCatalog catalog = ScriptableObject.CreateInstance<QuestCatalog>();
            try
            {
                ConfigureWeatherQuest(definition);
                SerializedObject serializedCatalog =
                    new SerializedObject(catalog);
                SerializedProperty definitions =
                    serializedCatalog.FindProperty("definitions");
                definitions.arraySize = 1;
                definitions.GetArrayElementAtIndex(0).objectReferenceValue =
                    definition;
                serializedCatalog.ApplyModifiedPropertiesWithoutUndo();

                QuestController quests =
                    questRoot.AddComponent<QuestController>();
                SetQuestSingleton(quests);
                quests.Configure(catalog);

                quests.Report(QuestSignalType.Custom, "weather_quest_start");
                quests.Report(QuestSignalType.Custom, "weather_quest_finish");

                Assert.That(weather.IsSandstormActive, Is.True);
                Assert.That(
                    weather.ActiveSandstormDuration,
                    Is.EqualTo(7f).Within(0.001f));
            }
            finally
            {
                SetQuestSingleton(null);
                Object.DestroyImmediate(questRoot);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(definition);
            }
        }

        private static void ConfigureWeatherQuest(QuestDefinition definition)
        {
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("questId").stringValue = "weather_quest";
            serialized.FindProperty("title").stringValue = "Weather Quest";
            serialized.FindProperty("weatherActionOnCompletion")
                .enumValueIndex = (int)QuestWeatherAction.StartSandstorm;
            serialized.FindProperty("sandstormDurationMinSeconds")
                .floatValue = 7f;
            serialized.FindProperty("sandstormDurationMaxSeconds")
                .floatValue = 7f;

            SerializedProperty activation =
                serialized.FindProperty("activationConditions");
            activation.arraySize = 1;
            ConfigureQuestCondition(
                activation.GetArrayElementAtIndex(0),
                "weather_quest_start");

            SerializedProperty stages = serialized.FindProperty("stages");
            stages.arraySize = 1;
            SerializedProperty stage = stages.GetArrayElementAtIndex(0);
            stage.FindPropertyRelative("title").stringValue = "Wait";
            SerializedProperty completion =
                stage.FindPropertyRelative("completionConditions");
            completion.arraySize = 1;
            ConfigureQuestCondition(
                completion.GetArrayElementAtIndex(0),
                "weather_quest_finish");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureQuestCondition(
            SerializedProperty condition,
            string targetId)
        {
            condition.FindPropertyRelative("signalType").enumValueIndex =
                (int)QuestSignalType.Custom;
            condition.FindPropertyRelative("evaluation").enumValueIndex =
                (int)QuestConditionEvaluation.Event;
            condition.FindPropertyRelative("target").enumValueIndex =
                (int)QuestConditionTarget.SpecificObject;
            condition.FindPropertyRelative("targetId").stringValue = targetId;
            condition.FindPropertyRelative("requiredCount").intValue = 1;
        }

        private static void SetWeatherSingleton(
            StationWeatherController controller)
        {
            typeof(StationWeatherController)
                .GetProperty(
                    "Instance",
                    BindingFlags.Static | BindingFlags.Public)
                ?.GetSetMethod(true)
                ?.Invoke(null, new object[] { controller });
        }

        private DroneScanController ConfigureDeviceAsDrone()
        {
            StationObjectIdentity identity =
                deviceRoot.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Drone,
                "station_drone");

            SerializedObject serialized = new SerializedObject(maintainable);
            serialized.FindProperty("role").enumValueIndex =
                (int)MaintenanceRole.Drone;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            maintainable.SetCondition(1f);

            DroneScanController drone =
                deviceRoot.AddComponent<DroneScanController>();
            SetDroneSingleton(drone);
            return drone;
        }

        private static void SetDroneAway(
            DroneScanController drone,
            bool isAway)
        {
            typeof(DroneScanController)
                .GetField(
                    "scanTimerRunning",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(drone, isAway);
            typeof(DroneScanController)
                .GetField(
                    "waitingForReturnAnimationEvent",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(drone, false);
        }

        private static void SetDroneSingleton(DroneScanController controller)
        {
            typeof(DroneScanController)
                .GetProperty(
                    "Instance",
                    BindingFlags.Static | BindingFlags.Public)
                ?.GetSetMethod(true)
                ?.Invoke(null, new object[] { controller });
        }

        private static void SetQuestSingleton(QuestController controller)
        {
            typeof(QuestController)
                .GetProperty(
                    "Instance",
                    BindingFlags.Static | BindingFlags.Public)
                ?.GetSetMethod(true)
                ?.Invoke(null, new object[] { controller });
        }
    }

    public sealed class SandMaintenancePowerStateTests
    {
        private GameObject root;
        private StationSystemsController systems;
        private MaintainableObject maintainable;

        [SetUp]
        public void SetUp()
        {
            TestStationSystemsConfigFactory.SetSingleton(
                typeof(StationWeatherController),
                null);
            TestStationSystemsConfigFactory.SetSingleton(
                typeof(EnergySystemController),
                null);
            TestStationSystemsConfigFactory.SetSingleton(
                typeof(StationSystemsController),
                null);

            root = new GameObject("Test_SandPowerState");
            EnergySystemController energy =
                root.AddComponent<EnergySystemController>();
            TestStationSystemsConfigFactory.SetSingleton(
                typeof(EnergySystemController),
                energy);
            energy.RegisterBattery("test_battery", 1000f, 1000f);
            energy.SetGridEnabled(true);

            systems = root.AddComponent<StationSystemsController>();
            TestStationSystemsConfigFactory.SetSingleton(
                typeof(StationSystemsController),
                systems);
            systems.ResetSystems();

            StationObjectIdentity identity =
                root.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Antenna,
                "station_antenna");
            maintainable = root.AddComponent<MaintainableObject>();
            SerializedObject serialized = new SerializedObject(maintainable);
            serialized.FindProperty("role").enumValueIndex =
                (int)MaintenanceRole.Antenna;
            serialized.FindProperty("exposedToWeather").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            maintainable.SetCondition(1f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            TestStationSystemsConfigFactory.SetSingleton(
                typeof(EnergySystemController),
                null);
            TestStationSystemsConfigFactory.SetSingleton(
                typeof(StationSystemsController),
                null);
        }

        [Test]
        public void PreviouslyActiveObjectResumesAfterCleaning()
        {
            Assert.That(systems.SetRequestedActive(
                StationSystemType.Antenna,
                true,
                "station_antenna"), Is.True);

            maintainable.SetCondition(0f);
            Assert.That(systems.IsRequestedActive(
                StationSystemType.Antenna,
                "station_antenna"), Is.True);
            Assert.That(systems.CanStart(
                StationSystemType.Antenna,
                "station_antenna",
                out _), Is.False);

            Assert.That(maintainable.Service(), Is.True);
            maintainable.AdvanceCleaning(
                maintainable.CleaningDurationSeconds);
            Assert.That(systems.IsRequestedActive(
                StationSystemType.Antenna,
                "station_antenna"), Is.True);
        }

        [Test]
        public void PowerCutoffWhileDirtyKeepsObjectOffAfterCleaning()
        {
            systems.SetRequestedActive(
                StationSystemType.Antenna,
                true,
                "station_antenna");
            maintainable.SetCondition(0f);
            systems.DisableFromPowerLimit(
                StationSystemType.Antenna,
                "station_antenna");

            Assert.That(maintainable.Service(), Is.True);
            maintainable.AdvanceCleaning(
                maintainable.CleaningDurationSeconds);
            Assert.That(systems.IsRequestedActive(
                StationSystemType.Antenna,
                "station_antenna"), Is.False);
        }

        [Test]
        public void SwitchedOffObjectStaysOffAfterSandCleaning()
        {
            systems.SetRequestedActive(
                StationSystemType.Antenna,
                false,
                "station_antenna");
            maintainable.SetCondition(0f);

            Assert.That(maintainable.Service(), Is.True);
            maintainable.AdvanceCleaning(
                maintainable.CleaningDurationSeconds);
            Assert.That(systems.IsRequestedActive(
                StationSystemType.Antenna,
                "station_antenna"), Is.False);
        }
    }
}
