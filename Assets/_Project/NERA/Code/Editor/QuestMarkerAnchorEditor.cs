using NERA.Navigation;
using UnityEditor;
using UnityEngine;

namespace NERA.Editor
{
    [CustomEditor(typeof(QuestMarkerAnchor))]
    public sealed class QuestMarkerAnchorEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty markerId =
                serializedObject.FindProperty("markerId");

            EditorGUILayout.HelpBox(
                "Укажите этот ID в поле «Квестовые маркеры этапа» у " +
                "Quest Definition. Компонент можно оставить отдельной " +
                "точкой или сделать дочерним объектом нужной цели.",
                MessageType.Info);
            DrawDefaultInspector();

            if (string.IsNullOrWhiteSpace(markerId.stringValue))
            {
                EditorGUILayout.HelpBox(
                    "Marker ID пуст — квест не сможет включить маркер.",
                    MessageType.Warning);
                if (GUILayout.Button("Создать ID из имени объекта"))
                {
                    markerId.stringValue = BuildId(target.name);
                    serializedObject.ApplyModifiedProperties();
                }
            }
        }

        [MenuItem("GameObject/NERA/Quest Marker", false, 20)]
        private static void CreateQuestMarker(MenuCommand command)
        {
            GameObject markerObject = new GameObject("Quest Marker");
            Undo.RegisterCreatedObjectUndo(
                markerObject,
                "Create Quest Marker");
            if (command.context is GameObject parent)
                GameObjectUtility.SetParentAndAlign(markerObject, parent);
            markerObject.AddComponent<QuestMarkerAnchor>();
            Selection.activeGameObject = markerObject;
        }

        private static string BuildId(string value)
        {
            string result = (value ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Replace(' ', '.');
            return string.IsNullOrEmpty(result)
                ? "quest.marker"
                : result;
        }
    }
}
