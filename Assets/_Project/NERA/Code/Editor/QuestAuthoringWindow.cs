using System;
using System.Collections.Generic;
using System.Linq;
using NERA.Quests;
using UnityEditor;
using UnityEngine;

namespace NERA.Editor
{
    public sealed class QuestAuthoringWindow : EditorWindow
    {
        private const string ConfigFolder =
            "Assets/_Project/NERA/Configs/Quests";
        private readonly List<QuestDefinition> definitions =
            new List<QuestDefinition>();
        private Vector2 scroll;
        private string search = string.Empty;

        [MenuItem("NERA/Quests/Open Quest Editor")]
        public static void Open()
        {
            GetWindow<QuestAuthoringWindow>("NERA Quests");
        }

        private void OnEnable()
        {
            RefreshList();
            EditorApplication.projectChanged += RefreshList;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= RefreshList;
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField(
                "Квесты NERA",
                new GUIStyle(EditorStyles.largeLabel)
                {
                    fontStyle = FontStyle.Bold
                });
            EditorGUILayout.HelpBox(
                "Создайте один из двух видов квеста, затем настройте событие " +
                "появления и последовательные этапы в Inspector.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        "Создать одноразовый квест",
                        GUILayout.Height(36f)))
                {
                    CreateQuest(QuestAvailability.Once);
                }

                if (GUILayout.Button(
                        "Создать повторяемый квест",
                        GUILayout.Height(36f)))
                {
                    CreateQuest(QuestAvailability.Repeatable);
                }
            }

            EditorGUILayout.Space(6f);
            search = EditorGUILayout.TextField("Поиск", search);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawGroup("Одноразовые", QuestAvailability.Once);
            DrawGroup("Повторяемые", QuestAvailability.Repeatable);
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Обновить список"))
                    RefreshList();
                if (GUILayout.Button("Синхронизировать каталог"))
                    QuestCatalogSynchronizer.Synchronize(false);
            }
        }

        private void DrawGroup(string label, QuestAvailability availability)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            IEnumerable<QuestDefinition> filtered = definitions.Where(
                definition =>
                    definition.Availability == availability &&
                    MatchesSearch(definition));
            bool any = false;
            foreach (QuestDefinition definition in filtered)
            {
                any = true;
                using (new EditorGUILayout.HorizontalScope(
                           EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        string.IsNullOrWhiteSpace(definition.Title)
                            ? definition.name
                            : definition.Title,
                        GUILayout.MinWidth(140f));
                    EditorGUILayout.LabelField(
                        definition.QuestId,
                        EditorStyles.miniLabel,
                        GUILayout.MinWidth(150f));
                    if (GUILayout.Button("Открыть", GUILayout.Width(60f)))
                    {
                        Selection.activeObject = definition;
                        EditorGUIUtility.PingObject(definition);
                    }
                }
            }

            if (!any)
                EditorGUILayout.LabelField("Нет квестов", EditorStyles.miniLabel);
        }

        private bool MatchesSearch(QuestDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            return definition.Title.IndexOf(
                       search,
                       StringComparison.OrdinalIgnoreCase) >= 0 ||
                definition.QuestId.IndexOf(
                    search,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void CreateQuest(QuestAvailability availability)
        {
            QuestDefinition definition =
                CreateInstance<QuestDefinition>();
            string baseName = availability == QuestAvailability.Once
                ? "Quest_OneTime"
                : "Quest_Repeatable";
            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{ConfigFolder}/{baseName}.asset");
            AssetDatabase.CreateAsset(definition, path);

            SerializedObject serialized = new SerializedObject(definition);
            bool repeatable = availability == QuestAvailability.Repeatable;
            string uniqueSuffix = Guid.NewGuid()
                .ToString("N")
                .Substring(0, 8);
            serialized.FindProperty("questId").stringValue = repeatable
                ? $"side.new_repeatable_{uniqueSuffix}"
                : $"main.new_one_time_{uniqueSuffix}";
            serialized.FindProperty("category").enumValueIndex = repeatable
                ? (int)QuestCategory.Side
                : (int)QuestCategory.Main;
            serialized.FindProperty("availability").enumValueIndex =
                (int)availability;
            serialized.FindProperty("targetScope").enumValueIndex = repeatable
                ? (int)QuestTargetScope.PerTriggeringObject
                : (int)QuestTargetScope.Single;
            serialized.FindProperty("title").stringValue = "Новый квест";
            serialized.FindProperty("description").stringValue =
                "Опишите задачу игрока.";
            serialized.FindProperty("showInHud").boolValue = true;
            serialized.FindProperty("activationLogic").enumValueIndex =
                (int)QuestConditionLogic.All;

            SerializedProperty activation =
                serialized.FindProperty("activationConditions");
            activation.arraySize = 1;
            InitializeCondition(
                activation.GetArrayElementAtIndex(0),
                repeatable
                    ? QuestConditionTarget.AnyObject
                    : QuestConditionTarget.SpecificObject);

            SerializedProperty stages = serialized.FindProperty("stages");
            stages.arraySize = 1;
            SerializedProperty stage = stages.GetArrayElementAtIndex(0);
            stage.FindPropertyRelative("title").stringValue = "Новый этап";
            stage.FindPropertyRelative("description").stringValue =
                "Опишите, что нужно сделать.";
            stage.FindPropertyRelative("completionLogic").enumValueIndex =
                (int)QuestConditionLogic.All;
            stage.FindPropertyRelative("createCheckpointOnCompletion")
                .boolValue = false;
            stage.FindPropertyRelative("questMarkerIds").arraySize = 0;
            stage.FindPropertyRelative("enemySpawnerIdsOnStart")
                .arraySize = 0;
            stage.FindPropertyRelative("enemySpawnerIdsOnCompletion")
                .arraySize = 0;
            SerializedProperty completion =
                stage.FindPropertyRelative("completionConditions");
            completion.arraySize = 1;
            InitializeCondition(
                completion.GetArrayElementAtIndex(0),
                repeatable
                    ? QuestConditionTarget.QuestTarget
                    : QuestConditionTarget.SpecificObject);

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            QuestCatalogSynchronizer.Synchronize(false);
            RefreshList();

            Selection.activeObject = definition;
            EditorGUIUtility.PingObject(definition);
        }

        private static void InitializeCondition(
            SerializedProperty condition,
            QuestConditionTarget target)
        {
            condition.FindPropertyRelative("signalType").enumValueIndex =
                (int)QuestSignalType.LocationDiscovered;
            condition.FindPropertyRelative("evaluation").enumValueIndex =
                (int)QuestConditionEvaluation.Event;
            condition.FindPropertyRelative("target").enumValueIndex =
                (int)target;
            condition.FindPropertyRelative("targetId").stringValue =
                string.Empty;
            condition.FindPropertyRelative("cause").stringValue =
                string.Empty;
            condition.FindPropertyRelative("requiredCount").intValue = 1;
            condition.FindPropertyRelative("comparison").enumValueIndex =
                (int)QuestValueComparison.GreaterOrEqual;
            condition.FindPropertyRelative("threshold").floatValue = 0.5f;
        }

        private void RefreshList()
        {
            definitions.Clear();
            definitions.AddRange(AssetDatabase
                .FindAssets("t:QuestDefinition")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<QuestDefinition>)
                .Where(definition => definition != null)
                .OrderBy(definition => definition.Title, StringComparer.Ordinal));
            Repaint();
        }
    }
}
