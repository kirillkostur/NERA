using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WaveConfig))]
public class WaveConfigEditor : Editor
{
    private SerializedProperty waves;

    private void OnEnable()
    {
        waves = serializedObject.FindProperty("waves");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        if (GUILayout.Button("➕ Добавить волну"))
            waves.arraySize++;

        for (int i = 0; i < waves.arraySize; i++)
        {
            var wave = waves.GetArrayElementAtIndex(i);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Волна {i + 1}", EditorStyles.boldLabel);

            if (GUILayout.Button("✖", GUILayout.Width(22)))
            {
                waves.DeleteArrayElementAtIndex(i);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }
            EditorGUILayout.EndHorizontal();

            // --- Основные настройки ---
            EditorGUILayout.PropertyField(
                wave.FindPropertyRelative("day"),
                new GUIContent("День запуска", "Волна активируется только в этот день")
            );
            EditorGUILayout.PropertyField(
                wave.FindPropertyRelative("useSpawnPoints"),
                new GUIContent("Использовать точки спавна", "Если включено — пауки появляются на указанных SpawnPoints")
            );

            // --- Контроль количества ---
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Контроль количества", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                wave.FindPropertyRelative("maxSpidersOnScene"),
                new GUIContent("Максимум пауков на сцене")
            );
            EditorGUILayout.PropertyField(
                wave.FindPropertyRelative("spawnEvents"),
                new GUIContent("Количество волн спавна за ночь")
            );
            EditorGUILayout.PropertyField(
                wave.FindPropertyRelative("spawnInterval"),
                new GUIContent("Интервал между спавнами (сек.)")
            );
            EditorGUILayout.PropertyField(
                wave.FindPropertyRelative("minSpidersPerSpawn"),
                new GUIContent("Мин. пауков за один спавн")
            );
            EditorGUILayout.PropertyField(
                wave.FindPropertyRelative("maxSpidersPerSpawn"),
                new GUIContent("Макс. пауков за один спавн")
            );

            // --- Размер и скорость ---
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Размер и скорость пауков", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(
                wave.FindPropertyRelative("baseSpeed"),
                new GUIContent("Базовая скорость пауков")
            );
            EditorGUILayout.PropertyField(
                wave.FindPropertyRelative("minScale"),
                new GUIContent("Минимальный масштаб (Scale)")
            );
            EditorGUILayout.PropertyField(
                wave.FindPropertyRelative("maxScale"),
                new GUIContent("Максимальный масштаб (Scale)")
            );
            EditorGUILayout.PropertyField(
                wave.FindPropertyRelative("sizeSpeedMultiplier"),
                new GUIContent("Множитель скорости от размера",
                               "Например, 0.3 → маленькие +30%, большие −30%")
            );

            // --- Префабы ---
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(
                wave.FindPropertyRelative("spiderTypes"),
                new GUIContent("Префабы пауков"),
                true
            );

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        serializedObject.ApplyModifiedProperties();
    }
}
