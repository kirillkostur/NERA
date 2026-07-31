using System;
using System.Collections.Generic;
using System.Linq;
using NERA.Quests;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NERA.Editor
{
    [InitializeOnLoad]
    internal sealed class QuestCatalogSynchronizer : AssetPostprocessor,
        IPreprocessBuildWithReport
    {
        internal const string CatalogPath =
            "Assets/_Project/NERA/Resources/Quests/" +
            "QuestCatalog_Default.asset";
        private static bool syncQueued;
        private static bool syncing;

        static QuestCatalogSynchronizer()
        {
            QueueSync();
        }

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            Synchronize(true);
        }

        [MenuItem("NERA/Quests/Sync Quest Catalog")]
        internal static void SynchronizeFromMenu()
        {
            Synchronize(false);
        }

        internal static void Synchronize(bool failOnErrors)
        {
            if (syncing)
                return;

            syncing = true;
            try
            {
                QuestCatalog catalog =
                    AssetDatabase.LoadAssetAtPath<QuestCatalog>(CatalogPath);
                if (catalog == null)
                {
                    string message = $"Quest catalog missing at " +
                        $"'{CatalogPath}'.";
                    if (failOnErrors)
                        throw new BuildFailedException(message);
                    Debug.LogError(message);
                    return;
                }

                List<QuestDefinition> definitions = AssetDatabase
                    .FindAssets("t:QuestDefinition")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<QuestDefinition>)
                    .Where(definition => definition != null)
                    .OrderBy(
                        definition => definition.QuestId,
                        StringComparer.Ordinal)
                    .ThenBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
                    .ToList();

                string[] duplicateIds = definitions
                    .Where(definition =>
                        !string.IsNullOrWhiteSpace(definition.QuestId))
                    .GroupBy(
                        definition => definition.QuestId,
                        StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToArray();
                if (duplicateIds.Length > 0)
                {
                    string message = "Duplicate Quest ID values: " +
                        string.Join(", ", duplicateIds);
                    if (failOnErrors)
                        throw new BuildFailedException(message);
                    Debug.LogError(message, catalog);
                }

                SerializedObject serialized = new SerializedObject(catalog);
                SerializedProperty entries =
                    serialized.FindProperty("definitions");
                entries.arraySize = definitions.Count;
                for (int index = 0; index < definitions.Count; index++)
                {
                    entries.GetArrayElementAtIndex(index)
                        .objectReferenceValue = definitions[index];
                }

                if (serialized.ApplyModifiedPropertiesWithoutUndo())
                {
                    EditorUtility.SetDirty(catalog);
                    AssetDatabase.SaveAssets();
                }
            }
            finally
            {
                syncing = false;
            }
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (ContainsAsset(importedAssets) ||
                ContainsAsset(deletedAssets) ||
                ContainsAsset(movedAssets) ||
                ContainsAsset(movedFromAssetPaths))
            {
                QueueSync();
            }
        }

        private static bool ContainsAsset(IEnumerable<string> paths)
        {
            return paths != null && paths.Any(path =>
                path.StartsWith(
                    "Assets/_Project/NERA/Configs/Quests/",
                    StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase));
        }

        private static void QueueSync()
        {
            if (syncQueued)
                return;

            syncQueued = true;
            EditorApplication.delayCall += () =>
            {
                syncQueued = false;
                Synchronize(false);
            };
        }
    }
}
