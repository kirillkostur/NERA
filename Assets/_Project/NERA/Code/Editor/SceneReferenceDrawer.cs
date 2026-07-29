using System;
using System.IO;
using System.Linq;
using NERA.Core;
using UnityEditor;
using UnityEngine;

namespace NERA.Editor
{
    [CustomPropertyDrawer(typeof(SceneReference))]
    public sealed class SceneReferenceDrawer : PropertyDrawer
    {
        private static string[] enabledPaths = Array.Empty<string>();
        private static GUIContent[] enabledLabels = Array.Empty<GUIContent>();

        static SceneReferenceDrawer()
        {
            RebuildCache();
            EditorBuildSettings.sceneListChanged += RebuildCache;
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            SerializedProperty guid =
                property.FindPropertyRelative("assetGuid");
            SerializedProperty path =
                property.FindPropertyRelative("assetPath");
            if (guid == null || path == null)
            {
                EditorGUI.LabelField(
                    position,
                    label.text,
                    "Invalid SceneReference data");
                return;
            }

            string resolvedPath = string.IsNullOrWhiteSpace(guid.stringValue)
                ? string.Empty
                : AssetDatabase.GUIDToAssetPath(guid.stringValue);
            if (!string.IsNullOrWhiteSpace(resolvedPath) &&
                !string.Equals(
                    path.stringValue,
                    resolvedPath,
                    StringComparison.Ordinal))
            {
                path.stringValue = resolvedPath;
            }

            int enabledIndex = Array.IndexOf(
                enabledPaths,
                path.stringValue);
            bool missingOrDisabled =
                !string.IsNullOrWhiteSpace(path.stringValue) &&
                enabledIndex < 0;

            GUIContent[] options;
            int selectedIndex;
            if (missingOrDisabled)
            {
                options = new GUIContent[enabledLabels.Length + 2];
                options[0] = new GUIContent(
                    $"⚠ {Path.GetFileNameWithoutExtension(path.stringValue)} " +
                    "(missing or disabled)");
                options[1] = new GUIContent("<None>");
                Array.Copy(
                    enabledLabels,
                    0,
                    options,
                    2,
                    enabledLabels.Length);
                selectedIndex = 0;
            }
            else
            {
                options = new GUIContent[enabledLabels.Length + 1];
                options[0] = new GUIContent("<None>");
                Array.Copy(
                    enabledLabels,
                    0,
                    options,
                    1,
                    enabledLabels.Length);
                selectedIndex = enabledIndex + 1;
            }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUI.Popup(
                position,
                label,
                selectedIndex,
                options);
            if (EditorGUI.EndChangeCheck())
            {
                int pathIndex = missingOrDisabled
                    ? newIndex - 2
                    : newIndex - 1;
                if (pathIndex >= 0 && pathIndex < enabledPaths.Length)
                {
                    string selectedPath = enabledPaths[pathIndex];
                    path.stringValue = selectedPath;
                    guid.stringValue =
                        AssetDatabase.AssetPathToGUID(selectedPath);
                }
                else if (!missingOrDisabled || newIndex == 1)
                {
                    path.stringValue = string.Empty;
                    guid.stringValue = string.Empty;
                }
            }
            EditorGUI.EndProperty();
        }

        private static void RebuildCache()
        {
            enabledPaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToArray();

            enabledLabels = enabledPaths
                .Select(
                    (path, index) => new GUIContent(
                        $"{Path.GetFileNameWithoutExtension(path)} " +
                        $"[Build {index}]",
                        path))
                .ToArray();
        }
    }

}
