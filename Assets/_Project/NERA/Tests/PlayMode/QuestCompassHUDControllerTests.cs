using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NERA.Navigation;
using NERA.Quests;
using NERA.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NERA.Tests.PlayMode
{
    public sealed class QuestCompassHUDControllerTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [UnityTest]
        public IEnumerator MarkerTracksSpawnedPlayerAndFadeDistance()
        {
            Assert.That(QuestController.Instance, Is.Null);

            GameObject runtimeRoot = CreateGameObject("Runtime Root");
            GameObject player = CreateGameObject("Player");
            player.tag = "Player";
            player.transform.SetParent(runtimeRoot.transform, false);
            GameObject cameraObject = CreateGameObject("Gameplay Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(runtimeRoot.transform, false);
            cameraObject.AddComponent<Camera>();

            GameObject markerObject = CreateGameObject("Quest Target");
            markerObject.transform.position = new Vector3(0f, 0f, 60f);
            QuestMarkerAnchor anchor =
                markerObject.AddComponent<QuestMarkerAnchor>();
            SetPrivateField(anchor, "markerId", "test.target");
            SetPrivateField(anchor, "localOffset", Vector3.zero);
            SetPrivateField(anchor, "worldMarkerFadeDistance", 2f);
            SetPrivateField(anchor, "worldMarkerMaxDistance", 50f);

            QuestDefinition definition = CreateQuest();
            QuestCatalog catalog = ScriptableObject.CreateInstance<QuestCatalog>();
            createdObjects.Add(catalog);
            SetPrivateField(
                catalog,
                "definitions",
                new List<QuestDefinition> { definition });

            GameObject questObject = CreateGameObject("Quest Controller");
            QuestController quests = questObject.AddComponent<QuestController>();
            quests.Configure(catalog);

            GameObject canvasObject = new GameObject(
                "HUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            createdObjects.Add(canvasObject);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            QuestCompassHUDController hud =
                canvasObject.AddComponent<QuestCompassHUDController>();

            yield return null;
            hud.RefreshNow();

            Assert.That(
                cameraObject.transform.root,
                Is.EqualTo(runtimeRoot.transform));
            Assert.That(hud.CompassMarkerCount, Is.EqualTo(1));
            Assert.That(hud.VisibleWorldMarkerCount, Is.EqualTo(0));
            Assert.That(
                hud.TryGetMarkerState("test.target", out var farState),
                Is.True);
            Assert.That(farState.CompassPosition.x, Is.EqualTo(0f).Within(1f));
            Assert.That(farState.CompassDistance, Is.EqualTo("60m"));

            player.transform.position = new Vector3(0f, 0f, 10f);
            hud.RefreshNow();
            Assert.That(hud.VisibleWorldMarkerCount, Is.EqualTo(1));
            Assert.That(
                hud.TryGetMarkerState("test.target", out var nearState),
                Is.True);
            Assert.That(nearState.WorldVisible, Is.True);
            Assert.That(nearState.WorldDistance, Is.EqualTo("50m"));

            player.transform.position = new Vector3(0f, 0f, 59f);
            hud.RefreshNow();
            Assert.That(hud.CompassMarkerCount, Is.EqualTo(1));
            Assert.That(hud.VisibleWorldMarkerCount, Is.EqualTo(0));
            Assert.That(
                hud.TryGetMarkerState("test.target", out var hiddenState),
                Is.True);
            Assert.That(hiddenState.CompassDistance, Is.EqualTo("1m"));

            player.transform.position = new Vector3(0f, 0f, 57f);
            hud.RefreshNow();
            Assert.That(hud.VisibleWorldMarkerCount, Is.EqualTo(1));
            Assert.That(
                hud.TryGetMarkerState("test.target", out var visibleAgain),
                Is.True);
            Assert.That(visibleAgain.WorldDistance, Is.EqualTo("3m"));

            Assert.That(
                quests.Report(QuestSignalType.Custom, "complete.marker"),
                Is.True);
            hud.RefreshNow();
            Assert.That(hud.CompassMarkerCount, Is.EqualTo(0));
            Assert.That(hud.VisibleWorldMarkerCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator CompletedConditionHidesOnlyItsOwnMarker()
        {
            Assert.That(QuestController.Instance, Is.Null);

            GameObject runtimeRoot = CreateGameObject("Runtime Root");
            GameObject player = CreateGameObject("Player");
            player.tag = "Player";
            player.transform.SetParent(runtimeRoot.transform, false);
            GameObject cameraObject = CreateGameObject("Gameplay Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(runtimeRoot.transform, false);
            cameraObject.AddComponent<Camera>();

            CreateMarkerAnchor("Stage Marker", "test.stage");
            CreateMarkerAnchor("First Marker", "test.first");
            CreateMarkerAnchor("Second Marker", "test.second");

            QuestDefinition definition = CreateConditionMarkerQuest();
            QuestCatalog catalog = ScriptableObject.CreateInstance<QuestCatalog>();
            createdObjects.Add(catalog);
            SetPrivateField(
                catalog,
                "definitions",
                new List<QuestDefinition> { definition });

            GameObject questObject = CreateGameObject("Quest Controller");
            QuestController quests = questObject.AddComponent<QuestController>();
            quests.Configure(catalog);

            GameObject canvasObject = new GameObject(
                "HUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            createdObjects.Add(canvasObject);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            QuestCompassHUDController hud =
                canvasObject.AddComponent<QuestCompassHUDController>();

            yield return null;
            hud.RefreshNow();

            Assert.That(hud.CompassMarkerCount, Is.EqualTo(3));
            Assert.That(
                hud.TryGetMarkerState("test.stage", out _),
                Is.True);
            Assert.That(
                hud.TryGetMarkerState("test.first", out _),
                Is.True);
            Assert.That(
                hud.TryGetMarkerState("test.second", out _),
                Is.True);

            Assert.That(
                quests.Report(QuestSignalType.Custom, "complete.first"),
                Is.True);
            hud.RefreshNow();

            Assert.That(hud.CompassMarkerCount, Is.EqualTo(2));
            Assert.That(
                hud.TryGetMarkerState("test.stage", out _),
                Is.True);
            Assert.That(
                hud.TryGetMarkerState("test.first", out _),
                Is.False);
            Assert.That(
                hud.TryGetMarkerState("test.second", out _),
                Is.True);

            Assert.That(
                quests.Report(QuestSignalType.Custom, "complete.second"),
                Is.True);
            hud.RefreshNow();

            Assert.That(hud.CompassMarkerCount, Is.EqualTo(0));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                    Object.Destroy(createdObjects[index]);
            }

            createdObjects.Clear();
            yield return null;
        }

        private QuestDefinition CreateQuest()
        {
            QuestConditionDefinition condition =
                new QuestConditionDefinition();
            SetPrivateField(
                condition,
                "signalType",
                QuestSignalType.Custom);
            SetPrivateField(
                condition,
                "target",
                QuestConditionTarget.SpecificObject);
            SetPrivateField(condition, "targetId", "complete.marker");

            QuestStageDefinition stage = new QuestStageDefinition();
            SetPrivateField(stage, "title", "Reach the marker");
            SetPrivateField(
                stage,
                "questMarkerIds",
                new List<string> { "test.target" });
            SetPrivateField(
                stage,
                "completionConditions",
                new List<QuestConditionDefinition> { condition });

            QuestDefinition definition =
                ScriptableObject.CreateInstance<QuestDefinition>();
            createdObjects.Add(definition);
            SetPrivateField(definition, "questId", "test.compass");
            SetPrivateField(definition, "title", "Compass test");
            SetPrivateField(
                definition,
                "availability",
                QuestAvailability.Once);
            SetPrivateField(
                definition,
                "targetScope",
                QuestTargetScope.Single);
            SetPrivateField(
                definition,
                "activationConditions",
                new List<QuestConditionDefinition>());
            SetPrivateField(
                definition,
                "stages",
                new List<QuestStageDefinition> { stage });
            return definition;
        }

        private QuestDefinition CreateConditionMarkerQuest()
        {
            QuestConditionDefinition firstCondition =
                new QuestConditionDefinition();
            SetPrivateField(
                firstCondition,
                "signalType",
                QuestSignalType.Custom);
            SetPrivateField(
                firstCondition,
                "target",
                QuestConditionTarget.SpecificObject);
            SetPrivateField(
                firstCondition,
                "targetId",
                "complete.first");
            SetPrivateField(
                firstCondition,
                "questMarkerId",
                "test.first");

            QuestConditionDefinition secondCondition =
                new QuestConditionDefinition();
            SetPrivateField(
                secondCondition,
                "signalType",
                QuestSignalType.Custom);
            SetPrivateField(
                secondCondition,
                "target",
                QuestConditionTarget.SpecificObject);
            SetPrivateField(
                secondCondition,
                "targetId",
                "complete.second");
            SetPrivateField(
                secondCondition,
                "questMarkerId",
                "test.second");

            QuestStageDefinition stage = new QuestStageDefinition();
            SetPrivateField(stage, "title", "Complete both targets");
            SetPrivateField(
                stage,
                "completionLogic",
                QuestConditionLogic.All);
            SetPrivateField(
                stage,
                "questMarkerIds",
                new List<string> { "test.stage" });
            SetPrivateField(
                stage,
                "completionConditions",
                new List<QuestConditionDefinition>
                {
                    firstCondition,
                    secondCondition
                });

            QuestDefinition definition =
                ScriptableObject.CreateInstance<QuestDefinition>();
            createdObjects.Add(definition);
            SetPrivateField(definition, "questId", "test.condition-markers");
            SetPrivateField(definition, "title", "Condition marker test");
            SetPrivateField(
                definition,
                "availability",
                QuestAvailability.Once);
            SetPrivateField(
                definition,
                "targetScope",
                QuestTargetScope.Single);
            SetPrivateField(
                definition,
                "activationConditions",
                new List<QuestConditionDefinition>());
            SetPrivateField(
                definition,
                "stages",
                new List<QuestStageDefinition> { stage });
            return definition;
        }

        private QuestMarkerAnchor CreateMarkerAnchor(
            string objectName,
            string markerId)
        {
            GameObject markerObject = CreateGameObject(objectName);
            markerObject.transform.position = new Vector3(0f, 0f, 10f);
            QuestMarkerAnchor anchor =
                markerObject.AddComponent<QuestMarkerAnchor>();
            SetPrivateField(anchor, "markerId", markerId);
            SetPrivateField(anchor, "localOffset", Vector3.zero);
            return anchor;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
