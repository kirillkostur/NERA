using System;
using System.Collections.Generic;
using NERA.Enemies;
using NERA.Quests;
using UnityEditor;
using UnityEngine;

namespace NERA.Editor
{
    [CustomEditor(typeof(QuestDefinition))]
    public sealed class QuestDefinitionEditor : UnityEditor.Editor
    {
        private static readonly string[] AvailabilityLabels =
        {
            "Появляется один раз",
            "Может появляться снова"
        };

        private static readonly string[] ScopeLabels =
        {
            "Один общий квест",
            "Отдельный квест для вызвавшего объекта"
        };

        private static readonly string[] CategoryLabels =
        {
            "Основное задание",
            "Второстепенное задание"
        };

        private static readonly QuestSignalType[] SignalValues =
        {
            QuestSignalType.LocationDiscovered,
            QuestSignalType.LocationEntered,
            QuestSignalType.LocationExited,
            QuestSignalType.AreaExplored,
            QuestSignalType.ItemCollected,
            QuestSignalType.ItemRemoved,
            QuestSignalType.ItemDelivered,
            QuestSignalType.InventoryItemCountChanged,
            QuestSignalType.ResearchAnalyzed,
            QuestSignalType.DroneScanCompleted,
            QuestSignalType.AntennaSignalFound,
            QuestSignalType.EnemyEncountered,
            QuestSignalType.EnemyKilled,
            QuestSignalType.EnemyWaveSpawned,
            QuestSignalType.EnemyWaveCleared,
            QuestSignalType.ObjectInteractionCompleted,
            QuestSignalType.DeviceConditionBelow,
            QuestSignalType.DeviceConditionRestored,
            QuestSignalType.StationFaultStarted,
            QuestSignalType.StationFaultResolved,
            QuestSignalType.StationSystemActivated,
            QuestSignalType.StationSystemDeactivated,
            QuestSignalType.StationSystemUpgraded,
            QuestSignalType.StationPowerOnline,
            QuestSignalType.StationPowerOffline,
            QuestSignalType.EnergyChargeChanged,
            QuestSignalType.StationAttackStarted,
            QuestSignalType.StationAttackRepelled,
            QuestSignalType.WeatherChanged,
            QuestSignalType.QuestCompleted,
            QuestSignalType.TimerElapsed,
            QuestSignalType.Custom
        };

        private static readonly string[] SignalLabels =
        {
            "Локации/Локация обнаружена",
            "Локации/Игрок вошёл в локацию",
            "Локации/Игрок покинул локацию",
            "Локации/Точка локации исследована",
            "Предметы/Предмет получен (событие)",
            "Предметы/Предмет покинул инвентарь (событие)",
            "Предметы/Предмет передан в цель (событие)",
            "Предметы/Количество предмета в инвентаре (состояние)",
            "Исследование и разведка/Исследование завершено в лаборатории",
            "Исследование и разведка/Сканирование дроном завершено",
            "Исследование и разведка/Антенной обнаружен сигнал",
            "Враги/Враг обнаружен",
            "Враги/Враг уничтожен",
            "Враги/Волна врагов создана",
            "Враги/Волна врагов уничтожена",
            "Объекты/Взаимодействие с объектом завершено",
            "Станция — состояние объектов/Состояние ниже порога",
            "Станция — состояние объектов/Состояние восстановлено",
            "Станция — неисправности/Неисправность возникла",
            "Станция — неисправности/Неисправность устранена",
            "Станция — системы/Система включена",
            "Станция — системы/Система выключена",
            "Станция — системы/Система улучшена",
            "Станция — питание/Общая сеть восстановлена",
            "Станция — питание/Общая сеть отключена",
            "Станция — питание/Уровень заряда",
            "Станция — нападение/Нападение началось",
            "Станция — нападение/Нападение отражено",
            "Окружение/Погода изменилась",
            "Квесты и служебные/После завершения квеста",
            "Квесты и служебные/Таймер завершён",
            "Квесты и служебные/Пользовательское событие"
        };

        private static readonly string[] ConditionLogicLabels =
        {
            "Нужно выполнить все условия",
            "Достаточно любого условия"
        };

        private static readonly string[] EvaluationLabels =
        {
            "Только после нового события",
            "Если уже выполнено — засчитать сразу"
        };

        private static readonly string[] ComparisonLabels =
        {
            "Меньше",
            "Не больше",
            "Равно",
            "Не меньше",
            "Больше"
        };

        private SerializedProperty questId;
        private SerializedProperty category;
        private SerializedProperty availability;
        private SerializedProperty targetScope;
        private SerializedProperty title;
        private SerializedProperty description;
        private SerializedProperty priority;
        private SerializedProperty showInHud;
        private SerializedProperty activationLogic;
        private SerializedProperty activationConditions;
        private SerializedProperty stages;
        private SerializedProperty weatherActionOnActivation;
        private SerializedProperty weatherActionOnCompletion;
        private SerializedProperty sandstormDurationMinSeconds;
        private SerializedProperty sandstormDurationMaxSeconds;

        private void OnEnable()
        {
            questId = serializedObject.FindProperty("questId");
            category = serializedObject.FindProperty("category");
            availability = serializedObject.FindProperty("availability");
            targetScope = serializedObject.FindProperty("targetScope");
            title = serializedObject.FindProperty("title");
            description = serializedObject.FindProperty("description");
            priority = serializedObject.FindProperty("priority");
            showInHud = serializedObject.FindProperty("showInHud");
            activationLogic = serializedObject.FindProperty(
                "activationLogic");
            activationConditions =
                serializedObject.FindProperty("activationConditions");
            stages = serializedObject.FindProperty("stages");
            weatherActionOnActivation = serializedObject.FindProperty(
                "weatherActionOnActivation");
            weatherActionOnCompletion = serializedObject.FindProperty(
                "weatherActionOnCompletion");
            sandstormDurationMinSeconds = serializedObject.FindProperty(
                "sandstormDurationMinSeconds");
            sandstormDurationMaxSeconds = serializedObject.FindProperty(
                "sandstormDurationMaxSeconds");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawInspectorHeader();
            DrawBehaviour();
            DrawPresentation();
            DrawActivation();
            DrawStages();
            DrawEnvironmentActions();

            bool changed = serializedObject.ApplyModifiedProperties();
            if (changed)
                EditorUtility.SetDirty(target);

            DrawValidation();
        }

        private static void DrawInspectorHeader()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                "Настройка квеста",
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Квест появляется по игровому событию, проходит этапы сверху " +
                "вниз и завершается после выполнения всех условий последнего " +
                "этапа.",
                MessageType.Info);
        }

        private void DrawBehaviour()
        {
            DrawSection("1. Поведение");

            availability.enumValueIndex = EditorGUILayout.Popup(
                new GUIContent(
                    "Повторяемость",
                    "Можно ли снова показать квест после завершения."),
                availability.enumValueIndex,
                AvailabilityLabels);

            category.enumValueIndex = EditorGUILayout.Popup(
                new GUIContent(
                    "Раздел журнала",
                    "Влияет только на группировку в интерфейсе."),
                category.enumValueIndex,
                CategoryLabels);

            targetScope.enumValueIndex = EditorGUILayout.Popup(
                new GUIContent(
                    "Цель квеста",
                    "Общий квест или независимый экземпляр для каждого " +
                    "объекта, вызвавшего событие."),
                targetScope.enumValueIndex,
                ScopeLabels);

            bool repeatable = availability.enumValueIndex ==
                (int)QuestAvailability.Repeatable;
            bool perObject = IsPerObject;
            string explanation = repeatable
                ? "После завершения квест сможет появиться снова, когда его " +
                  "условие появления сработает повторно."
                : "После первого завершения квест больше не появится в этом " +
                  "сохранении.";
            if (perObject)
            {
                explanation += " Для каждого вызвавшего объекта создаётся " +
                    "свой экземпляр без дубликатов.";
            }

            EditorGUILayout.HelpBox(explanation, MessageType.None);
        }

        private void DrawPresentation()
        {
            DrawSection("2. Название и отображение");
            EditorGUILayout.PropertyField(
                questId,
                new GUIContent(
                    "Quest ID",
                    "Стабильный ID сохранения. После релиза не менять."));
            EditorGUILayout.PropertyField(title, new GUIContent("Название"));
            EditorGUILayout.PropertyField(
                description,
                new GUIContent("Описание"));

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(
                    priority,
                    new GUIContent("Приоритет"));
                EditorGUILayout.PropertyField(
                    showInHud,
                    new GUIContent("Показывать в HUD"));
            }

            if (IsPerObject)
            {
                EditorGUILayout.HelpBox(
                    "В названии и описании можно использовать {targetName}. " +
                    "Например: «Очистите {targetName}».",
                    MessageType.None);
            }
        }

        private void DrawActivation()
        {
            DrawSection("3. Когда появляется квест");

            if (activationConditions.arraySize == 0)
            {
                EditorGUILayout.HelpBox(
                    "Без условия квест запустится автоматически при старте " +
                    "игровой сессии. Это подходит только для одноразового " +
                    "общего квеста.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    activationConditions.arraySize == 1
                        ? "Квест появится, когда произойдёт это событие."
                        : activationLogic.enumValueIndex ==
                            (int)QuestConditionLogic.All
                            ? "Квест появится после выполнения всех " +
                              "условий ниже."
                            : "Квест появится после выполнения любого " +
                              "условия ниже.",
                    MessageType.None);
            }

            if (activationConditions.arraySize > 1)
                DrawConditionLogic(activationLogic);

            DrawConditionList(activationConditions, true);
        }

        private void DrawStages()
        {
            DrawSection("4. Этапы квеста");
            EditorGUILayout.HelpBox(
                "Этапы выполняются по порядку. Если у этапа несколько " +
                "условий, способ их объединения задаётся внутри этапа.",
                MessageType.None);

            for (int index = 0; index < stages.arraySize; index++)
            {
                SerializedProperty stage =
                    stages.GetArrayElementAtIndex(index);
                SerializedProperty stageTitle =
                    stage.FindPropertyRelative("title");
                SerializedProperty stageDescription =
                    stage.FindPropertyRelative("description");
                SerializedProperty completionLogic =
                    stage.FindPropertyRelative("completionLogic");
                SerializedProperty createCheckpointOnCompletion =
                    stage.FindPropertyRelative(
                        "createCheckpointOnCompletion");
                SerializedProperty enemySpawnerIdsOnStart =
                    stage.FindPropertyRelative(
                        "enemySpawnerIdsOnStart");
                SerializedProperty enemySpawnerIdsOnCompletion =
                    stage.FindPropertyRelative(
                        "enemySpawnerIdsOnCompletion");
                SerializedProperty completionConditions =
                    stage.FindPropertyRelative("completionConditions");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            $"Этап {index + 1}",
                            EditorStyles.boldLabel);
                        DrawMoveButtons(stages, index);
                        if (GUILayout.Button("Удалить", GUILayout.Width(65f)))
                        {
                            stages.DeleteArrayElementAtIndex(index);
                            break;
                        }
                    }

                    EditorGUILayout.PropertyField(
                        stageTitle,
                        new GUIContent("Задача игроку"));
                    EditorGUILayout.PropertyField(
                        stageDescription,
                        new GUIContent("Подробности"));
                    EditorGUILayout.PropertyField(
                        createCheckpointOnCompletion,
                        new GUIContent(
                            "Чекпоинт после завершения",
                            "После завершения этого этапа сохраняет весь " +
                            "прогресс и текущую позицию игрока."));
                    DrawEnemySpawnerActions(
                        enemySpawnerIdsOnStart,
                        "Спавн при входе в этап",
                        "Вызывается, когда этот этап становится активным.");
                    DrawEnemySpawnerActions(
                        enemySpawnerIdsOnCompletion,
                        "Спавн после завершения",
                        "Вызывается после выполнения этапа, но до " +
                        "чекпоинта.");
                    EditorGUILayout.LabelField(
                        "Условия завершения этапа",
                        EditorStyles.boldLabel);
                    if (completionConditions.arraySize > 1)
                        DrawConditionLogic(completionLogic);
                    DrawConditionList(completionConditions, false);
                }

                EditorGUILayout.Space(3f);
            }

            if (GUILayout.Button("+ Добавить этап", GUILayout.Height(26f)))
                AddStage(stages, IsPerObject);
        }

        private void DrawEnvironmentActions()
        {
            DrawSection("5. Действия с погодой");
            EditorGUILayout.HelpBox(
                "Квест может запустить или остановить песчаную бурю при " +
                "появлении либо завершении. Сами квесты также могут ждать " +
                "события Weather Changed с ID sandstorm или clear.",
                MessageType.None);

            EditorGUILayout.PropertyField(
                weatherActionOnActivation,
                new GUIContent("При появлении квеста"));
            EditorGUILayout.PropertyField(
                weatherActionOnCompletion,
                new GUIContent("После завершения квеста"));

            bool startsSandstorm =
                weatherActionOnActivation.enumValueIndex ==
                    (int)QuestWeatherAction.StartSandstorm ||
                weatherActionOnCompletion.enumValueIndex ==
                    (int)QuestWeatherAction.StartSandstorm;
            if (!startsSandstorm)
                return;

            EditorGUILayout.PropertyField(
                sandstormDurationMinSeconds,
                new GUIContent("Длительность от, сек"));
            EditorGUILayout.PropertyField(
                sandstormDurationMaxSeconds,
                new GUIContent("Длительность до, сек"));
            sandstormDurationMinSeconds.floatValue = Mathf.Max(
                0f,
                sandstormDurationMinSeconds.floatValue);
            sandstormDurationMaxSeconds.floatValue = Mathf.Max(
                sandstormDurationMinSeconds.floatValue,
                sandstormDurationMaxSeconds.floatValue);

            if (sandstormDurationMaxSeconds.floatValue <= 0f)
            {
                EditorGUILayout.HelpBox(
                    "Будет использован диапазон из Station Environment " +
                    "Config.",
                    MessageType.Info);
            }
        }

        private void DrawConditionList(
            SerializedProperty list,
            bool activation)
        {
            for (int index = 0; index < list.arraySize; index++)
            {
                SerializedProperty condition =
                    list.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(
                            $"Условие {index + 1}",
                            EditorStyles.miniBoldLabel);
                        DrawMoveButtons(list, index);
                        if (GUILayout.Button("×", GUILayout.Width(24f)))
                        {
                            list.DeleteArrayElementAtIndex(index);
                            break;
                        }
                    }

                    DrawCondition(condition, activation);
                }
            }

            if (GUILayout.Button("+ Добавить условие"))
                AddCondition(list, activation, IsPerObject);
        }

        private void DrawCondition(
            SerializedProperty condition,
            bool activation)
        {
            SerializedProperty signal =
                condition.FindPropertyRelative("signalType");
            SerializedProperty evaluation =
                condition.FindPropertyRelative("evaluation");
            SerializedProperty conditionTarget =
                condition.FindPropertyRelative("target");
            SerializedProperty targetId =
                condition.FindPropertyRelative("targetId");
            SerializedProperty cause =
                condition.FindPropertyRelative("cause");
            SerializedProperty requiredCount =
                condition.FindPropertyRelative("requiredCount");
            SerializedProperty comparison =
                condition.FindPropertyRelative("comparison");
            SerializedProperty threshold =
                condition.FindPropertyRelative("threshold");

            QuestSignalType previousSignal =
                (QuestSignalType)signal.enumValueIndex;
            int selectedSignal = Array.IndexOf(
                SignalValues,
                previousSignal);
            if (selectedSignal < 0)
                selectedSignal = 0;
            selectedSignal = EditorGUILayout.Popup(
                "Игровое событие",
                selectedSignal,
                SignalLabels);
            QuestSignalType signalType =
                SignalValues[selectedSignal];
            signal.enumValueIndex = (int)signalType;
            if (signalType != previousSignal)
            {
                evaluation.enumValueIndex =
                    (int)QuestConditionEvaluation.Event;
                comparison.enumValueIndex =
                    (int)QuestValueComparison.GreaterOrEqual;
                requiredCount.intValue = 1;
                threshold.floatValue =
                    signalType ==
                            QuestSignalType.InventoryItemCountChanged ||
                        signalType == QuestSignalType.StationSystemUpgraded
                        ? 1f
                        : 0.5f;
            }

            DrawSignalHint(signalType);

            bool numericState = UsesNumericState(signalType);
            if (numericState)
            {
                evaluation.enumValueIndex =
                    (int)QuestConditionEvaluation.CurrentState;
                EditorGUILayout.LabelField(
                    "Когда засчитывать условие",
                    "По текущему значению");
            }
            else if (QuestConditionDefinition.SupportsCurrentState(signalType))
            {
                evaluation.enumValueIndex = EditorGUILayout.Popup(
                    new GUIContent(
                        "Когда засчитывать условие",
                        "Можно ждать новое событие или сразу учесть уже " +
                        "выполненное состояние."),
                    evaluation.enumValueIndex,
                    EvaluationLabels);

                bool useCurrentState = evaluation.enumValueIndex ==
                    (int)QuestConditionEvaluation.CurrentState;
                EditorGUILayout.HelpBox(
                    useCurrentState
                        ? "Если условие уже выполнено, оно засчитается " +
                          "сразу. Иначе система будет ждать его выполнения."
                        : "Засчитается только событие, произошедшее после " +
                          "начала этого условия. Выполненное раньше не " +
                          "учитывается.",
                    MessageType.Info);
            }
            else
            {
                evaluation.enumValueIndex =
                    (int)QuestConditionEvaluation.Event;
            }

            if (TryGetFixedTarget(
                    signalType,
                    out string fixedTargetId,
                    out string fixedTargetLabel))
            {
                conditionTarget.enumValueIndex =
                    (int)QuestConditionTarget.SpecificObject;
                targetId.stringValue = fixedTargetId;
                EditorGUILayout.LabelField("Объект", fixedTargetLabel);
            }
            else
            {
                DrawTargetSelector(
                    conditionTarget,
                    activation,
                    signalType);
                if (conditionTarget.enumValueIndex ==
                    (int)QuestConditionTarget.SpecificObject)
                {
                    EditorGUILayout.PropertyField(
                        targetId,
                        GetTargetIdContent(signalType));
                }
            }

            if (UsesCauseFilter(signalType))
            {
                EditorGUILayout.PropertyField(
                    cause,
                    new GUIContent(
                        "Причина или тег (необязательно)",
                        "Пусто — принимать событие с любой причиной."));
            }

            if (signalType == QuestSignalType.DeviceConditionBelow ||
                signalType == QuestSignalType.DeviceConditionRestored)
            {
                threshold.floatValue = EditorGUILayout.Slider(
                    signalType == QuestSignalType.DeviceConditionBelow
                        ? "Порог: не выше"
                        : "Порог: не ниже",
                    threshold.floatValue,
                    0f,
                    1f);
                EditorGUILayout.LabelField(
                    $"{Mathf.RoundToInt(threshold.floatValue * 100f)}%",
                    EditorStyles.miniLabel);
            }
            else if (numericState)
            {
                comparison.enumValueIndex = EditorGUILayout.Popup(
                    "Сравнение",
                    comparison.enumValueIndex,
                    ComparisonLabels);

                if (signalType == QuestSignalType.EnergyChargeChanged)
                {
                    threshold.floatValue = EditorGUILayout.Slider(
                        "Уровень заряда",
                        threshold.floatValue,
                        0f,
                        1f);
                    EditorGUILayout.LabelField(
                        $"{Mathf.RoundToInt(threshold.floatValue * 100f)}%",
                        EditorStyles.miniLabel);
                }
                else
                {
                    int value = Mathf.Max(
                        0,
                        Mathf.RoundToInt(threshold.floatValue));
                    threshold.floatValue = EditorGUILayout.IntField(
                        signalType ==
                            QuestSignalType.InventoryItemCountChanged
                            ? "Количество предметов"
                            : "Уровень улучшения",
                        value);
                    threshold.floatValue = Mathf.Max(
                        0f,
                        threshold.floatValue);
                }
            }

            if (!numericState)
            {
                bool currentState = evaluation.enumValueIndex ==
                    (int)QuestConditionEvaluation.CurrentState;
                EditorGUILayout.PropertyField(
                    requiredCount,
                    new GUIContent(
                        currentState
                            ? "Сколько объектов"
                            : "Сколько раз",
                        currentState
                            ? "Сколько подходящих объектов должно сейчас " +
                              "соответствовать условию."
                            : "Например, исследовать 3 точки или уничтожить " +
                              "5 врагов."));
                requiredCount.intValue = Mathf.Max(
                    1,
                    requiredCount.intValue);
            }
            else
            {
                requiredCount.intValue = 1;
            }
        }

        private void DrawTargetSelector(
            SerializedProperty conditionTarget,
            bool activation,
            QuestSignalType signalType)
        {
            List<QuestConditionTarget> values =
                new List<QuestConditionTarget>();
            List<string> labels = new List<string>();

            if (!activation && IsPerObject)
            {
                values.Add(QuestConditionTarget.QuestTarget);
                labels.Add("Объект, вызвавший этот квест");
            }

            values.Add(QuestConditionTarget.SpecificObject);
            labels.Add(GetSpecificTargetLabel(signalType));
            values.Add(QuestConditionTarget.AnyObject);
            labels.Add(
                signalType == QuestSignalType.QuestCompleted
                    ? "Завершение любого квеста"
                    : "Любой объект этого события");

            QuestConditionTarget current =
                (QuestConditionTarget)conditionTarget.enumValueIndex;
            int selected = values.IndexOf(current);
            if (selected < 0)
                selected = 0;

            int next = EditorGUILayout.Popup(
                "Какой объект",
                selected,
                labels.ToArray());
            conditionTarget.enumValueIndex = (int)values[next];
        }

        private static void DrawConditionLogic(
            SerializedProperty logic)
        {
            logic.enumValueIndex = EditorGUILayout.Popup(
                new GUIContent(
                    "Как объединять",
                    "Все — логика И. Любое — логика ИЛИ."),
                logic.enumValueIndex,
                ConditionLogicLabels);
        }

        private static bool UsesNumericState(QuestSignalType signalType)
        {
            return signalType ==
                    QuestSignalType.InventoryItemCountChanged ||
                signalType == QuestSignalType.StationSystemUpgraded ||
                signalType == QuestSignalType.EnergyChargeChanged;
        }

        private static void DrawEnemySpawnerActions(
            SerializedProperty spawnerIds,
            string title,
            string description)
        {
            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField(
                title,
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                description + " Один спавнер можно использовать в любом " +
                "количестве квестов и этапов.",
                MessageType.None);

            List<string> knownIds = GetLoadedSpawnerIds();
            for (int index = 0; index < spawnerIds.arraySize; index++)
            {
                SerializedProperty spawnerId =
                    spawnerIds.GetArrayElementAtIndex(index);
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawSpawnerIdField(spawnerId, knownIds);
                    if (GUILayout.Button("Удалить", GUILayout.Width(65f)))
                    {
                        spawnerIds.DeleteArrayElementAtIndex(index);
                        break;
                    }
                }
            }

            if (GUILayout.Button("+ Вызвать EnemySpawner"))
            {
                int index = spawnerIds.arraySize;
                spawnerIds.arraySize++;
                spawnerIds.GetArrayElementAtIndex(index).stringValue =
                    knownIds.Count > 0 ? knownIds[0] : string.Empty;
            }
        }

        private static void DrawSpawnerIdField(
            SerializedProperty spawnerId,
            IReadOnlyList<string> knownIds)
        {
            string current = spawnerId.stringValue?.Trim() ?? string.Empty;
            spawnerId.stringValue = EditorGUILayout.TextField(
                "Spawner ID",
                current);
            if (knownIds.Count == 0)
                return;

            string[] options = new string[knownIds.Count + 1];
            options[0] = "Из открытой сцены…";
            for (int index = 0; index < knownIds.Count; index++)
                options[index + 1] = knownIds[index];

            int selected = EditorGUILayout.Popup(
                0,
                options,
                GUILayout.Width(180f));
            if (selected > 0)
                spawnerId.stringValue = options[selected];
        }

        private static List<string> GetLoadedSpawnerIds()
        {
            EnemySpawner[] spawners =
                UnityEngine.Object.FindObjectsByType<EnemySpawner>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            List<string> result = new List<string>();
            foreach (EnemySpawner spawner in spawners)
            {
                if (spawner == null ||
                    !spawner.gameObject.scene.IsValid() ||
                    string.IsNullOrWhiteSpace(spawner.SpawnerId) ||
                    result.Exists(id => string.Equals(
                        id,
                        spawner.SpawnerId,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                result.Add(spawner.SpawnerId);
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        private static void DrawSignalHint(QuestSignalType signalType)
        {
            string message = signalType switch
            {
                QuestSignalType.ItemCollected or
                QuestSignalType.ItemRemoved =>
                    "Это разовое событие перемещения предмета. Для проверки " +
                    "уже имеющегося количества используйте событие " +
                    "«Количество предмета в инвентаре».",
                QuestSignalType.InventoryItemCountChanged =>
                    "Это текущее состояние инвентаря. Оно не дублирует " +
                    "разовые события получения и удаления предмета.",
                QuestSignalType.DeviceConditionBelow or
                QuestSignalType.DeviceConditionRestored =>
                    "Это числовое состояние износа объекта. Неисправность " +
                    "станции — отдельное дискретное событие с причиной.",
                QuestSignalType.StationFaultStarted or
                QuestSignalType.StationFaultResolved =>
                    "Это начало или устранение неисправности. Для обычного " +
                    "порога износа используйте события состояния объекта.",
                QuestSignalType.StationSystemActivated or
                QuestSignalType.StationSystemDeactivated =>
                    "Относится к конкретной системе станции. События питания " +
                    "относятся ко всей энергетической сети.",
                QuestSignalType.StationPowerOnline or
                QuestSignalType.StationPowerOffline =>
                    "Относится ко всей энергетической сети станции, а не к " +
                    "отдельному модулю.",
                QuestSignalType.EnemyWaveSpawned or
                QuestSignalType.EnemyWaveCleared =>
                    "Target ID должен совпадать со Spawner ID нужного " +
                    "EnemySpawner.",
                _ => string.Empty
            };

            if (!string.IsNullOrEmpty(message))
                EditorGUILayout.HelpBox(message, MessageType.Info);

            bool requiresManualSource =
                signalType == QuestSignalType.ItemDelivered ||
                signalType == QuestSignalType.StationAttackStarted ||
                signalType == QuestSignalType.StationAttackRepelled ||
                signalType == QuestSignalType.TimerElapsed;
            if (requiresManualSource)
            {
                EditorGUILayout.HelpBox(
                    "Автоматический игровой источник для этого события " +
                    "пока не подключён. Сейчас его нужно отправлять через " +
                    "QuestSignalEmitter или из кода игровой системы.",
                    MessageType.Warning);
            }
        }

        private static bool TryGetFixedTarget(
            QuestSignalType signalType,
            out string targetId,
            out string label)
        {
            if (signalType == QuestSignalType.StationPowerOnline ||
                signalType == QuestSignalType.StationPowerOffline)
            {
                targetId = "station_power";
                label = "Питание станции (station_power)";
                return true;
            }

            if (signalType == QuestSignalType.EnergyChargeChanged)
            {
                targetId = "station_energy";
                label = "Энергосистема станции (station_energy)";
                return true;
            }

            targetId = string.Empty;
            label = string.Empty;
            return false;
        }

        private static bool UsesCauseFilter(QuestSignalType signalType)
        {
            return signalType == QuestSignalType.StationFaultStarted ||
                signalType == QuestSignalType.StationFaultResolved ||
                signalType == QuestSignalType.ItemDelivered ||
                signalType == QuestSignalType.StationAttackStarted ||
                signalType == QuestSignalType.StationAttackRepelled ||
                signalType == QuestSignalType.WeatherChanged ||
                signalType == QuestSignalType.Custom;
        }

        private static string GetSpecificTargetLabel(
            QuestSignalType signalType)
        {
            return signalType switch
            {
                QuestSignalType.QuestCompleted =>
                    "Конкретный квест по Quest ID",
                QuestSignalType.ItemCollected or
                QuestSignalType.ItemRemoved or
                QuestSignalType.ItemDelivered or
                QuestSignalType.InventoryItemCountChanged =>
                    "Конкретный предмет по Item ID",
                QuestSignalType.WeatherChanged =>
                    "Конкретная погода по ID",
                QuestSignalType.EnemyWaveSpawned or
                QuestSignalType.EnemyWaveCleared =>
                    "Конкретный спавнер по Spawner ID",
                QuestSignalType.Custom =>
                    "Конкретное событие по Event ID",
                _ => "Конкретный объект по ID"
            };
        }

        private static GUIContent GetTargetIdContent(
            QuestSignalType signalType)
        {
            return signalType switch
            {
                QuestSignalType.QuestCompleted => new GUIContent(
                    "Quest ID",
                    "Quest ID задания, после завершения которого должно " +
                    "сработать условие."),
                QuestSignalType.ItemCollected or
                QuestSignalType.ItemRemoved or
                QuestSignalType.ItemDelivered or
                QuestSignalType.InventoryItemCountChanged => new GUIContent(
                    "Item ID",
                    "Стабильный Item ID предмета."),
                QuestSignalType.WeatherChanged => new GUIContent(
                    "Weather ID",
                    "clear, cloudy или sandstorm."),
                QuestSignalType.EnemyWaveSpawned or
                QuestSignalType.EnemyWaveCleared => new GUIContent(
                    "Spawner ID",
                    "Стабильный ID компонента EnemySpawner."),
                QuestSignalType.Custom => new GUIContent(
                    "Event ID",
                    "Стабильный ID пользовательского события."),
                _ => new GUIContent(
                    "ID объекта",
                    "Стабильный ID локации, исследования, врага или " +
                    "устройства.")
            };
        }

        private static void DrawMoveButtons(
            SerializedProperty list,
            int index)
        {
            using (new EditorGUI.DisabledScope(index == 0))
            {
                if (GUILayout.Button("↑", GUILayout.Width(24f)))
                    list.MoveArrayElement(index, index - 1);
            }

            using (new EditorGUI.DisabledScope(
                       index >= list.arraySize - 1))
            {
                if (GUILayout.Button("↓", GUILayout.Width(24f)))
                    list.MoveArrayElement(index, index + 1);
            }
        }

        private static void AddCondition(
            SerializedProperty list,
            bool activation,
            bool perObject)
        {
            int index = list.arraySize;
            list.arraySize++;
            SerializedProperty condition = list.GetArrayElementAtIndex(index);
            condition.FindPropertyRelative("signalType").enumValueIndex =
                (int)QuestSignalType.LocationDiscovered;
            condition.FindPropertyRelative("evaluation").enumValueIndex =
                (int)QuestConditionEvaluation.Event;
            condition.FindPropertyRelative("target").enumValueIndex =
                activation
                    ? perObject
                        ? (int)QuestConditionTarget.AnyObject
                        : (int)QuestConditionTarget.SpecificObject
                    : perObject
                        ? (int)QuestConditionTarget.QuestTarget
                        : (int)QuestConditionTarget.SpecificObject;
            condition.FindPropertyRelative("targetId").stringValue =
                string.Empty;
            condition.FindPropertyRelative("cause").stringValue =
                string.Empty;
            condition.FindPropertyRelative("requiredCount").intValue = 1;
            condition.FindPropertyRelative("comparison").enumValueIndex =
                (int)QuestValueComparison.GreaterOrEqual;
            condition.FindPropertyRelative("threshold").floatValue = 0.5f;
        }

        private static void AddStage(
            SerializedProperty list,
            bool perObject)
        {
            int index = list.arraySize;
            list.arraySize++;
            SerializedProperty stage = list.GetArrayElementAtIndex(index);
            stage.FindPropertyRelative("title").stringValue = "Новый этап";
            stage.FindPropertyRelative("description").stringValue =
                string.Empty;
            stage.FindPropertyRelative("completionLogic").enumValueIndex =
                (int)QuestConditionLogic.All;
            stage.FindPropertyRelative("createCheckpointOnCompletion")
                .boolValue = false;
            stage.FindPropertyRelative("enemySpawnerIdsOnStart")
                .arraySize = 0;
            stage.FindPropertyRelative("enemySpawnerIdsOnCompletion")
                .arraySize = 0;
            SerializedProperty conditions =
                stage.FindPropertyRelative("completionConditions");
            conditions.arraySize = 0;
            AddCondition(conditions, false, perObject);
        }

        private void DrawValidation()
        {
            QuestDefinition definition = (QuestDefinition)target;
            if (definition.TryValidate(out string error))
            {
                EditorGUILayout.HelpBox(
                    "Конфиг квеста заполнен корректно.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
            }
        }

        private bool IsPerObject => targetScope.enumValueIndex ==
            (int)QuestTargetScope.PerTriggeringObject;

        private static void DrawSection(string title)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }
    }
}
