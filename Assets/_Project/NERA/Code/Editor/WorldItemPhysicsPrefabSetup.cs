using NERA.Items;
using UnityEditor;
using UnityEngine;

namespace NERA.EditorTools
{
    public static class WorldItemPhysicsPrefabSetup
    {
        private const string PrefabRoot = "Assets/_Project/NERA";
        private const string MenuPath =
            "NERA/Items/Apply WorldItem Rigidbody Defaults";

        [MenuItem(MenuPath)]
        public static void Apply()
        {
            string[] prefabGuids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { PrefabRoot });
            int worldItemPrefabCount = 0;
            int worldItemCount = 0;
            int addedBodyCount = 0;
            int changedPrefabCount = 0;

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;

                try
                {
                    WorldItem[] items =
                        root.GetComponentsInChildren<WorldItem>(true);
                    if (items.Length == 0)
                        continue;

                    worldItemPrefabCount++;
                    worldItemCount += items.Length;

                    foreach (WorldItem item in items)
                    {
                        Rigidbody body = item.GetComponent<Rigidbody>();
                        if (body == null)
                        {
                            body = item.gameObject.AddComponent<Rigidbody>();
                            addedBodyCount++;
                            changed = true;
                        }

                        if (body.useGravity)
                        {
                            body.useGravity = false;
                            changed = true;
                        }

                        if (!body.isKinematic)
                        {
                            body.isKinematic = true;
                            changed = true;
                        }
                    }

                    if (!changed)
                        continue;

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changedPrefabCount++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "WorldItem physics setup complete. " +
                $"Prefabs: {worldItemPrefabCount}, " +
                $"items: {worldItemCount}, " +
                $"Rigidbody added: {addedBodyCount}, " +
                $"prefabs changed: {changedPrefabCount}.");
        }
    }
}
