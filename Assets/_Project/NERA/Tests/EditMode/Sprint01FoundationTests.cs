using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using NeraInteractionMode = NERA.Interaction.InteractionMode;
using NERA.Antenna;
using NERA.Drone;
using NERA.Combat;
using NERA.Energy;
using NERA.Enemies;
using NERA.Expeditions;
using NERA.Inventory;
using NERA.Items;
using NERA.Library;
using NERA.Locations;
using NERA.Maintenance;
using NERA.Research;
using NERA.Station;
using NERA.Terminal;
using NERA.Core;
using NERA.Graphics;
using NERA.Quests;
using NERA.Save;

namespace NERA.Tests
{
    public sealed class Sprint01FoundationTests
    {
        private static readonly string[] RequiredBuildScenePrefix =
        {
            "Assets/_Project/NERA/Scenes/Boot.unity",
            "Assets/_Project/NERA/Scenes/MainScene.unity",
            "Assets/_Project/NERA/Scenes/Player_Station.unity"
        };

        [Test]
        public void RequiredScenesAreEnabledInBuildSettings()
        {
            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            foreach (string requiredScene in RequiredBuildScenePrefix)
            {
                Assert.That(
                    enabledScenes,
                    Does.Contain(requiredScene),
                    $"Required scene is missing or disabled: {requiredScene}"
                );
            }

            CollectionAssert.AreEqual(
                RequiredBuildScenePrefix,
                enabledScenes.Take(RequiredBuildScenePrefix.Length).ToArray()
            );
        }

        [Test]
        public void ProjectValidatorAcceptsCurrentProject()
        {
            Type validatorType = Type.GetType(
                "NERA.Editor.ProjectValidator, Assembly-CSharp-Editor");
            Assert.That(
                validatorType,
                Is.Not.Null,
                "Permanent project validator assembly is unavailable.");

            MethodInfo validateMethod = validatorType.GetMethod(
                "ValidateOrThrow",
                BindingFlags.Static | BindingFlags.Public);
            Assert.That(validateMethod, Is.Not.Null);
            Assert.DoesNotThrow(() => validateMethod.Invoke(null, null));
        }

        [Test]
        public void DefaultQuestCatalogIsValidAndDataDriven()
        {
            QuestCatalog catalog = QuestCatalog.LoadDefault();

            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryValidate(out string error), Is.True, error);
            Assert.That(catalog.Find("main.expedition_01"), Is.Not.Null);
            QuestDefinition cleaning =
                catalog.Find("side.clean_solar_panel");
            Assert.That(cleaning?.TargetScope,
                Is.EqualTo(QuestTargetScope.PerTriggeringObject));
            Assert.That(cleaning?.ActivationConditions[0].Target,
                Is.EqualTo(QuestConditionTarget.AnyObject));
            Assert.That(cleaning?.Stages[0].CompletionConditions[0].Target,
                Is.EqualTo(QuestConditionTarget.QuestTarget));
            Assert.That(catalog.Find("side.restore_turret")?.Availability,
                Is.EqualTo(QuestAvailability.Repeatable));

            Assert.That(
                Type.GetType(
                    "NERA.Editor.QuestDefinitionEditor, " +
                    "Assembly-CSharp-Editor"),
                Is.Not.Null);
            Assert.That(
                Type.GetType(
                    "NERA.Editor.QuestAuthoringWindow, " +
                    "Assembly-CSharp-Editor"),
                Is.Not.Null);
        }

        [Test]
        public void ItemDataUsesEquipmentConditionalInspector()
        {
            Type editorType = Type.GetType(
                "NERA.Editor.ItemDataEditor, Assembly-CSharp-Editor");
            Assert.That(editorType, Is.Not.Null);

            MethodInfo equipmentOnlyCheck = editorType.GetMethod(
                "IsEquipmentOnlyProperty",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(equipmentOnlyCheck, Is.Not.Null);

            foreach (string propertyName in new[]
                     {
                         "acceptsAnomalyIntegration",
                         "anomalyIntegrationDefinition",
                         "weaponDefinition",
                         "energyDefinition"
                     })
            {
                Assert.That(
                    equipmentOnlyCheck.Invoke(
                        null,
                        new object[] { propertyName }),
                    Is.EqualTo(true),
                    propertyName);
            }

            Assert.That(
                equipmentOnlyCheck.Invoke(
                    null,
                    new object[] { "researchDefinition" }),
                Is.EqualTo(false));
        }

        [Test]
        public void LocationConfigsUseValidSceneReferencesAndMapSlots()
        {
            string[] enabledScenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            HashSet<string> enabledScenes = new HashSet<string>(
                enabledScenePaths,
                StringComparer.Ordinal);
            HashSet<string> locationIds =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> scenePaths =
                new HashSet<string>(StringComparer.Ordinal);
            HashSet<MapSlotData> mapSlots = new HashSet<MapSlotData>();

            string[] locationGuids = AssetDatabase.FindAssets(
                $"t:{nameof(ExpeditionLocationData)}",
                new[] { "Assets/_Project/NERA/Configs" });
            Assert.That(locationGuids.Length, Is.GreaterThan(0));

            foreach (string guid in locationGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                ExpeditionLocationData location =
                    AssetDatabase.LoadAssetAtPath<ExpeditionLocationData>(
                        assetPath);
                Assert.That(location, Is.Not.Null, assetPath);
                Assert.That(
                    locationIds.Add(location.LocationId),
                    Is.True,
                    $"Duplicate Location Id in {assetPath}");
                Assert.That(
                    location.Scene,
                    Is.Not.Null,
                    $"Missing scene reference in {assetPath}");
                Assert.That(
                    location.Scene.IsConfigured,
                    Is.True,
                    $"Incomplete scene reference in {assetPath}");
                Assert.That(
                    enabledScenes,
                    Does.Contain(location.ScenePath),
                    $"Scene is missing or disabled for {assetPath}");
                Assert.That(
                    scenePaths.Add(location.ScenePath),
                    Is.True,
                    $"Duplicate scene reference in {assetPath}");
                Assert.That(
                    location.SpawnPointId,
                    Is.Not.Empty,
                    $"Missing Spawn Point Id in {assetPath}");

                if (location.LocationType == LocationType.Expedition &&
                    location.DiscoverySource != DiscoverySource.Antenna)
                {
                    Assert.That(
                        location.MapSlot,
                        Is.Not.Null,
                        $"Missing map slot in {assetPath}");
                    Assert.That(
                        mapSlots.Add(location.MapSlot),
                        Is.True,
                        $"Duplicate map slot in {assetPath}");
                }
            }
        }

        [Test]
        public void InteractionModesContainPressAndHold()
        {
            Assert.That(
                NeraInteractionMode.Press,
                Is.Not.EqualTo(NeraInteractionMode.Hold)
            );
        }
    }

    public sealed class QuestSystemTests
    {
        private GameObject root;
        private QuestController quests;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Test_QuestController");
            quests = root.AddComponent<QuestController>();
            quests.Configure(QuestCatalog.LoadDefault());
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void MainExpeditionQuestAdvancesOnlyFromConfiguredSignals()
        {
            Assert.That(
                quests.Report(
                    QuestSignalType.LocationDiscovered,
                    "Expedition_01",
                    "Ancient Outpost"),
                Is.True);

            QuestRuntimeState state =
                quests.FindActive("main.expedition_01");
            Assert.That(state, Is.Not.Null);
            Assert.That(state.CurrentStageIndex, Is.Zero);
            Assert.That(state.ObjectiveTitle,
                Is.EqualTo("Отправляйтесь в Ancient Outpost"));

            quests.Report(
                QuestSignalType.LocationDiscovered,
                "Expedition_01",
                "Ancient Outpost");
            Assert.That(quests.ActiveQuests.Count, Is.EqualTo(1));

            quests.Report(QuestSignalType.LocationEntered, "Expedition_01");
            quests.Report(QuestSignalType.EnemyEncountered, "io_blue_weak");
            quests.Report(QuestSignalType.ItemCollected, "io_blue_shard_01");
            quests.Report(QuestSignalType.LocationEntered, "Player_Station");
            quests.Report(
                QuestSignalType.ResearchAnalyzed,
                "research_io_blue_shard_01");

            Assert.That(quests.FindActive("main.expedition_01"), Is.Null);
            Assert.That(quests.IsCompleted("main.expedition_01"), Is.True);
            Assert.That(
                quests.GetCompletionCount("main.expedition_01"),
                Is.EqualTo(1));

            quests.Report(
                QuestSignalType.LocationDiscovered,
                "Expedition_01",
                "Ancient Outpost");
            Assert.That(
                quests.FindActive("main.expedition_01"),
                Is.Null,
                "A one-time quest must not appear again after completion.");
            Assert.That(
                quests.GetCompletionCount("main.expedition_01"),
                Is.EqualTo(1));
        }

        [Test]
        public void DroneExpeditionQuestAppearsOnceAndCompletesAfterReturn()
        {
            quests.Report(
                QuestSignalType.ObjectInteractionCompleted,
                "station_terminal",
                "Station Terminal");

            const string questId =
                "main.launch_drone_expedition_01";
            Assert.That(quests.FindActive(questId), Is.Not.Null);

            quests.Report(
                QuestSignalType.LocationDiscovered,
                "Expedition_01",
                "Ancient Outpost");
            Assert.That(
                quests.FindActive(questId),
                Is.Not.Null,
                "Location discovery happens before the drone return " +
                "signal and must not close this quest early.");

            quests.Report(
                QuestSignalType.DroneScanCompleted,
                "Expedition_01",
                "Ancient Outpost",
                cause: "new_location");

            Assert.That(quests.FindActive(questId), Is.Null);
            Assert.That(quests.GetCompletionCount(questId), Is.EqualTo(1));

            quests.Report(
                QuestSignalType.DroneScanCompleted,
                "Expedition_01",
                "Ancient Outpost",
                cause: "new_location");
            Assert.That(quests.FindActive(questId), Is.Null);
            Assert.That(quests.GetCompletionCount(questId), Is.EqualTo(1));
        }

        [Test]
        public void DynamicMaintenanceQuestUsesTargetContextWithoutDuplicates()
        {
            quests.ReportDeviceCondition(
                "station_solar_01",
                "Solar Panel 01",
                0.3f);

            const string instanceId =
                "side.clean_solar_panel:station_solar_01";
            QuestRuntimeState state = quests.FindActive(instanceId);
            Assert.That(state, Is.Not.Null);
            Assert.That(state.Title, Is.EqualTo("Очистите Solar Panel 01"));

            quests.ReportDeviceCondition(
                "station_solar_01",
                "Solar Panel 01",
                0.2f);
            Assert.That(
                quests.ActiveQuests.Count(quest =>
                    quest.InstanceId == instanceId),
                Is.EqualTo(1));

            quests.ReportDeviceCondition(
                "station_solar_01",
                "Solar Panel 01",
                1f);
            Assert.That(quests.FindActive(instanceId), Is.Null);
            Assert.That(quests.GetCompletionCount(instanceId), Is.EqualTo(1));

            quests.ReportDeviceCondition(
                "station_solar_01",
                "Solar Panel 01",
                0.3f);
            Assert.That(quests.FindActive(instanceId), Is.Not.Null);
        }

        [Test]
        public void ActiveQuestStageAndProgressRoundTripThroughSaveData()
        {
            quests.Report(
                QuestSignalType.LocationDiscovered,
                "Expedition_01",
                "Ancient Outpost");
            quests.Report(
                QuestSignalType.LocationEntered,
                "Expedition_01",
                "Ancient Outpost");

            List<QuestInstanceSaveData> active =
                quests.CaptureActiveQuests();
            List<QuestHistorySaveData> history = quests.CaptureHistory();
            List<QuestActivationSaveData> pending =
                quests.CapturePendingActivations();

            quests.ResetProgress();
            quests.RestoreProgress(active, history, pending);

            QuestRuntimeState restored =
                quests.FindActive("main.expedition_01");
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.CurrentStageIndex, Is.EqualTo(1));
            Assert.That(restored.ObjectiveTitle,
                Is.EqualTo("Исследуйте Ancient Outpost"));
        }

        [Test]
        public void CompletingQuestActivatesConfiguredFollowUpByQuestId()
        {
            QuestDefinition followUp =
                ScriptableObject.CreateInstance<QuestDefinition>();
            QuestCatalog temporaryCatalog =
                ScriptableObject.CreateInstance<QuestCatalog>();

            try
            {
                SerializedObject definition =
                    new SerializedObject(followUp);
                definition.FindProperty("questId").stringValue =
                    "main.inspect_station";
                definition.FindProperty("category").enumValueIndex =
                    (int)QuestCategory.Main;
                definition.FindProperty("availability").enumValueIndex =
                    (int)QuestAvailability.Once;
                definition.FindProperty("targetScope").enumValueIndex =
                    (int)QuestTargetScope.Single;
                definition.FindProperty("title").stringValue =
                    "Осмотрите станцию";
                definition.FindProperty("description").stringValue =
                    "Познакомьтесь с основными системами станции.";

                SerializedProperty activation =
                    definition.FindProperty("activationConditions");
                activation.arraySize = 1;
                ConfigureQuestCondition(
                    activation.GetArrayElementAtIndex(0),
                    QuestSignalType.QuestCompleted,
                    "main.restore_battery");

                SerializedProperty stages =
                    definition.FindProperty("stages");
                stages.arraySize = 1;
                SerializedProperty stage = stages.GetArrayElementAtIndex(0);
                stage.FindPropertyRelative("title").stringValue =
                    "Осмотрите станцию";
                stage.FindPropertyRelative("description").stringValue =
                    "Посетите основные помещения.";
                SerializedProperty completion =
                    stage.FindPropertyRelative("completionConditions");
                completion.arraySize = 1;
                ConfigureQuestCondition(
                    completion.GetArrayElementAtIndex(0),
                    QuestSignalType.AreaExplored,
                    "station_introduction");
                definition.ApplyModifiedPropertiesWithoutUndo();

                QuestCatalog defaults = QuestCatalog.LoadDefault();
                SerializedObject catalog =
                    new SerializedObject(temporaryCatalog);
                SerializedProperty definitions =
                    catalog.FindProperty("definitions");
                definitions.arraySize = defaults.Definitions.Count + 1;
                for (int index = 0;
                     index < defaults.Definitions.Count;
                     index++)
                {
                    definitions.GetArrayElementAtIndex(index)
                        .objectReferenceValue = defaults.Definitions[index];
                }

                definitions.GetArrayElementAtIndex(definitions.arraySize - 1)
                    .objectReferenceValue = followUp;
                catalog.ApplyModifiedPropertiesWithoutUndo();
                quests.Configure(temporaryCatalog);

                Assert.That(
                    quests.FindActive("main.inspect_station"),
                    Is.Null);
                Assert.That(
                    quests.Report(
                        QuestSignalType.LocationEntered,
                        "Player_Station"),
                    Is.True);
                Assert.That(
                    quests.FindActive("main.restore_battery"),
                    Is.Not.Null);
                Assert.That(
                    quests.FindActive("main.inspect_station"),
                    Is.Null);

                Assert.That(
                    quests.Report(
                        QuestSignalType.StationSystemActivated,
                        "station_battery",
                        "BATTERY"),
                    Is.True);

                Assert.That(
                    quests.IsCompleted("main.restore_battery"),
                    Is.True);
                Assert.That(
                    quests.FindActive("main.inspect_station"),
                    Is.Not.Null,
                    "The configured follow-up must activate immediately.");
            }
            finally
            {
                Object.DestroyImmediate(temporaryCatalog);
                Object.DestroyImmediate(followUp);
            }
        }

        [Test]
        public void CompletedStageKeepsItsCheckpointFlagAfterAdvancing()
        {
            QuestDefinition definition = CreateQuestDefinition(
                "main.stage_checkpoint_test",
                activationConditionCount: 0,
                completionConditionCount: 1);
            QuestCatalog catalog = null;
            try
            {
                SerializedObject serialized =
                    new SerializedObject(definition);
                SerializedProperty stages =
                    serialized.FindProperty("stages");
                stages.arraySize = 2;

                SerializedProperty first = stages.GetArrayElementAtIndex(0);
                first.FindPropertyRelative("createCheckpointOnCompletion")
                    .boolValue = true;
                ConfigureQuestCondition(
                    first.FindPropertyRelative("completionConditions")
                        .GetArrayElementAtIndex(0),
                    QuestSignalType.LocationEntered,
                    "stage_checkpoint_a");

                SerializedProperty second = stages.GetArrayElementAtIndex(1);
                second.FindPropertyRelative("title").stringValue =
                    "Second stage";
                second.FindPropertyRelative("createCheckpointOnCompletion")
                    .boolValue = false;
                ConfigureQuestCondition(
                    second.FindPropertyRelative("completionConditions")
                        .GetArrayElementAtIndex(0),
                    QuestSignalType.LocationEntered,
                    "stage_checkpoint_b");
                serialized.ApplyModifiedPropertiesWithoutUndo();

                bool completedStageCreatesCheckpoint = false;
                quests.QuestStageChanged += state =>
                {
                    int completedIndex = state.CurrentStageIndex - 1;
                    completedStageCreatesCheckpoint =
                        state.Definition.Stages[completedIndex]
                            .CreateCheckpointOnCompletion;
                };
                catalog = CreateQuestCatalog(definition);
                quests.Configure(catalog);

                quests.Report(
                    QuestSignalType.LocationEntered,
                    "stage_checkpoint_a");

                QuestRuntimeState active = quests.FindActive(
                    "main.stage_checkpoint_test");
                Assert.That(active, Is.Not.Null);
                Assert.That(active.CurrentStageIndex, Is.EqualTo(1));
                Assert.That(completedStageCreatesCheckpoint, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void CurrentStateConditionUsesFactReportedBeforeQuestActivation()
        {
            QuestDefinition definition = CreateQuestDefinition(
                "main.energy_ready",
                1,
                1);
            QuestCatalog catalog = CreateQuestCatalog(definition);

            try
            {
                SerializedObject serialized =
                    new SerializedObject(definition);
                ConfigureQuestCondition(
                    serialized.FindProperty("activationConditions")
                        .GetArrayElementAtIndex(0),
                    QuestSignalType.LocationEntered,
                    "Player_Station");
                ConfigureQuestCondition(
                    serialized.FindProperty("stages")
                        .GetArrayElementAtIndex(0)
                        .FindPropertyRelative("completionConditions")
                        .GetArrayElementAtIndex(0),
                    QuestSignalType.EnergyChargeChanged,
                    "station_energy",
                    QuestConditionEvaluation.CurrentState,
                    0.5f,
                    QuestValueComparison.GreaterOrEqual);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                quests.Configure(catalog);

                quests.Report(
                    QuestSignalType.EnergyChargeChanged,
                    "station_energy",
                    value: 0.75f);
                Assert.That(quests.IsCompleted("main.energy_ready"), Is.False);

                quests.Report(
                    QuestSignalType.LocationEntered,
                    "Player_Station");

                Assert.That(quests.IsCompleted("main.energy_ready"), Is.True);
                Assert.That(quests.FindActive("main.energy_ready"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void AnyConditionLogicActivatesQuestFromEitherConfiguredEvent()
        {
            QuestDefinition definition = CreateQuestDefinition(
                "side.any_discovery",
                2,
                1,
                QuestConditionLogic.Any);
            QuestCatalog catalog = CreateQuestCatalog(definition);

            try
            {
                SerializedObject serialized =
                    new SerializedObject(definition);
                SerializedProperty activation =
                    serialized.FindProperty("activationConditions");
                ConfigureQuestCondition(
                    activation.GetArrayElementAtIndex(0),
                    QuestSignalType.LocationDiscovered,
                    "expedition_01");
                ConfigureQuestCondition(
                    activation.GetArrayElementAtIndex(1),
                    QuestSignalType.LocationDiscovered,
                    "expedition_02");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                quests.Configure(catalog);

                quests.Report(
                    QuestSignalType.LocationDiscovered,
                    "expedition_02");

                Assert.That(
                    quests.FindActive("side.any_discovery"),
                    Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void OppositeStationStateClearsRememberedCurrentState()
        {
            QuestDefinition definition = CreateQuestDefinition(
                "main.power_terminal",
                1,
                1);
            QuestCatalog catalog = CreateQuestCatalog(definition);

            try
            {
                SerializedObject serialized =
                    new SerializedObject(definition);
                ConfigureQuestCondition(
                    serialized.FindProperty("activationConditions")
                        .GetArrayElementAtIndex(0),
                    QuestSignalType.LocationDiscovered,
                    "station_intro");
                ConfigureQuestCondition(
                    serialized.FindProperty("stages")
                        .GetArrayElementAtIndex(0)
                        .FindPropertyRelative("completionConditions")
                        .GetArrayElementAtIndex(0),
                    QuestSignalType.StationSystemActivated,
                    "station_terminal",
                    QuestConditionEvaluation.CurrentState);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                quests.Configure(catalog);

                quests.Report(
                    QuestSignalType.StationSystemActivated,
                    "station_terminal");
                quests.Report(
                    QuestSignalType.StationSystemDeactivated,
                    "station_terminal");
                quests.Report(
                    QuestSignalType.LocationDiscovered,
                    "station_intro");

                Assert.That(
                    quests.FindActive("main.power_terminal"),
                    Is.Not.Null);
                Assert.That(
                    quests.IsCompleted("main.power_terminal"),
                    Is.False);

                quests.Report(
                    QuestSignalType.StationSystemActivated,
                    "station_terminal");
                Assert.That(
                    quests.IsCompleted("main.power_terminal"),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void StateSynchronizationDoesNotImitateNewGameplayEvent()
        {
            QuestDefinition definition = CreateQuestDefinition(
                "main.wait_for_power_event",
                1,
                1);
            QuestCatalog catalog = CreateQuestCatalog(definition);

            try
            {
                SerializedObject serialized =
                    new SerializedObject(definition);
                ConfigureQuestCondition(
                    serialized.FindProperty("activationConditions")
                        .GetArrayElementAtIndex(0),
                    QuestSignalType.StationPowerOnline,
                    "station_power");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                quests.Configure(catalog);

                quests.SynchronizeState(
                    QuestSignalType.StationPowerOnline,
                    "station_power");
                Assert.That(
                    quests.FindActive("main.wait_for_power_event"),
                    Is.Null);

                quests.Report(
                    QuestSignalType.StationPowerOnline,
                    "station_power");
                Assert.That(
                    quests.FindActive("main.wait_for_power_event"),
                    Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void CompletedQuestCurrentStateIsRebuiltFromSaveHistory()
        {
            QuestDefinition definition = CreateQuestDefinition(
                "main.after_saved_quest",
                1,
                1);
            QuestDefinition prerequisite = CreateQuestDefinition(
                "main.saved_prerequisite",
                1,
                1);
            QuestCatalog catalog = CreateQuestCatalog(
                definition,
                prerequisite);

            try
            {
                SerializedObject serialized =
                    new SerializedObject(definition);
                ConfigureQuestCondition(
                    serialized.FindProperty("activationConditions")
                        .GetArrayElementAtIndex(0),
                    QuestSignalType.QuestCompleted,
                    "main.saved_prerequisite",
                    QuestConditionEvaluation.CurrentState);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                quests.Configure(catalog);

                quests.RestoreProgress(
                    Array.Empty<QuestInstanceSaveData>(),
                    new[]
                    {
                        new QuestHistorySaveData
                        {
                            instanceId = "main.saved_prerequisite",
                            questId = "main.saved_prerequisite",
                            completionCount = 1
                        }
                    },
                    Array.Empty<QuestActivationSaveData>());

                Assert.That(
                    quests.FindActive("main.after_saved_quest"),
                    Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(prerequisite);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void LegacyQuestSignalEnumValuesRemainStable()
        {
            Assert.That((int)QuestSignalType.LocationDiscovered, Is.EqualTo(0));
            Assert.That((int)QuestSignalType.StationSystemActivated, Is.EqualTo(10));
            Assert.That((int)QuestSignalType.QuestCompleted, Is.EqualTo(11));
            Assert.That((int)QuestSignalType.LocationExited, Is.EqualTo(12));
        }

        [Test]
        public void SaveVersion14SerializesQuestAndMaintenanceState()
        {
            SaveGameData data = new SaveGameData();
            data.activeQuests.Add(new QuestInstanceSaveData
            {
                instanceId = "main.expedition_01",
                questId = "main.expedition_01",
                currentStageIndex = 2,
                conditionProgress = new List<int> { 1 }
            });
            data.maintenanceObjects.Add(new MaintenanceSaveData
            {
                objectId = "station_solar_01",
                condition = 0.25f
            });

            SaveGameData restored = JsonUtility.FromJson<SaveGameData>(
                JsonUtility.ToJson(data));

            Assert.That(restored.version, Is.EqualTo(16));
            Assert.That(restored.activeQuests[0].currentStageIndex,
                Is.EqualTo(2));
            Assert.That(restored.maintenanceObjects[0].objectId,
                Is.EqualTo("station_solar_01"));
            Assert.That(restored.maintenanceObjects[0].condition,
                Is.EqualTo(0.25f));
        }

        private static void ConfigureQuestCondition(
            SerializedProperty condition,
            QuestSignalType signalType,
            string targetId,
            QuestConditionEvaluation evaluation =
                QuestConditionEvaluation.Event,
            float threshold = 0.5f,
            QuestValueComparison comparison =
                QuestValueComparison.GreaterOrEqual)
        {
            condition.FindPropertyRelative("signalType").enumValueIndex =
                (int)signalType;
            condition.FindPropertyRelative("evaluation").enumValueIndex =
                (int)evaluation;
            condition.FindPropertyRelative("target").enumValueIndex =
                (int)QuestConditionTarget.SpecificObject;
            condition.FindPropertyRelative("targetId").stringValue = targetId;
            condition.FindPropertyRelative("cause").stringValue = string.Empty;
            condition.FindPropertyRelative("requiredCount").intValue = 1;
            condition.FindPropertyRelative("comparison").enumValueIndex =
                (int)comparison;
            condition.FindPropertyRelative("threshold").floatValue =
                threshold;
        }

        private static QuestDefinition CreateQuestDefinition(
            string questId,
            int activationConditionCount,
            int completionConditionCount,
            QuestConditionLogic activationConditionLogic =
                QuestConditionLogic.All,
            QuestConditionLogic completionConditionLogic =
                QuestConditionLogic.All)
        {
            QuestDefinition definition =
                ScriptableObject.CreateInstance<QuestDefinition>();
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("questId").stringValue = questId;
            serialized.FindProperty("category").enumValueIndex =
                (int)QuestCategory.Main;
            serialized.FindProperty("availability").enumValueIndex =
                (int)QuestAvailability.Once;
            serialized.FindProperty("targetScope").enumValueIndex =
                (int)QuestTargetScope.Single;
            serialized.FindProperty("title").stringValue = questId;
            serialized.FindProperty("description").stringValue = questId;
            serialized.FindProperty("activationLogic").enumValueIndex =
                (int)activationConditionLogic;
            serialized.FindProperty("activationConditions").arraySize =
                activationConditionCount;

            SerializedProperty stages = serialized.FindProperty("stages");
            stages.arraySize = 1;
            SerializedProperty stage = stages.GetArrayElementAtIndex(0);
            stage.FindPropertyRelative("title").stringValue = "Test objective";
            stage.FindPropertyRelative("description").stringValue =
                "Test objective";
            stage.FindPropertyRelative("completionLogic").enumValueIndex =
                (int)completionConditionLogic;
            stage.FindPropertyRelative("completionConditions").arraySize =
                completionConditionCount;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private static QuestCatalog CreateQuestCatalog(
            params QuestDefinition[] definitions)
        {
            QuestCatalog catalog =
                ScriptableObject.CreateInstance<QuestCatalog>();
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty items = serialized.FindProperty("definitions");
            items.arraySize = definitions.Length;
            for (int index = 0; index < definitions.Length; index++)
            {
                items.GetArrayElementAtIndex(index).objectReferenceValue =
                    definitions[index];
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }
    }

    public sealed class GameSessionLaunchStateTests
    {
        [TearDown]
        public void TearDown()
        {
            GameSessionLaunchState.Clear();
        }

        [Test]
        public void RequestedLaunchModeIsConsumedOnlyOnce()
        {
            GameSessionLaunchState.Request(GameLaunchMode.NewGame);

            GameSessionLaunchRequest request =
                GameSessionLaunchState.ConsumeOrDefault();
            Assert.That(request.Mode, Is.EqualTo(GameLaunchMode.NewGame));
            Assert.That(
                request.SaveSlot,
                Is.EqualTo(SaveSlotStorage.DefaultSlot));

            GameSessionLaunchRequest fallback =
                GameSessionLaunchState.ConsumeOrDefault();
            Assert.That(fallback.Mode, Is.EqualTo(GameLaunchMode.Continue));
            Assert.That(
                fallback.SaveSlot,
                Is.EqualTo(SaveSlotStorage.DefaultSlot));
        }

        [Test]
        public void RequestedSaveSlotIsConsumedWithLaunchMode()
        {
            GameSessionLaunchState.Request(GameLaunchMode.NewGame, 2);

            GameSessionLaunchRequest request =
                GameSessionLaunchState.ConsumeOrDefault();

            Assert.That(request.Mode, Is.EqualTo(GameLaunchMode.NewGame));
            Assert.That(request.SaveSlot, Is.EqualTo(2));
        }
    }

    public sealed class Sprint02StationPowerTests
    {
        private GameObject root;
        private StationPowerController power;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Test_StationPower");
            power = root.AddComponent<StationPowerController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void RestorePowerTransitionsOfflineToOnlineAndRaisesEvent()
        {
            StationPowerState? observedState = null;
            power.StateChanged += state => observedState = state;

            bool restored = power.RestorePower();

            Assert.That(restored, Is.True);
            Assert.That(power.State, Is.EqualTo(StationPowerState.Online));
            Assert.That(power.IsPowered, Is.True);
            Assert.That(observedState, Is.EqualTo(StationPowerState.Online));
        }

        [Test]
        public void RestorePowerIsIdempotent()
        {
            Assert.That(power.RestorePower(), Is.True);
            Assert.That(power.RestorePower(), Is.False);
            Assert.That(power.State, Is.EqualTo(StationPowerState.Online));
        }
    }

    public sealed class StationEnergySystemTests
    {
        private GameObject root;
        private StationEnvironmentController environment;
        private EnergySystemController energy;

        [SetUp]
        public void SetUp()
        {
            ClearSingleton(typeof(StationEnvironmentController));
            ClearSingleton(typeof(EnergySystemController));
            root = new GameObject("Test_EnergySystem");
            environment = root.AddComponent<StationEnvironmentController>();
            energy = root.AddComponent<EnergySystemController>();
            SetSingleton(typeof(StationEnvironmentController), environment);
            SetSingleton(typeof(EnergySystemController), energy);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            ClearSingleton(typeof(StationEnvironmentController));
            ClearSingleton(typeof(EnergySystemController));
        }

        [Test]
        public void MultipleBatteriesShareCapacityAndInitialCharge()
        {
            energy.RegisterBattery("battery_01", 1000f, 1000f);
            energy.RegisterBattery("battery_02", 1000f, 1000f);

            Assert.That(energy.TotalCapacity, Is.EqualTo(2000f));
            Assert.That(energy.CurrentEnergy, Is.EqualTo(2000f));
        }

        [Test]
        public void ReRegisteringBatteryUpdatesCapacityAndPreservesCharge()
        {
            energy.RegisterBattery("battery_01", 1000f, 1000f);
            energy.RestoreState(600f, true);

            Assert.That(
                energy.RegisterBattery("battery_01", 2000f, 2000f),
                Is.True);

            Assert.That(energy.TotalCapacity, Is.EqualTo(2000f));
            Assert.That(energy.CurrentEnergy, Is.EqualTo(600f));

            energy.ResetForNewGame();
            Assert.That(
                energy.CurrentEnergy,
                Is.EqualTo(2000f),
                "The updated stage parameters must become the new-game baseline.");
        }

        [Test]
        public void ReloadingStationDoesNotDuplicateBatteryOrSolarPanel()
        {
            energy.RegisterBattery("station/battery_01", 1000f, 1000f);
            energy.RegisterSolarPanel("station/panel_01", 1f);

            energy.RegisterBattery("station/battery_01", 1000f, 1000f);
            energy.RegisterSolarPanel("station/panel_01", 1f);

            environment.SetTime(12f);
            environment.SetWeather(StationWeather.Clear);
            energy.AdvanceSimulation(1f);

            Assert.That(energy.TotalCapacity, Is.EqualTo(1000f));
            Assert.That(
                energy.CurrentGeneration,
                Is.EqualTo(energy.Config.ClearDayGeneration)
            );
        }

        [Test]
        public void SolarPanelGeneratesByDayButNotAtNight()
        {
            energy.RegisterBattery("battery_01", 1000f, 0f);
            energy.RegisterSolarPanel("panel_01", 1f);
            environment.SetWeather(StationWeather.Clear);

            environment.SetTime(12f);
            energy.AdvanceSimulation(1f);
            Assert.That(
                energy.CurrentGeneration,
                Is.EqualTo(energy.Config.ClearDayGeneration)
            );

            environment.SetTime(0f);
            energy.AdvanceSimulation(1f);
            Assert.That(energy.CurrentGeneration, Is.Zero);
        }

        [Test]
        public void EmergencyReserveDisconnectsNonEssentialConsumers()
        {
            energy.RegisterBattery("battery_01", 1000f, 1000f);
            energy.RegisterConsumer("laboratory", 4f, true);
            energy.SetConsumerActive("laboratory", true);
            energy.RestoreState(200f, true);
            energy.AdvanceSimulation(0.1f);

            Assert.That(energy.State, Is.EqualTo(EnergyState.Emergency));
            Assert.That(energy.IsConsumerPowered("laboratory"), Is.False);
            Assert.That(energy.CurrentConsumption, Is.Zero);
        }

        [Test]
        public void ConsumerChargeThresholdsAreIndependent()
        {
            energy.RegisterBattery("battery_01", 1000f, 1000f);
            energy.RegisterConsumer("laboratory", 4f, 0.5f);
            energy.RegisterConsumer("antenna", 2f, 0.2f);
            energy.SetConsumerActive("laboratory", true);
            energy.SetConsumerActive("antenna", true);
            energy.RestoreState(300f, true);

            energy.AdvanceSimulation(0.1f);

            Assert.That(energy.IsConsumerPowered("laboratory"), Is.False);
            Assert.That(energy.IsConsumerPowered("antenna"), Is.True);
            Assert.That(energy.CurrentConsumption, Is.EqualTo(2f));
        }

        [Test]
        public void LaboratoryCannotReceivePowerBeforeGridStarts()
        {
            energy.RegisterBattery("battery_01", 1000f, 1000f);
            energy.RegisterConsumer("laboratory", 4f, true);

            Assert.That(energy.CanPowerConsumer("laboratory"), Is.False);

            energy.SetGridEnabled(true);

            Assert.That(energy.CanPowerConsumer("laboratory"), Is.True);
        }

        private static void ClearSingleton(System.Type controllerType)
        {
            SetSingleton(controllerType, null);
        }

        private static void SetSingleton(
            System.Type controllerType,
            object value
        )
        {
            PropertyInfo instanceProperty = controllerType.GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public
            );
            MethodInfo setter = instanceProperty?.GetSetMethod(true);
            setter?.Invoke(null, new[] { value });
        }
    }

    public sealed class Sprint03DroneStateTests
    {
        private GameObject root;
        private StationPowerController power;
        private ExpeditionDiscoveryController discovery;
        private DroneScanController drone;
        private ExpeditionLocationData location;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Test_DroneState");
            power = root.AddComponent<StationPowerController>();
            discovery = root.AddComponent<ExpeditionDiscoveryController>();
            drone = root.AddComponent<DroneScanController>();
            location = ScriptableObject.CreateInstance<ExpeditionLocationData>();
            SetPrivateField(drone, "stationPower", power);
            SetPrivateField(drone, "discovery", discovery);

            SerializedObject locationObject = new SerializedObject(location);
            locationObject.FindProperty("locationId").stringValue = "Test_Expedition";
            locationObject.FindProperty("droneScanDuration").floatValue = 2f;
            locationObject.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(location);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void DroneUnlocksWhenStationPowerComesOnline()
        {
            Assert.That(drone.State, Is.EqualTo(DroneState.Locked));

            power.RestorePower();
            drone.RefreshAvailability();

            Assert.That(drone.State, Is.EqualTo(DroneState.Ready));
        }

        [Test]
        public void DroneCannotLaunchWithoutConfiguredLocation()
        {
            power.RestorePower();
            drone.RefreshAvailability();

            Assert.That(drone.LaunchScan(), Is.False);
            Assert.That(drone.State, Is.EqualTo(DroneState.Ready));
        }

        [Test]
        public void DroneScanDiscoversConfiguredLocation()
        {
            power.RestorePower();
            drone.RefreshAvailability();

            Assert.That(drone.LaunchScan(location), Is.True);
            Assert.That(drone.State, Is.EqualTo(DroneState.Scanning));

            drone.AdvanceScan(1f);
            Assert.That(drone.ScanProgress, Is.EqualTo(0.5f).Within(0.001f));

            drone.AdvanceScan(1f);
            Assert.That(drone.State, Is.EqualTo(DroneState.ScanComplete));
            Assert.That(discovery.IsDiscovered(location), Is.True);
        }

        [Test]
        public void DiscoveryCanFilterLocationsBySourceAndType()
        {
            ExpeditionLocationData expedition =
                CreateLocation("expedition_02", LocationType.Expedition, DiscoverySource.Drone);
            ExpeditionLocationData signal =
                CreateLocation("unknown_signal_01", LocationType.UnknownSignal, DiscoverySource.Antenna);
            AddKnownLocation(expedition);
            AddKnownLocation(signal);

            Assert.That(
                discovery.GetKnownLocations(DiscoverySource.Drone),
                Is.EquivalentTo(new[] { expedition })
            );
            Assert.That(
                discovery.GetKnownLocations(DiscoverySource.Antenna),
                Is.EquivalentTo(new[] { signal })
            );
            Assert.That(
                discovery.GetKnownLocations(LocationType.UnknownSignal),
                Is.EquivalentTo(new[] { signal })
            );

            Object.DestroyImmediate(expedition);
            Object.DestroyImmediate(signal);
        }

        [Test]
        public void NextUndiscoveredLocationSkipsAlreadyDiscoveredTargets()
        {
            ExpeditionLocationData first =
                CreateLocation("expedition_02", LocationType.Expedition, DiscoverySource.Drone);
            ExpeditionLocationData second =
                CreateLocation("expedition_03", LocationType.Expedition, DiscoverySource.Drone);
            AddKnownLocation(first);
            AddKnownLocation(second);

            Assert.That(
                discovery.TryGetNextUndiscovered(
                    DiscoverySource.Drone,
                    out ExpeditionLocationData next
                ),
                Is.True
            );
            Assert.That(next, Is.EqualTo(first));

            discovery.Discover(first);

            Assert.That(
                discovery.TryGetNextUndiscovered(
                    DiscoverySource.Drone,
                    out next
                ),
                Is.True
            );
            Assert.That(next, Is.EqualTo(second));

            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        private static void SetPrivateField(
            DroneScanController target,
            string fieldName,
            object value
        )
        {
            FieldInfo field = typeof(DroneScanController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }

        private void AddKnownLocation(ExpeditionLocationData knownLocation)
        {
            SerializedObject serializedDiscovery = new SerializedObject(discovery);
            SerializedProperty locations =
                serializedDiscovery.FindProperty("knownLocations");
            int index = locations.arraySize;
            locations.InsertArrayElementAtIndex(index);
            locations.GetArrayElementAtIndex(index).objectReferenceValue =
                knownLocation;
            serializedDiscovery.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ExpeditionLocationData CreateLocation(
            string locationId,
            LocationType locationType,
            DiscoverySource discoverySource
        )
        {
            ExpeditionLocationData data =
                ScriptableObject.CreateInstance<ExpeditionLocationData>();
            SerializedObject serialized = new SerializedObject(data);
            serialized.FindProperty("locationId").stringValue = locationId;
            serialized.FindProperty("locationType").enumValueIndex =
                (int)locationType;
            serialized.FindProperty("discoverySource").enumValueIndex =
                (int)discoverySource;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }
    }

    public sealed class Sprint04IOEnemyTests
    {
        [Test]
        public void PlayerHealthReceivesEnergyDamage()
        {
            GameObject player = new GameObject("Test_Player");
            PlayerHealth health = player.AddComponent<PlayerHealth>();
            health.RestoreFullHealth();

            health.TakeDamage(25f, null);

            Assert.That(health.IsAlive, Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(75f));
            Object.DestroyImmediate(player);
        }

        [Test]
        public void BlueIOImplementsDamageableContract()
        {
            GameObject enemy = new GameObject("Test_BlueIO");
            IOEnemyController controller =
                enemy.AddComponent<IOEnemyController>();

            Assert.That(controller, Is.InstanceOf<IDamageable>());
            Assert.That(controller.IsAlive, Is.True);
            Object.DestroyImmediate(enemy);
        }

        [Test]
        public void EnabledIOIsPresentOnlyWhileActive()
        {
            GameObject enemy = new GameObject("Test_RegisteredIO");
            IOEnemyController controller =
                enemy.AddComponent<IOEnemyController>();
            InvokeLifecycle(controller, "OnEnable");

            Assert.That(
                IOEnemyController.ActiveEnemies.Contains(controller),
                Is.True);

            InvokeLifecycle(controller, "OnDisable");
            Assert.That(
                IOEnemyController.ActiveEnemies.Contains(controller),
                Is.False);

            InvokeLifecycle(controller, "OnEnable");
            Assert.That(
                IOEnemyController.ActiveEnemies.Contains(controller),
                Is.True);

            InvokeLifecycle(controller, "OnDestroy");
            Object.DestroyImmediate(enemy);
            Assert.That(
                IOEnemyController.ActiveEnemies.Contains(controller),
                Is.False);
        }

        private static void InvokeLifecycle(
            IOEnemyController controller,
            string methodName)
        {
            MethodInfo method = typeof(IOEnemyController).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, null);
        }
    }

    public sealed class PCQualityPresetTests
    {
        [Test]
        public void WindowsPlayerDefaultsUseHighPresetAndOneHundredFpsCap()
        {
            int originalQuality = QualitySettings.GetQualityLevel();
            int originalVSyncCount = QualitySettings.vSyncCount;
            int originalTargetFrameRate = Application.targetFrameRate;
            try
            {
                Assert.That(
                    PCQualityRuntimeController.ApplyWindowsPlayerDefaults(),
                    Is.True);
                Assert.That(
                    QualitySettings.names[QualitySettings.GetQualityLevel()],
                    Is.EqualTo("High"));
                Assert.That(QualitySettings.vSyncCount, Is.Zero);
                Assert.That(Application.targetFrameRate, Is.EqualTo(100));
            }
            finally
            {
                QualitySettings.SetQualityLevel(originalQuality, false);
                QualitySettings.vSyncCount = originalVSyncCount;
                Application.targetFrameRate = originalTargetFrameRate;
            }
        }

        [Test]
        public void StandaloneQualityPresetsUseDedicatedPipelineAssets()
        {
            int originalQuality = QualitySettings.GetQualityLevel();
            try
            {
                AssertPreset("Low", "PC_Low_RPAsset", 0);
                AssertPreset("Medium", "PC_Medium_RPAsset", 2);
                AssertPreset("High", "PC_High_RPAsset", 4);
            }
            finally
            {
                QualitySettings.SetQualityLevel(originalQuality, false);
            }
        }

        private static void AssertPreset(
            string presetName,
            string pipelineName,
            int expectedMsaa)
        {
            int index = System.Array.IndexOf(
                QualitySettings.names,
                presetName);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));

            QualitySettings.SetQualityLevel(index, false);
            Assert.That(
                QualitySettings.renderPipeline,
                Is.Not.Null);
            Assert.That(
                QualitySettings.renderPipeline.name,
                Is.EqualTo(pipelineName));
            Assert.That(
                QualitySettings.antiAliasing,
                Is.EqualTo(expectedMsaa));
            Assert.That(
                QualitySettings.maximumLODLevel,
                Is.EqualTo(0),
                "LOD0 and LOD1 must remain available in every PC preset.");
        }
    }

    public sealed class AntennaControllerTests
    {
        private GameObject root;
        private StationEnvironmentController environment;
        private EnergySystemController energy;
        private StationPowerController power;
        private ExpeditionDiscoveryController discovery;
        private MaintainableObject maintenance;
        private AntennaController antenna;
        private ExpeditionLocationData expedition;
        private ExpeditionLocationData signal;
        private MapSlotData mapSlot;

        [SetUp]
        public void SetUp()
        {
            SetSingleton(typeof(StationEnvironmentController), null);
            SetSingleton(typeof(EnergySystemController), null);
            SetSingleton(typeof(StationPowerController), null);
            SetSingleton(typeof(ExpeditionDiscoveryController), null);
            SetSingleton(typeof(AntennaController), null);

            root = new GameObject("Test_AntennaSystems");
            environment = root.AddComponent<StationEnvironmentController>();
            energy = root.AddComponent<EnergySystemController>();
            power = root.AddComponent<StationPowerController>();
            discovery = root.AddComponent<ExpeditionDiscoveryController>();
            StationObjectIdentity identity =
                root.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Antenna,
                "station_antenna");
            maintenance = root.AddComponent<MaintainableObject>();
            SerializedObject serializedMaintenance = new SerializedObject(maintenance);
            serializedMaintenance.FindProperty("role").enumValueIndex =
                (int)MaintenanceRole.Antenna;
            serializedMaintenance.ApplyModifiedPropertiesWithoutUndo();
            antenna = root.AddComponent<AntennaController>();
            SerializedObject serializedAntenna = new SerializedObject(antenna);
            serializedAntenna.FindProperty("signalDiscoveryChance").floatValue = 1f;
            serializedAntenna.ApplyModifiedPropertiesWithoutUndo();

            SetSingleton(typeof(StationEnvironmentController), environment);
            SetSingleton(typeof(EnergySystemController), energy);
            SetSingleton(typeof(StationPowerController), power);
            SetSingleton(typeof(ExpeditionDiscoveryController), discovery);
            SetSingleton(typeof(AntennaController), antenna);

            energy.RegisterBattery("battery_01", 1000f, 1000f);
            energy.SetGridEnabled(true);
            power.RestorePower();

            mapSlot = ScriptableObject.CreateInstance<MapSlotData>();
            SerializedObject serializedMapSlot = new SerializedObject(mapSlot);
            serializedMapSlot.FindProperty("slotId").stringValue =
                "test_map_slot";
            serializedMapSlot.FindProperty("legacySectorIndex").intValue = 0;
            serializedMapSlot.ApplyModifiedPropertiesWithoutUndo();

            expedition = CreateLocation(
                "expedition_01",
                LocationType.Expedition,
                DiscoverySource.Drone,
                mapSlot
            );
            signal = CreateLocation(
                "unknown_signal_01",
                LocationType.UnknownSignal,
                DiscoverySource.Antenna
            );
            AddKnownLocation(expedition);
            AddKnownLocation(signal);
            discovery.Discover(expedition);
            antenna.RefreshAvailability();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(signal);
            Object.DestroyImmediate(expedition);
            Object.DestroyImmediate(mapSlot);
            Object.DestroyImmediate(root);
            SetSingleton(typeof(StationEnvironmentController), null);
            SetSingleton(typeof(EnergySystemController), null);
            SetSingleton(typeof(StationPowerController), null);
            SetSingleton(typeof(ExpeditionDiscoveryController), null);
            SetSingleton(typeof(AntennaController), null);
        }

        [Test]
        public void AntennaCalibrationDiscoversUnknownSignal()
        {
            Assert.That(power.IsPowered, Is.True, "Station power must be online for calibration.");
            Assert.That(discovery.IsDiscovered(expedition), Is.True, "Expedition sector must be open.");
            Assert.That(signal.DiscoverySource, Is.EqualTo(DiscoverySource.Antenna));
            Assert.That(antenna.IsOperational, Is.True, "Antenna must be operational.");
            Assert.That(
                energy.CanPowerConsumer("antenna_calibration"),
                Is.True,
                "Antenna energy consumer must be registered and powered."
            );
            Assert.That(antenna.CanCalibrate(signal), Is.True);

            Assert.That(antenna.StartCalibration(signal), Is.True);
            Assert.That(antenna.State, Is.EqualTo(AntennaState.Calibrating));

            antenna.AdvanceCalibration(antenna.CalibrationDuration);

            Assert.That(antenna.State, Is.EqualTo(AntennaState.SignalFound));
            Assert.That(antenna.ActiveSignal, Is.EqualTo(signal));
            Assert.That(antenna.ActiveSignalMapSlot, Is.EqualTo(mapSlot));
            Assert.That(discovery.IsDiscovered(signal), Is.False);
        }

        [Test]
        public void AntennaCannotCalibrateDroneLocations()
        {
            ExpeditionLocationData expedition = CreateLocation(
                "expedition_03",
                LocationType.Expedition,
                DiscoverySource.Drone
            );

            Assert.That(antenna.StartCalibration(expedition), Is.False);

            Object.DestroyImmediate(expedition);
        }

        [Test]
        public void MaintenanceConditionCanFaultAntennaAndRepairRestoresIt()
        {
            Assert.That(
                maintenance.ObjectId,
                Is.EqualTo("station_antenna"));
            maintenance.SetCondition(0f);
            Assert.That(maintenance.Condition, Is.Zero);
            Assert.That(antenna.Condition, Is.Zero);
            antenna.RefreshAvailability();

            Assert.That(antenna.State, Is.EqualTo(AntennaState.Faulted));
            Assert.That(antenna.CanCalibrate(signal), Is.False);

            Assert.That(antenna.Repair(), Is.True);
            Assert.That(antenna.Condition, Is.EqualTo(1f));
            Assert.That(antenna.CanCalibrate(signal), Is.True);
        }

        private void AddKnownLocation(ExpeditionLocationData knownLocation)
        {
            SerializedObject serializedDiscovery = new SerializedObject(discovery);
            SerializedProperty locations =
                serializedDiscovery.FindProperty("knownLocations");
            int index = locations.arraySize;
            locations.InsertArrayElementAtIndex(index);
            locations.GetArrayElementAtIndex(index).objectReferenceValue =
                knownLocation;
            serializedDiscovery.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ExpeditionLocationData CreateLocation(
            string locationId,
            LocationType locationType,
            DiscoverySource discoverySource,
            MapSlotData mapSlot = null
        )
        {
            ExpeditionLocationData data =
                ScriptableObject.CreateInstance<ExpeditionLocationData>();
            SerializedObject serialized = new SerializedObject(data);
            serialized.FindProperty("locationId").stringValue = locationId;
            serialized.FindProperty("locationType").enumValueIndex =
                (int)locationType;
            serialized.FindProperty("discoverySource").enumValueIndex =
                (int)discoverySource;
            serialized.FindProperty("mapSlot").objectReferenceValue = mapSlot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        private static void SetSingleton(System.Type controllerType, object value)
        {
            PropertyInfo instanceProperty = controllerType.GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public
            );
            instanceProperty?.GetSetMethod(true)?.Invoke(null, new[] { value });
        }
    }

    public sealed class Sprint05InventoryTests
    {
        private GameObject root;
        private PlayerInventory inventory;
        private readonly System.Collections.Generic.List<ItemData> createdItems = new System.Collections.Generic.List<ItemData>();
        private readonly System.Collections.Generic.List<ItemEnergyDefinition> createdEnergyDefinitions = new System.Collections.Generic.List<ItemEnergyDefinition>();

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Test_PlayerInventory");
            inventory = root.AddComponent<PlayerInventory>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (ItemData item in createdItems)
                Object.DestroyImmediate(item);
            foreach (ItemEnergyDefinition definition in createdEnergyDefinitions)
                Object.DestroyImmediate(definition);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ItemsRouteToTheirDedicatedSlotGroups()
        {
            ItemData engineering = CreateItem("engineering", ItemType.EngineeringPart);
            ItemData record = CreateItem("record", ItemType.Record);
            ItemData equipment = CreateItem("equipment", ItemType.Equipment);
            ItemData anomaly = CreateItem("anomaly", ItemType.Anomaly);

            Assert.That(inventory.AddItem(engineering), Is.True);
            Assert.That(inventory.AddItem(record), Is.True);
            Assert.That(inventory.AddItem(equipment), Is.True);
            Assert.That(inventory.AddItem(anomaly), Is.True);

            Assert.That(inventory.GetItem(InventorySlotGroup.Backpack, 0), Is.EqualTo(engineering));
            Assert.That(inventory.GetItem(InventorySlotGroup.Backpack, 1), Is.EqualTo(record));
            Assert.That(inventory.GetItem(InventorySlotGroup.QuickAccess, 0), Is.EqualTo(equipment));
            Assert.That(inventory.GetItem(InventorySlotGroup.Anomaly, 0), Is.EqualTo(anomaly));
        }

        [Test]
        public void EquipmentFillsAllQuickAccessSlotsInDisplayOrder()
        {
            ItemData[] equipment = new ItemData[PlayerInventory.QuickAccessCapacity];
            for (int i = 0; i < equipment.Length; i++)
            {
                equipment[i] = CreateItem($"equipment_{i}", ItemType.Equipment);
                Assert.That(inventory.AddItem(equipment[i]), Is.True);
            }

            Assert.That(inventory.GetItem(InventorySlotGroup.QuickAccess, 0), Is.EqualTo(equipment[0]));
            Assert.That(inventory.GetItem(InventorySlotGroup.QuickAccess, 1), Is.EqualTo(equipment[1]));
            Assert.That(inventory.GetItem(InventorySlotGroup.QuickAccess, 2), Is.EqualTo(equipment[2]));
            Assert.That(inventory.GetItem(InventorySlotGroup.QuickAccess, 3), Is.EqualTo(equipment[3]));
        }

        [Test]
        public void WorldItemPickupAddsItemInstanceToPlayerInventory()
        {
            ItemData item = CreateItem("world_part", ItemType.EngineeringPart);
            GameObject worldObject = new GameObject("Test_WorldItem");
            WorldItem worldItem = worldObject.AddComponent<WorldItem>();
            worldItem.Initialize(item);

            SerializedObject serializedWorldItem = new SerializedObject(worldItem);
            serializedWorldItem.FindProperty("destroyAfterPickup").boolValue = false;
            serializedWorldItem.ApplyModifiedPropertiesWithoutUndo();

            worldItem.CompleteInteraction(root);

            Assert.That(inventory.GetItem(InventorySlotGroup.Backpack, 0), Is.EqualTo(item));
            Assert.That(
                inventory.GetItemInstance(InventorySlotGroup.Backpack, 0)?.ItemData,
                Is.EqualTo(item)
            );
            Assert.That(worldObject.activeSelf, Is.False);

            Object.DestroyImmediate(worldObject);
        }

        [Test]
        public void InvalidSerializedInstancesDoNotOccupyEmptySlots()
        {
            ItemInstance invalidInstance = JsonUtility.FromJson<ItemInstance>("{}");
            inventory.RestoreInstanceSlots(
                new[] { invalidInstance, null },
                new ItemInstance[PlayerInventory.AnomalyCapacity],
                new ItemInstance[PlayerInventory.QuickAccessCapacity]
            );

            ItemData item = CreateItem("recovered_pickup", ItemType.EngineeringPart);

            Assert.That(inventory.AddItem(item), Is.True);
            Assert.That(inventory.GetItem(InventorySlotGroup.Backpack, 0), Is.EqualTo(item));
        }

        [Test]
        public void EqualItemsOccupySeparateBackpackSlots()
        {
            ItemData first = CreateItem("same_part", ItemType.EngineeringPart);
            ItemData second = CreateItem("same_part", ItemType.EngineeringPart);

            Assert.That(inventory.AddItem(first), Is.True);
            Assert.That(inventory.AddItem(second), Is.True);
            Assert.That(inventory.GetItem(InventorySlotGroup.Backpack, 0), Is.EqualTo(first));
            Assert.That(inventory.GetItem(InventorySlotGroup.Backpack, 1), Is.EqualTo(second));
            Assert.That(inventory.Count, Is.EqualTo(2));
        }

        [Test]
        public void BackpackCapacityComesFromInventoryConfig()
        {
            InventoryConfig config = ScriptableObject.CreateInstance<InventoryConfig>();
            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("backpackCapacity").intValue = 8;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            inventory.Configure(config);

            Assert.That(inventory.BackpackCapacity, Is.EqualTo(8));
            Assert.That(inventory.BackpackSlots.Count, Is.EqualTo(8));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void BackpackCapacityIsLimitedToAuthoredSpawnPoints()
        {
            InventoryConfig config = ScriptableObject.CreateInstance<InventoryConfig>();
            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("backpackCapacity").intValue = 20;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            inventory.Configure(config);

            Assert.That(
                inventory.BackpackCapacity,
                Is.EqualTo(InventoryConfig.MaxBackpackCapacity)
            );
            Assert.That(
                inventory.BackpackSlots.Count,
                Is.EqualTo(InventoryConfig.MaxBackpackCapacity)
            );

            Object.DestroyImmediate(config);
        }

        [Test]
        public void StructuredRestorePreservesEmptySlotPositions()
        {
            ItemData first = CreateItem("first", ItemType.EngineeringPart);
            ItemData third = CreateItem("third", ItemType.EngineeringPart);
            ItemData[] backpack =
            {
                first,
                null,
                third,
                null,
                null
            };

            inventory.RestoreSlots(
                backpack,
                new ItemData[PlayerInventory.AnomalyCapacity],
                new ItemData[PlayerInventory.QuickAccessCapacity]
            );

            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 0),
                Is.EqualTo(first)
            );
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 1),
                Is.Null
            );
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 2),
                Is.EqualTo(third)
            );
        }

        [Test]
        public void RemovingSelectedSlotDoesNotRemoveEqualItemFromAnotherSlot()
        {
            ItemData first = CreateItem("same_part", ItemType.EngineeringPart);
            ItemData second = CreateItem("same_part", ItemType.EngineeringPart);
            inventory.AddItem(first);
            inventory.AddItem(second);

            Assert.That(
                inventory.RemoveItemAt(
                    InventorySlotGroup.Backpack,
                    1,
                    out ItemData removed
                ),
                Is.True
            );
            Assert.That(removed, Is.EqualTo(second));
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 0),
                Is.EqualTo(first)
            );
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 1),
                Is.Null
            );
        }

        [Test]
        public void ItemsCanMoveToAnyValidInventorySlot()
        {
            ItemData first = CreateItem("first", ItemType.EngineeringPart);
            ItemData second = CreateItem("second", ItemType.EngineeringPart);
            inventory.AddItem(first);
            inventory.AddItem(second);

            Assert.That(
                inventory.TryMoveItem(
                    InventorySlotGroup.Backpack,
                    0,
                    InventorySlotGroup.Backpack,
                    4
                ),
                Is.True
            );
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 4),
                Is.EqualTo(first)
            );
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 0),
                Is.Null
            );
            Assert.That(
                inventory.GetItem(InventorySlotGroup.Backpack, 1),
                Is.EqualTo(second)
            );
        }

        [Test]
        public void RepeatedItemDataCreatesIndependentInstances()
        {
            ItemData item = CreateChargeableItem("charged_tool", ItemType.EngineeringPart);

            Assert.That(inventory.AddItem(item), Is.True);
            Assert.That(inventory.AddItem(item), Is.True);

            ItemInstance first = inventory.GetItemInstance(InventorySlotGroup.Backpack, 0);
            ItemInstance second = inventory.GetItemInstance(InventorySlotGroup.Backpack, 1);
            Assert.That(first.InstanceId, Is.Not.EqualTo(second.InstanceId));
            Assert.That(inventory.TryConsumeCharge(first, 10f), Is.True);
            Assert.That(first.Charge, Is.EqualTo(90f).Within(0.001f));
            Assert.That(second.Charge, Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void InstanceRestorePreservesIdentitySlotAndCharge()
        {
            ItemData item = CreateChargeableItem("restored_tool", ItemType.EngineeringPart);
            ItemInstance instance = ItemInstance.Create(item);
            instance.TryConsume(35f);

            inventory.RestoreInstanceSlots(
                new[] { instance, null },
                new ItemInstance[PlayerInventory.AnomalyCapacity],
                new ItemInstance[PlayerInventory.QuickAccessCapacity]
            );

            ItemInstance restored = inventory.GetItemInstance(InventorySlotGroup.Backpack, 0);
            Assert.That(restored.InstanceId, Is.EqualTo(instance.InstanceId));
            Assert.That(restored.Charge, Is.EqualTo(65f).Within(0.001f));
            Assert.That(inventory.GetItem(InventorySlotGroup.Backpack, 1), Is.Null);
        }

        [Test]
        public void UnifiedEquipmentUseConsumesConfiguredItemEnergy()
        {
            ItemData scanner = CreateChargeableItem("scanner", ItemType.Equipment);
            SerializedObject serializedScanner = new SerializedObject(scanner);
            serializedScanner.FindProperty("quickAccessAction").enumValueIndex =
                (int)QuickAccessAction.Scan;
            serializedScanner.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(inventory.AddItem(scanner), Is.True);
            ItemInstance instance = inventory.GetItemInstance(
                InventorySlotGroup.QuickAccess,
                PlayerInventory.ActiveQuickAccessStartIndex
            );
            PlayerEquipmentController equipment =
                root.AddComponent<PlayerEquipmentController>();

            Assert.That(equipment.TryUseItem(instance), Is.True);
            Assert.That(instance.Charge, Is.EqualTo(90f).Within(0.001f));
            Assert.That(equipment.TryUseItem(instance), Is.True);
            Assert.That(instance.Charge, Is.EqualTo(80f).Within(0.001f));
        }

        [Test]
        public void OrdinaryEnergyWeaponStillUsesItsFireAction()
        {
            ItemData weapon = CreateChargeableItem(
                "ordinary_energy_weapon",
                ItemType.Equipment);
            SerializedObject serializedWeapon = new SerializedObject(weapon);
            serializedWeapon.FindProperty("quickAccessAction").enumValueIndex =
                (int)QuickAccessAction.Fire;
            serializedWeapon.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(inventory.AddItem(weapon), Is.True);
            ItemInstance instance = inventory.GetItemInstance(
                InventorySlotGroup.QuickAccess,
                PlayerInventory.ActiveQuickAccessStartIndex);
            PlayerEquipmentController equipment =
                root.AddComponent<PlayerEquipmentController>();
            bool fireRequested = false;
            equipment.EquipmentUseRequested += (_, action) =>
            {
                fireRequested = action == QuickAccessAction.Fire;
                return fireRequested;
            };

            Assert.That(equipment.TryUseItem(instance), Is.True);
            Assert.That(fireRequested, Is.True);
            Assert.That(instance.Charge, Is.EqualTo(90f).Within(0.001f));
            Assert.That(instance.AnomalyIntegration, Is.Null);
        }

        private ItemData CreateChargeableItem(string id, ItemType type)
        {
            ItemEnergyDefinition energy = ScriptableObject.CreateInstance<ItemEnergyDefinition>();
            SerializedObject serializedEnergy = new SerializedObject(energy);
            serializedEnergy.FindProperty("capacity").floatValue = 100f;
            serializedEnergy.FindProperty("initialCharge").floatValue = 100f;
            serializedEnergy.FindProperty("energyPerUse").floatValue = 10f;
            serializedEnergy.FindProperty("rechargePerSecond").floatValue = 20f;
            serializedEnergy.ApplyModifiedPropertiesWithoutUndo();
            createdEnergyDefinitions.Add(energy);

            ItemData item = CreateItem(id, type);
            SerializedObject serializedItem = new SerializedObject(item);
            serializedItem.FindProperty("energyDefinition").objectReferenceValue = energy;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private ItemData CreateItem(string id, ItemType type)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = id;
            serialized.FindProperty("displayName").stringValue = id;
            serialized.FindProperty("itemType").enumValueIndex = (int)type;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            createdItems.Add(item);
            return item;
        }
    }

    public sealed class AnomalyIntegrationTests
    {
        private GameObject player;
        private GameObject systems;
        private PlayerInventory inventory;
        private PlayerEquipmentController equipmentController;
        private ResearchController research;
        private LaboratoryWorkstationController workstation;
        private ItemData tool;
        private ItemData anomaly;
        private ResearchDefinition researchDefinition;
        private AnomalyIntegrationDefinition integrationDefinition;
        private ItemEnergyDefinition toolEnergy;

        [SetUp]
        public void SetUp()
        {
            player = new GameObject("Test_AnomalyToolPlayer");
            inventory = player.AddComponent<PlayerInventory>();
            equipmentController =
                player.AddComponent<PlayerEquipmentController>();

            systems = new GameObject("Test_AnomalyIntegrationSystems");
            research = systems.AddComponent<ResearchController>();
            workstation =
                systems.AddComponent<LaboratoryWorkstationController>();
            SetSingleton(
                typeof(ResearchController),
                research);

            integrationDefinition =
                ScriptableObject.CreateInstance<
                    AnomalyIntegrationDefinition>();
            SerializedObject serializedIntegration =
                new SerializedObject(integrationDefinition);
            serializedIntegration.FindProperty("integrationId").stringValue =
                "test_io_pulse";
            serializedIntegration.FindProperty("displayName").stringValue =
                "Test IO Pulse";
            serializedIntegration.ApplyModifiedPropertiesWithoutUndo();

            researchDefinition =
                ScriptableObject.CreateInstance<ResearchDefinition>();
            SerializedObject serializedResearch =
                new SerializedObject(researchDefinition);
            serializedResearch.FindProperty("researchId").stringValue =
                "research_test_io_pulse";
            serializedResearch.ApplyModifiedPropertiesWithoutUndo();

            toolEnergy =
                ScriptableObject.CreateInstance<ItemEnergyDefinition>();
            SerializedObject serializedEnergy =
                new SerializedObject(toolEnergy);
            serializedEnergy.FindProperty("capacity").floatValue = 100f;
            serializedEnergy.FindProperty("initialCharge").floatValue = 100f;
            serializedEnergy.FindProperty("rechargePerSecond").floatValue =
                20f;
            serializedEnergy.ApplyModifiedPropertiesWithoutUndo();

            tool = CreateItem("test_io_integrator", ItemType.Equipment);
            SerializedObject serializedTool =
                new SerializedObject(tool);
            serializedTool.FindProperty("acceptsAnomalyIntegration")
                .boolValue = true;
            serializedTool.FindProperty("energyDefinition")
                .objectReferenceValue = toolEnergy;
            serializedTool.ApplyModifiedPropertiesWithoutUndo();
            anomaly = CreateItem("test_io_shard", ItemType.Anomaly);
            SerializedObject serializedAnomaly =
                new SerializedObject(anomaly);
            serializedAnomaly.FindProperty("researchDefinition")
                .objectReferenceValue = researchDefinition;
            serializedAnomaly.FindProperty("anomalyIntegrationDefinition")
                .objectReferenceValue = integrationDefinition;
            serializedAnomaly.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(inventory.AddItem(tool), Is.True);
            Assert.That(inventory.AddItem(anomaly), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            SetSingleton(
                typeof(ResearchController),
                null);
            Object.DestroyImmediate(systems);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(tool);
            Object.DestroyImmediate(anomaly);
            Object.DestroyImmediate(researchDefinition);
            Object.DestroyImmediate(integrationDefinition);
            Object.DestroyImmediate(toolEnergy);
        }

        [Test]
        public void SynthesisRequiresResearchAndConsumesOnlyTheAnomaly()
        {
            Assert.That(
                workstation.LoadUpgradeItem(
                    0,
                    inventory,
                    InventorySlotGroup.QuickAccess,
                    0),
                Is.True);
            Assert.That(
                workstation.LoadUpgradeItem(
                    1,
                    inventory,
                    InventorySlotGroup.Anomaly,
                    0),
                Is.True);

            Assert.That(
                workstation.CanSynthesize(out string reason),
                Is.False);
            Assert.That(reason, Does.Contain("Scan"));

            research.RestoreAnalyzed(
                new[] { researchDefinition.ResearchId });

            Assert.That(
                workstation.CanSynthesize(out reason),
                Is.False,
                "Knowing the anomaly type must not scan this instance.");
            Assert.That(reason, Does.Contain("Scan"));

            ItemInstance anomalyInstance =
                workstation.GetUpgradeItem(1);
            Assert.That(anomalyInstance.MarkScanned(), Is.True);

            Assert.That(
                workstation.CanSynthesize(out reason),
                Is.True,
                reason);
            Assert.That(workstation.TrySynthesize(), Is.True);

            ItemInstance integratedTool =
                workstation.GetUpgradeItem(0);
            Assert.That(integratedTool, Is.Not.Null);
            Assert.That(integratedTool.ItemData, Is.SameAs(tool));
            Assert.That(
                integratedTool.IntegratedAnomaly,
                Is.SameAs(anomaly));
            Assert.That(integratedTool.AnomalyCharges, Is.EqualTo(1));
            Assert.That(workstation.GetUpgradeItem(1), Is.Null);
        }

        [Test]
        public void IntegratedEffectConsumesItsSingleCharge()
        {
            ItemInstance instance = inventory.GetItemInstance(
                InventorySlotGroup.QuickAccess,
                0);
            ItemInstance anomalyInstance = inventory.GetItemInstance(
                InventorySlotGroup.Anomaly,
                0);
            Assert.That(
                instance.TryInstallAnomaly(anomalyInstance),
                Is.False,
                "An unscanned anomaly instance must be rejected.");
            Assert.That(anomalyInstance.MarkScanned(), Is.True);
            Assert.That(
                instance.TryInstallAnomaly(anomalyInstance),
                Is.True);
            ItemInstance secondAnomalyInstance =
                ItemInstance.Create(anomaly);
            Assert.That(secondAnomalyInstance.MarkScanned(), Is.True);
            Assert.That(
                instance.TryInstallAnomaly(secondAnomalyInstance),
                Is.False,
                "An installed anomaly must not be replaced before use.");
            equipmentController.AnomalyUseRequested += (_, _) => true;

            Assert.That(
                equipmentController.TryUseIntegratedAnomaly(instance),
                Is.True);
            Assert.That(instance.AnomalyCharges, Is.Zero);
            Assert.That(instance.Charge, Is.Zero);
            Assert.That(instance.IntegratedAnomaly, Is.Null);
            Assert.That(
                equipmentController.TryUseIntegratedAnomaly(instance),
                Is.False);

            instance.Recharge(instance.MaxCharge);
            Assert.That(
                instance.TryInstallAnomaly(anomalyInstance),
                Is.True);
            Assert.That(instance.AnomalyCharges, Is.EqualTo(1));
        }

        [Test]
        public void OrdinaryEquipmentCannotAcceptAnomalyIntegration()
        {
            ItemData ordinaryEquipment =
                CreateItem("test_ordinary_equipment", ItemType.Equipment);
            try
            {
                Assert.That(
                    integrationDefinition.Supports(ordinaryEquipment),
                    Is.False);
                Assert.That(
                    inventory.AddItem(ordinaryEquipment),
                    Is.True);
                Assert.That(
                    workstation.LoadUpgradeItem(
                        0,
                        inventory,
                        InventorySlotGroup.QuickAccess,
                        1),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(ordinaryEquipment);
            }
        }

        [Test]
        public void RestorePreservesIntegratedAnomalyAndCharges()
        {
            ItemInstance instance = ItemInstance.Create(tool);
            ItemInstance anomalyInstance = ItemInstance.Create(anomaly);
            Assert.That(anomalyInstance.MarkScanned(), Is.True);
            Assert.That(
                instance.TryInstallAnomaly(anomalyInstance),
                Is.True);

            ItemInstance restored = ItemInstance.Restore(
                instance.InstanceId,
                tool,
                instance.Charge,
                anomaly,
                instance.AnomalyCharges,
                true);

            Assert.That(
                restored.InstanceId,
                Is.EqualTo(instance.InstanceId));
            Assert.That(restored.IntegratedAnomaly, Is.SameAs(anomaly));
            Assert.That(restored.AnomalyCharges, Is.EqualTo(1));
            Assert.That(restored.IsScanned, Is.True);
        }

        private static ItemData CreateItem(
            string itemId,
            ItemType itemType)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = itemId;
            serialized.FindProperty("displayName").stringValue = itemId;
            serialized.FindProperty("itemType").enumValueIndex =
                (int)itemType;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static void SetSingleton(
            System.Type controllerType,
            object value)
        {
            PropertyInfo instanceProperty = controllerType.GetProperty(
                "Instance",
                BindingFlags.Static | BindingFlags.Public);
            instanceProperty?.GetSetMethod(true)?.Invoke(
                null,
                new[] { value });
        }
    }

    public sealed class Sprint05LaboratoryTests
    {
        private GameObject systems;
        private GameObject player;
        private StationPowerController power;
        private ResearchController research;
        private PlayerInventory inventory;
        private ResearchDefinition definition;
        private LibraryEntryData libraryEntry;
        private ItemData sample;

        [SetUp]
        public void SetUp()
        {
            systems = new GameObject("Test_ResearchSystems");
            power = systems.AddComponent<StationPowerController>();
            research = systems.AddComponent<ResearchController>();
            research.SetPowerSource(power);

            player = new GameObject("Test_ResearchPlayer");
            inventory = player.AddComponent<PlayerInventory>();

            libraryEntry = ScriptableObject.CreateInstance<LibraryEntryData>();
            SerializedObject serializedEntry = new SerializedObject(libraryEntry);
            serializedEntry.FindProperty("entryId").stringValue = "test_sample_entry";
            serializedEntry.FindProperty("title").stringValue = "Test Sample Entry";
            serializedEntry.ApplyModifiedPropertiesWithoutUndo();

            definition = ScriptableObject.CreateInstance<ResearchDefinition>();
            SerializedObject serializedDefinition = new SerializedObject(definition);
            serializedDefinition.FindProperty("researchId").stringValue = "test_sample_research";
            serializedDefinition.FindProperty("displayName").stringValue = "Test Sample";
            serializedDefinition.FindProperty("analysisDuration").floatValue = 2f;
            serializedDefinition.FindProperty("unlockedEntry").objectReferenceValue = libraryEntry;
            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();

            sample = ScriptableObject.CreateInstance<ItemData>();
            SerializedObject serializedItem = new SerializedObject(sample);
            serializedItem.FindProperty("itemId").stringValue = "test_sample";
            serializedItem.FindProperty("displayName").stringValue = "Test Sample";
            serializedItem.FindProperty("itemType").enumValueIndex = (int)ItemType.Anomaly;
            serializedItem.FindProperty("researchDefinition").objectReferenceValue = definition;
            serializedItem.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(sample);
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(libraryEntry);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(systems);
        }

        [Test]
        public void EachSampleMustBeScannedOnceWithoutDuplicateResearch()
        {
            int researchNotifications = 0;
            research.ResearchAnalyzed += _ => researchNotifications++;
            power.RestorePower();
            Assert.That(inventory.AddItem(sample), Is.True);
            Assert.That(inventory.AddItem(sample), Is.True);
            ItemInstance firstSample = inventory.GetItemInstance(
                InventorySlotGroup.Anomaly,
                0);
            ItemInstance secondSample = inventory.GetItemInstance(
                InventorySlotGroup.Anomaly,
                1);
            Assert.That(firstSample.InstanceId, Is.Not.EqualTo(secondSample.InstanceId));

            Assert.That(
                research.LoadItem(
                    sample,
                    inventory,
                    InventorySlotGroup.Anomaly,
                    0),
                Is.True,
                "First sample should enter the laboratory slot.");
            Assert.That(research.StartAnalysis(), Is.True, "Powered laboratory should start scanning.");

            research.AdvanceAnalysis(2f);

            Assert.That(research.State, Is.EqualTo(ResearchController.ResearchState.Complete));
            Assert.That(research.LoadedItem, Is.EqualTo(sample));
            Assert.That(research.IsAnalyzed(sample), Is.True);
            Assert.That(firstSample.IsScanned, Is.True);
            Assert.That(secondSample.IsScanned, Is.False);
            Assert.That(researchNotifications, Is.EqualTo(1));

            Assert.That(research.RetrieveLoadedItem(), Is.True);
            Assert.That(inventory.Contains(sample.ItemId), Is.True);

            Assert.That(
                research.LoadItem(
                    sample,
                    inventory,
                    InventorySlotGroup.Anomaly,
                    0),
                Is.True,
                "The scanned sample should remain loadable for inspection.");
            Assert.That(
                research.State,
                Is.EqualTo(ResearchController.ResearchState.ItemLoaded));
            Assert.That(research.CanStartAnalysis, Is.False);
            Assert.That(research.StartAnalysis(), Is.False);
            Assert.That(research.StatusMessage, Does.Contain("already scanned"));
            Assert.That(research.RetrieveLoadedItem(), Is.True);

            Assert.That(
                research.LoadItem(
                    sample,
                    inventory,
                    InventorySlotGroup.Anomaly,
                    1),
                Is.True,
                "A second instance of the known type must still require scanning.");
            Assert.That(research.LoadedItemInstance, Is.SameAs(secondSample));
            Assert.That(research.CanStartAnalysis, Is.True);
            Assert.That(research.StartAnalysis(), Is.True);
            research.AdvanceAnalysis(2f);

            Assert.That(
                research.State,
                Is.EqualTo(ResearchController.ResearchState.Complete));
            Assert.That(secondSample.IsScanned, Is.True);
            Assert.That(researchNotifications, Is.EqualTo(1));
            Assert.That(
                research.AnalyzedResearchIds.Count,
                Is.EqualTo(1));
        }

        [Test]
        public void KnownItemCanUseLaboratorySlotWithoutCreatingResearch()
        {
            ItemData knownItem = ScriptableObject.CreateInstance<ItemData>();
            SerializedObject serialized = new SerializedObject(knownItem);
            serialized.FindProperty("itemId").stringValue = "known_part";
            serialized.FindProperty("displayName").stringValue = "Known Part";
            serialized.FindProperty("description").stringValue = "Already identified.";
            serialized.FindProperty("itemType").enumValueIndex =
                (int)ItemType.EngineeringPart;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(inventory.AddItem(knownItem), Is.True);
            Assert.That(research.LoadItem(knownItem, inventory), Is.True);
            Assert.That(research.LoadedItem, Is.EqualTo(knownItem));
            Assert.That(research.IsResearchable(knownItem), Is.False);
            Assert.That(research.CanStartAnalysis, Is.False);
            Assert.That(research.StatusMessage, Is.EqualTo("Known Part"));
            Assert.That(research.RetrieveLoadedItem(), Is.True);

            Object.DestroyImmediate(knownItem);
        }

        [Test]
        public void LibraryCataloguesKnownStationItemsButNotAnomaliesOnPickup()
        {
            GameObject libraryRoot = new GameObject("Test_Library");
            LibraryController library = libraryRoot.AddComponent<LibraryController>();
            ItemData stationItem = CreateItem("known_station_part", ItemType.EngineeringPart);
            ItemData anomalyItem = CreateItem("unknown_anomaly", ItemType.Anomaly);

            Assert.That(library.RegisterKnownItem(stationItem), Is.True);
            Assert.That(library.IsKnownItem(stationItem), Is.True);

            Assert.That(library.RegisterKnownItem(stationItem), Is.False);
            Assert.That(library.RegisterKnownItem(anomalyItem), Is.False);
            Assert.That(library.IsKnownItem(anomalyItem), Is.False);

            Object.DestroyImmediate(stationItem);
            Object.DestroyImmediate(anomalyItem);
            Object.DestroyImmediate(libraryRoot);
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
