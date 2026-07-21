using System;
using System.Collections.Generic;
using System.Linq;
using NERA.Items;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace NERA.Editor
{
    [InitializeOnLoad]
    internal sealed class ItemCatalogSynchronizer : AssetPostprocessor, IPreprocessBuildWithReport
    {
        private const string CatalogPath =
            "Assets/_Project/NERA/Resources/ItemCatalog_Default.asset";
        private static bool syncQueued;
        private static bool syncing;

        static ItemCatalogSynchronizer()
        {
            QueueSync();
        }

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            Synchronize(true);
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths
        )
        {
            if (ContainsItemAsset(importedAssets) ||
                ContainsItemAsset(deletedAssets) ||
                ContainsItemAsset(movedAssets) ||
                ContainsItemAsset(movedFromAssetPaths))
            {
                QueueSync();
            }
        }

        private static bool ContainsItemAsset(IEnumerable<string> paths)
        {
            return paths != null && paths.Any(path =>
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

        private static void Synchronize(bool failOnDuplicateIds)
        {
            if (syncing)
                return;

            syncing = true;
            try
            {
                ItemCatalogData catalog =
                    AssetDatabase.LoadAssetAtPath<ItemCatalogData>(CatalogPath);
                if (catalog == null)
                    throw new BuildFailedException($"Item catalog missing at '{CatalogPath}'.");

                List<ItemData> items = AssetDatabase.FindAssets("t:ItemData")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<ItemData>)
                    .Where(item => item != null)
                    .OrderBy(item => item.ItemId, StringComparer.Ordinal)
                    .ThenBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal)
                    .ToList();

                string[] duplicateIds = items
                    .Where(item => !string.IsNullOrWhiteSpace(item.ItemId))
                    .GroupBy(item => item.ItemId, StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .ToArray();

                if (duplicateIds.Length > 0)
                {
                    string message =
                        $"Duplicate Item Id values: {string.Join(", ", duplicateIds)}";
                    if (failOnDuplicateIds)
                        throw new BuildFailedException(message);
                    Debug.LogError(message, catalog);
                }

                SerializedObject serialized = new SerializedObject(catalog);
                SerializedProperty entries = serialized.FindProperty("items");
                entries.arraySize = items.Count;
                for (int i = 0; i < items.Count; i++)
                {
                    entries.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
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
    }
}
