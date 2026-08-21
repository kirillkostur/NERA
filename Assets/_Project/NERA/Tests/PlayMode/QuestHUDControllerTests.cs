using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NERA.Quests;
using NERA.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NERA.Tests
{
    public sealed class QuestHUDControllerTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [UnityTest]
        public IEnumerator DisplaysPriorityPoolsAndRefillsFreedSlot()
        {
            Assert.That(QuestController.Instance, Is.Null);

            QuestDefinition[] definitions =
            {
                CreateQuest("main.10", QuestCategory.Main, 10),
                CreateQuest("main.20", QuestCategory.Main, 20),
                CreateQuest("main.30", QuestCategory.Main, 30),
                CreateQuest("main.40", QuestCategory.Main, 40),
                CreateQuest("side.10", QuestCategory.Side, 10),
                CreateQuest("side.20", QuestCategory.Side, 20),
                CreateQuest("side.30", QuestCategory.Side, 30),
                CreateQuest("side.40", QuestCategory.Side, 40),
                CreateQuest("side.50", QuestCategory.Side, 50)
            };

            QuestCatalog catalog = ScriptableObject.CreateInstance<QuestCatalog>();
            createdObjects.Add(catalog);
            SetPrivateField(
                catalog,
                "definitions",
                new List<QuestDefinition>(definitions));

            GameObject controllerObject = CreateGameObject("Quest Controller");
            QuestController controller =
                controllerObject.AddComponent<QuestController>();
            controller.Configure(catalog);

            GameObject hudObject = CreateGameObject("Quest HUD");
            QuestHUDController hud = CreateHud(hudObject);

            yield return null;

            Assert.That(hud.MaxDisplayedMainQuests, Is.EqualTo(3));
            Assert.That(hud.MaxDisplayedSideQuests, Is.EqualTo(4));
            Assert.That(hud.DisplayedMainText, Does.Contain("main.40"));
            Assert.That(hud.DisplayedMainText, Does.Contain("main.30"));
            Assert.That(hud.DisplayedMainText, Does.Contain("main.20"));
            Assert.That(hud.DisplayedMainText, Does.Not.Contain("main.10"));
            Assert.That(hud.DisplayedSideText, Does.Contain("side.50"));
            Assert.That(hud.DisplayedSideText, Does.Contain("side.20"));
            Assert.That(hud.DisplayedSideText, Does.Not.Contain("side.10"));
            Assert.That(
                hud.DisplayedMainText,
                Does.Contain("- Objective main.40"));

            Assert.That(
                controller.Report(QuestSignalType.Custom, "complete.main.40"),
                Is.True);
            Assert.That(hud.DisplayedMainText, Does.Contain("<s>"));
            Assert.That(hud.DisplayedMainText, Does.Contain("main.40"));
            Assert.That(
                hud.DisplayedMainText,
                Does.Not.Contain("main.10"),
                "The struck-through quest keeps its slot until it disappears.");

            yield return new WaitForSecondsRealtime(
                hud.CompletedDisplayDuration + 0.1f);

            Assert.That(hud.DisplayedMainText, Does.Not.Contain("main.40"));
            Assert.That(
                hud.DisplayedMainText,
                Does.Contain("main.10"),
                "The next active quest must fill the released slot.");
        }

        [UnityTest]
        public IEnumerator DelaysNewQuestUntilCompletedQuestDisappears()
        {
            Assert.That(QuestController.Instance, Is.Null);

            QuestDefinition completedQuest = CreateQuest(
                "main.completed",
                QuestCategory.Main,
                10);
            QuestDefinition nextQuest = CreateQuest(
                "main.next",
                QuestCategory.Main,
                100);
            SetPrivateField(
                nextQuest,
                "activationConditions",
                new List<QuestConditionDefinition>
                {
                    CreateSignalCondition(
                        QuestSignalType.Custom,
                        "complete.main.completed")
                });

            QuestCatalog catalog = ScriptableObject.CreateInstance<QuestCatalog>();
            createdObjects.Add(catalog);
            SetPrivateField(
                catalog,
                "definitions",
                new List<QuestDefinition> { completedQuest, nextQuest });

            GameObject controllerObject = CreateGameObject("Quest Controller");
            QuestController controller =
                controllerObject.AddComponent<QuestController>();
            controller.Configure(catalog);

            GameObject hudObject = CreateGameObject("Quest HUD");
            QuestHUDController hud = CreateHud(hudObject);
            yield return null;

            Assert.That(hud.DisplayedMainText, Does.Contain("main.completed"));
            Assert.That(hud.DisplayedMainText, Does.Not.Contain("main.next"));

            Assert.That(
                controller.Report(
                    QuestSignalType.Custom,
                    "complete.main.completed"),
                Is.True);
            Assert.That(
                hud.DisplayedMainText,
                Does.Contain("<s>main.completed</s>"));
            Assert.That(
                hud.DisplayedMainText,
                Does.Not.Contain("main.next"),
                "The replacement must wait until the crossed-out quest disappears.");

            yield return new WaitForSecondsRealtime(
                hud.CompletedDisplayDuration + 0.1f);

            Assert.That(hud.DisplayedMainText, Does.Not.Contain("main.completed"));
            Assert.That(hud.DisplayedMainText, Does.Contain("main.next"));
        }

        [UnityTest]
        public IEnumerator GroupsPerObjectQuestsUnderOneTitle()
        {
            Assert.That(QuestController.Instance, Is.Null);

            QuestDefinition definition = CreateGroupedQuest();
            QuestCatalog catalog = ScriptableObject.CreateInstance<QuestCatalog>();
            createdObjects.Add(catalog);
            SetPrivateField(
                catalog,
                "definitions",
                new List<QuestDefinition> { definition });

            GameObject controllerObject = CreateGameObject("Quest Controller");
            QuestController controller =
                controllerObject.AddComponent<QuestController>();
            controller.Configure(catalog);

            GameObject hudObject = CreateGameObject("Quest HUD");
            QuestHUDController hud = CreateHud(hudObject);

            controller.Report(
                QuestSignalType.StationFaultStarted,
                "antenna",
                "Antenna");
            controller.Report(
                QuestSignalType.StationFaultStarted,
                "drone",
                "Drone");
            controller.Report(
                QuestSignalType.StationFaultStarted,
                "solar_panel",
                "Solar Panel");
            yield return null;

            string displayed = hud.DisplayedSideText;
            Assert.That(
                CountOccurrences(displayed, "Service objects"),
                Is.EqualTo(1));
            Assert.That(displayed, Does.Contain("- Service Antenna"));
            Assert.That(displayed, Does.Contain("- Service Drone"));
            Assert.That(displayed, Does.Contain("- Service Solar Panel"));
            QuestGroupView firstSideGroup = hudObject.transform
                .Find("background_QuestSide/Content/QuestGroup_00")
                .GetComponent<QuestGroupView>();
            Assert.That(firstSideGroup.PreferredHeight, Is.GreaterThan(60f));

            Assert.That(
                controller.Report(
                    QuestSignalType.StationSystemActivated,
                    "drone",
                    "Drone"),
                Is.True);
            Assert.That(
                hud.DisplayedSideText,
                Does.Contain("<s>- Service Drone</s>"));
            Assert.That(
                hud.DisplayedSideText,
                Does.Not.Contain("<s>Service objects"));

            yield return new WaitForSecondsRealtime(
                hud.CompletedDisplayDuration + 0.1f);

            Assert.That(hud.DisplayedSideText, Does.Not.Contain("Drone"));
            Assert.That(hud.DisplayedSideText, Does.Contain("- Service Antenna"));
            Assert.That(hud.DisplayedSideText, Does.Contain("- Service Solar Panel"));
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

        private QuestDefinition CreateQuest(
            string questId,
            QuestCategory category,
            int priority)
        {
            QuestConditionDefinition condition = CreateSignalCondition(
                QuestSignalType.Custom,
                $"complete.{questId}");

            QuestStageDefinition stage = new QuestStageDefinition();
            SetPrivateField(stage, "title", $"Objective {questId}");
            SetPrivateField(
                stage,
                "completionConditions",
                new List<QuestConditionDefinition> { condition });

            QuestDefinition definition =
                ScriptableObject.CreateInstance<QuestDefinition>();
            createdObjects.Add(definition);
            SetPrivateField(definition, "questId", questId);
            SetPrivateField(definition, "category", category);
            SetPrivateField(definition, "availability", QuestAvailability.Once);
            SetPrivateField(definition, "targetScope", QuestTargetScope.Single);
            SetPrivateField(definition, "title", questId);
            SetPrivateField(definition, "description", questId);
            SetPrivateField(definition, "priority", priority);
            SetPrivateField(definition, "showInHud", true);
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

        private static QuestConditionDefinition CreateSignalCondition(
            QuestSignalType signalType,
            string targetId)
        {
            QuestConditionDefinition condition =
                new QuestConditionDefinition();
            SetPrivateField(condition, "signalType", signalType);
            SetPrivateField(
                condition,
                "target",
                QuestConditionTarget.SpecificObject);
            SetPrivateField(condition, "targetId", targetId);
            return condition;
        }

        private QuestDefinition CreateGroupedQuest()
        {
            QuestConditionDefinition activation =
                new QuestConditionDefinition();
            SetPrivateField(
                activation,
                "signalType",
                QuestSignalType.StationFaultStarted);
            SetPrivateField(
                activation,
                "target",
                QuestConditionTarget.AnyObject);

            QuestConditionDefinition completion =
                new QuestConditionDefinition();
            SetPrivateField(
                completion,
                "signalType",
                QuestSignalType.StationSystemActivated);
            SetPrivateField(
                completion,
                "target",
                QuestConditionTarget.QuestTarget);

            QuestStageDefinition stage = new QuestStageDefinition();
            SetPrivateField(stage, "title", "Service {targetName}");
            SetPrivateField(
                stage,
                "completionConditions",
                new List<QuestConditionDefinition> { completion });

            QuestDefinition definition =
                ScriptableObject.CreateInstance<QuestDefinition>();
            createdObjects.Add(definition);
            SetPrivateField(definition, "questId", "side.service_objects");
            SetPrivateField(definition, "category", QuestCategory.Side);
            SetPrivateField(
                definition,
                "availability",
                QuestAvailability.Repeatable);
            SetPrivateField(
                definition,
                "targetScope",
                QuestTargetScope.PerTriggeringObject);
            SetPrivateField(definition, "title", "Service objects");
            SetPrivateField(definition, "description", "Service objects");
            SetPrivateField(definition, "priority", 50);
            SetPrivateField(definition, "showInHud", true);
            SetPrivateField(
                definition,
                "activationConditions",
                new List<QuestConditionDefinition> { activation });
            SetPrivateField(
                definition,
                "stages",
                new List<QuestStageDefinition> { stage });
            return definition;
        }

        private QuestHUDController CreateHud(GameObject hudObject)
        {
            RectTransform mainContent = CreateDisplayRoot(
                hudObject.transform,
                "background_QuestMain");
            RectTransform sideContent = CreateDisplayRoot(
                hudObject.transform,
                "background_QuestSide");
            QuestGroupView groupPrefab = CreateGroupTemplate();
            QuestObjectiveView objectivePrefab = CreateObjectiveTemplate();

            QuestHUDController hud =
                hudObject.AddComponent<QuestHUDController>();
            hud.ConfigureView(
                mainContent.parent.gameObject,
                mainContent,
                sideContent.parent.gameObject,
                sideContent,
                groupPrefab,
                objectivePrefab);
            return hud;
        }

        private static RectTransform CreateDisplayRoot(
            Transform hudRoot,
            string name)
        {
            GameObject background = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));
            background.transform.SetParent(hudRoot, false);
            ((RectTransform)background.transform).sizeDelta =
                new Vector2(400f, 200f);

            GameObject content = new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            RectTransform contentRect = (RectTransform)content.transform;
            contentRect.SetParent(background.transform, false);
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout =
                content.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            return contentRect;
        }

        private QuestGroupView CreateGroupTemplate()
        {
            System.Type textType = GetTextComponentType();
            GameObject template = new GameObject(
                "QuestGroupView Template",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(LayoutElement),
                typeof(QuestGroupView));
            createdObjects.Add(template);
            ((RectTransform)template.transform).sizeDelta =
                new Vector2(400f, 60f);
            VerticalLayoutGroup groupLayout =
                template.GetComponent<VerticalLayoutGroup>();
            groupLayout.childControlWidth = true;
            groupLayout.childControlHeight = true;
            groupLayout.childForceExpandWidth = false;
            groupLayout.childForceExpandHeight = false;

            GameObject title = new GameObject(
                "QuestTitle_Text",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                textType,
                typeof(LayoutElement));
            title.transform.SetParent(template.transform, false);
            ((RectTransform)title.transform).sizeDelta =
                new Vector2(400f, 30f);
            title.GetComponent<LayoutElement>().preferredHeight = 30f;

            GameObject objectives = new GameObject(
                "Objectives",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            objectives.transform.SetParent(template.transform, false);
            VerticalLayoutGroup objectiveLayout =
                objectives.GetComponent<VerticalLayoutGroup>();
            objectiveLayout.childControlWidth = true;
            objectiveLayout.childControlHeight = true;
            objectiveLayout.childForceExpandWidth = false;
            objectiveLayout.childForceExpandHeight = false;

            QuestGroupView view = template.GetComponent<QuestGroupView>();
            template.SetActive(false);
            return view;
        }

        private QuestObjectiveView CreateObjectiveTemplate()
        {
            System.Type textType = GetTextComponentType();
            GameObject template = new GameObject(
                "Quest_Text Template",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                textType,
                typeof(LayoutElement),
                typeof(QuestObjectiveView));
            createdObjects.Add(template);
            ((RectTransform)template.transform).sizeDelta =
                new Vector2(400f, 30f);
            LayoutElement layout = template.GetComponent<LayoutElement>();
            layout.minHeight = 30f;
            layout.preferredHeight = 30f;
            QuestObjectiveView view =
                template.GetComponent<QuestObjectiveView>();
            template.SetActive(false);
            return view;
        }

        private static System.Type GetTextComponentType()
        {
            System.Type type = System.Type.GetType(
                "TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Assert.That(type, Is.Not.Null);
            return type;
        }

        private static int CountOccurrences(string source, string value)
        {
            int count = 0;
            int index = 0;
            while ((index = source.IndexOf(
                       value,
                       index,
                       System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += value.Length;
            }
            return count;
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
            Assert.That(
                field,
                Is.Not.Null,
                $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
