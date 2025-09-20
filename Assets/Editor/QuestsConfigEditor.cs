using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;

[CustomEditor(typeof(QuestsConfig))]
public class QuestsConfigEditor : Editor
{
    private SerializedProperty quests;
    private readonly Dictionary<Object, bool> foldouts = new Dictionary<Object, bool>();

    private void OnEnable()
    {
        quests = serializedObject.FindProperty("quests");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("➕ Добавить пустую ссылку", GUILayout.Height(24)))
        {
            quests.arraySize++;
        }
        if (GUILayout.Button("📝 Создать и добавить QuestData", GUILayout.Height(24)))
        {
            CreateAndAppendQuestAsset();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Список квестов
        for (int i = 0; i < quests.arraySize; i++)
        {
            var el = quests.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Квест {i + 1}", EditorStyles.boldLabel);

            if (GUILayout.Button("▲", GUILayout.Width(24)))
            {
                if (i > 0) quests.MoveArrayElement(i, i - 1);
            }
            if (GUILayout.Button("▼", GUILayout.Width(24)))
            {
                if (i < quests.arraySize - 1) quests.MoveArrayElement(i, i + 1);
            }
            if (GUILayout.Button("✖", GUILayout.Width(24)))
            {
                quests.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            // Поле ссылки на QuestData
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(el, new GUIContent("QuestData (asset)"));
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            // Если ссылка пустая — предложим создать
            if (el.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Ссылка на QuestData не назначена.", MessageType.Info);
                if (GUILayout.Button("Создать новый QuestData и назначить"))
                {
                    var newAsset = CreateQuestAsset();
                    el.objectReferenceValue = newAsset;
                    serializedObject.ApplyModifiedProperties();
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space();
                continue;
            }

            // Инлайн-редактор для назначенного QuestData
            DrawQuestDataInline(el.objectReferenceValue);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawQuestDataInline(Object questObj)
    {
        if (!foldouts.ContainsKey(questObj)) foldouts[questObj] = true;
        foldouts[questObj] = EditorGUILayout.Foldout(foldouts[questObj], "Параметры QuestData", true);

        if (!foldouts[questObj]) return;

        using (new EditorGUI.IndentLevelScope())
        {
            var so = new SerializedObject(questObj);
            so.Update();

            // Безопасно получаем свойства
            var questID = so.FindProperty("questID");
            var questName = so.FindProperty("questName");
            var description = so.FindProperty("description");
            var conditionType = so.FindProperty("conditionType");
            var targetObject = so.FindProperty("targetObjectName");
            var requiredCount = so.FindProperty("requiredCount");
            var rewardXP = so.FindProperty("rewardXP");
            var rewardCredits = so.FindProperty("rewardCredits");

            // Если структура неожиданно изменилась — подсказка вместо краша
            if (questID == null || questName == null || description == null || conditionType == null)
            {
                EditorGUILayout.HelpBox("Структура QuestData не соответствует ожидаемой. Проверь поля в скрипте QuestData.", MessageType.Error);
                so.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.PropertyField(questID, new GUIContent("ID"));
            EditorGUILayout.PropertyField(questName, new GUIContent("Название"));
            EditorGUILayout.PropertyField(description, new GUIContent("Описание"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Условия", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(conditionType, new GUIContent("Тип условия"));

            // Отрисовываем параметры по типу условия
            var cond = (QuestConditionType)conditionType.enumValueIndex;

            switch (cond)
            {
                case QuestConditionType.ObjectRepairedByName:
                    if (targetObject != null)
                        EditorGUILayout.PropertyField(targetObject, new GUIContent("Имя объекта (RepairableObject.objectName)"));
                    if (requiredCount != null)
                        EditorGUILayout.PropertyField(requiredCount, new GUIContent("Требуемое количество"));
                    break;

                case QuestConditionType.SpidersKilled:
                    if (requiredCount != null)
                        EditorGUILayout.PropertyField(requiredCount, new GUIContent("Сколько пауков убить"));
                    break;

                case QuestConditionType.BatteryStarted:
                    EditorGUILayout.HelpBox("Требуется однократный запуск аккумулятора.", MessageType.None);
                    if (requiredCount != null) requiredCount.intValue = 1; // фиксируем 1
                    break;

                case QuestConditionType.Custom:
                    EditorGUILayout.HelpBox("Custom пока не используется — можно расширить позднее.", MessageType.Info);
                    break;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Награды", EditorStyles.boldLabel);
            if (rewardXP != null) EditorGUILayout.PropertyField(rewardXP, new GUIContent("Опыт"));
            if (rewardCredits != null) EditorGUILayout.PropertyField(rewardCredits, new GUIContent("Кредиты"));

            so.ApplyModifiedProperties();

            // Кнопки быстрого открытия/перехода к ассету
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Выделить в Project", GUILayout.Height(20)))
            {
                Selection.activeObject = questObj;
                EditorGUIUtility.PingObject(questObj);
            }
            if (GUILayout.Button("Открыть отдельным инспектором", GUILayout.Height(20)))
            {
                EditorUtility.FocusProjectWindow();
                Selection.activeObject = questObj;
                EditorGUIUtility.PingObject(questObj);
                EditorApplication.ExecuteMenuItem("Window/General/Inspector");
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void CreateAndAppendQuestAsset()
    {
        var newAsset = CreateQuestAsset();
        if (newAsset == null) return;

        quests.arraySize++;
        quests.GetArrayElementAtIndex(quests.arraySize - 1).objectReferenceValue = newAsset;
        serializedObject.ApplyModifiedProperties();
    }

    private QuestData CreateQuestAsset()
    {
        // Путь рядом с конфигом
        string cfgPath = AssetDatabase.GetAssetPath(target);
        if (string.IsNullOrEmpty(cfgPath))
        {
            // Если конфиг ещё не сохранён как asset
            EditorUtility.DisplayDialog("Сначала сохраните QuestsConfig", "Сохраните QuestsConfig как asset в Project, чтобы можно было создавать QuestData рядом с ним.", "OK");
            return null;
        }

        string dir = Path.GetDirectoryName(cfgPath);
        string baseName = "QuestData";
        string uniquePath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(dir, baseName + ".asset"));

        var asset = ScriptableObject.CreateInstance<QuestData>();
        AssetDatabase.CreateAsset(asset, uniquePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(asset);
        return asset;
    }
}
