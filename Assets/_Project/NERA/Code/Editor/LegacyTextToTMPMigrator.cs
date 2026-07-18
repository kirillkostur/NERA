using System;
using NERA.Terminal;
using NERA.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NERA.EditorTools
{
    public static class LegacyTextToTMPMigrator
    {
        private const string ProjectRoot = "Assets/_Project/NERA";

        [MenuItem("NERA/UI/Migrate All Legacy Text To TMP")]
        public static void MigrateAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("NERA TMP migration must be run outside Play Mode.");
                return;
            }

            EditorSceneManager.SaveOpenScenes();
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            int converted = 0;

            try
            {
                converted += MigratePrefabs();
                converted += MigrateScenes();
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            int remaining = CountLegacyTextAssets();
            if (remaining == 0)
            {
                Debug.Log(
                    $"NERA TMP migration complete. Converted {converted} Legacy Text components. " +
                    "No Legacy Text remains in project-owned scenes or prefabs."
                );
            }
            else
            {
                Debug.LogError(
                    $"NERA TMP migration converted {converted} components, but {remaining} " +
                    "Legacy Text components still remain."
                );
            }
        }

        [MenuItem("NERA/UI/Validate TMP Only")]
        public static void ValidateTmpOnly()
        {
            int remaining = CountLegacyTextAssets();
            if (remaining == 0)
                Debug.Log("NERA UI validation passed: all project-owned UI text uses TextMeshPro.");
            else
                Debug.LogError($"NERA UI validation failed: {remaining} Legacy Text components remain.");
        }

        private static int MigratePrefabs()
        {
            int converted = 0;
            string[] prefabGuids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { ProjectRoot }
            );

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int prefabCount = ConvertHierarchy(root);
                    if (prefabCount <= 0)
                        continue;

                    WireKnownViews(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    converted += prefabCount;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return converted;
        }

        private static int MigrateScenes()
        {
            int converted = 0;
            string[] sceneGuids = AssetDatabase.FindAssets(
                "t:Scene",
                new[] { ProjectRoot }
            );

            foreach (string guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int sceneCount = 0;

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    sceneCount += ConvertHierarchy(root);
                    WireKnownViews(root);
                }

                if (sceneCount <= 0)
                    continue;

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                converted += sceneCount;
            }

            return converted;
        }

        private static int ConvertHierarchy(GameObject root)
        {
            Text[] legacyTexts = root.GetComponentsInChildren<Text>(true);
            foreach (Text legacy in legacyTexts)
                ConvertText(legacy);

            return legacyTexts.Length;
        }

        private static void ConvertText(Text legacy)
        {
            GameObject owner = legacy.gameObject;
            string value = legacy.text;
            Color color = legacy.color;
            int fontSize = legacy.fontSize;
            TextAnchor alignment = legacy.alignment;
            FontStyle fontStyle = legacy.fontStyle;
            bool richText = legacy.supportRichText;
            bool bestFit = legacy.resizeTextForBestFit;
            int minSize = legacy.resizeTextMinSize;
            int maxSize = legacy.resizeTextMaxSize;
            float lineSpacing = legacy.lineSpacing;
            bool wraps = legacy.horizontalOverflow == HorizontalWrapMode.Wrap;
            bool overflows = legacy.verticalOverflow == VerticalWrapMode.Overflow;
            bool raycastTarget = legacy.raycastTarget;
            bool maskable = legacy.maskable;

            UnityEngine.Object.DestroyImmediate(legacy, true);

            TextMeshProUGUI tmp = owner.AddComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;

            tmp.text = value;
            tmp.color = color;
            tmp.fontSize = fontSize;
            tmp.alignment = ConvertAlignment(alignment);
            tmp.fontStyle = ConvertFontStyle(fontStyle);
            tmp.richText = richText;
            tmp.enableAutoSizing = bestFit;
            tmp.fontSizeMin = minSize;
            tmp.fontSizeMax = maxSize;
            tmp.lineSpacing = lineSpacing;
            tmp.textWrappingMode = wraps
                ? TextWrappingModes.Normal
                : TextWrappingModes.NoWrap;
            tmp.overflowMode = overflows
                ? TextOverflowModes.Overflow
                : TextOverflowModes.Truncate;
            tmp.raycastTarget = raycastTarget;
            tmp.maskable = maskable;
        }

        private static TextAlignmentOptions ConvertAlignment(TextAnchor alignment)
        {
            return alignment switch
            {
                TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
                TextAnchor.UpperCenter => TextAlignmentOptions.Top,
                TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
                TextAnchor.MiddleLeft => TextAlignmentOptions.MidlineLeft,
                TextAnchor.MiddleCenter => TextAlignmentOptions.Center,
                TextAnchor.MiddleRight => TextAlignmentOptions.MidlineRight,
                TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
                TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
                TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
                _ => TextAlignmentOptions.Center
            };
        }

        private static FontStyles ConvertFontStyle(FontStyle style)
        {
            return style switch
            {
                FontStyle.Bold => FontStyles.Bold,
                FontStyle.Italic => FontStyles.Italic,
                FontStyle.BoldAndItalic => FontStyles.Bold | FontStyles.Italic,
                _ => FontStyles.Normal
            };
        }

        private static void WireKnownViews(GameObject root)
        {
            foreach (InteractionPromptView view in
                     root.GetComponentsInChildren<InteractionPromptView>(true))
            {
                SetObjectReference(
                    view,
                    "promptText",
                    view.GetComponentInChildren<TMP_Text>(true)
                );
            }

            foreach (TerminalUIScreen terminal in
                     root.GetComponentsInChildren<TerminalUIScreen>(true))
            {
                SetObjectReference(terminal, "statusText", FindText(root, "StatusText"));
                SetObjectReference(terminal, "mapText", FindText(root, "MapText"));
                SetObjectReference(
                    terminal,
                    "locationListText",
                    FindText(root, "LocationListText")
                );
                SetObjectReference(terminal, "libraryText", FindText(root, "LibraryText"));
            }
        }

        private static TMP_Text FindText(GameObject root, string objectName)
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == objectName)
                    return text;
            }

            return null;
        }

        private static void SetObjectReference(
            UnityEngine.Object owner,
            string propertyName,
            UnityEngine.Object value
        )
        {
            SerializedObject serialized = new SerializedObject(owner);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
                return;

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int CountLegacyTextAssets()
        {
            int count = 0;
            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

            try
            {
                string[] prefabGuids = AssetDatabase.FindAssets(
                    "t:Prefab",
                    new[] { ProjectRoot }
                );
                foreach (string guid in prefabGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject root = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        count += root.GetComponentsInChildren<Text>(true).Length;
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }

                string[] sceneGuids = AssetDatabase.FindAssets(
                    "t:Scene",
                    new[] { ProjectRoot }
                );
                foreach (string guid in sceneGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    foreach (GameObject root in scene.GetRootGameObjects())
                        count += root.GetComponentsInChildren<Text>(true).Length;
                }
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }

            return count;
        }
    }
}
