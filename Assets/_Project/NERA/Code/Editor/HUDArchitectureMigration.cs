using System;
using System.Collections.Generic;
using NERA.Inventory;
using NERA.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NERA.Editor
{
    public static class HUDArchitectureMigration
    {
        private const string MainScenePath =
            "Assets/_Project/NERA/Scenes/MainScene.unity";
        private const string HudPrefabPath =
            "Assets/_Project/NERA/Prefabs/UI/P_HUD_Canvas.prefab";
        private const string HudFolder =
            "Assets/_Project/NERA/Prefabs/UI/HUD";
        private const string ScreenFolder =
            "Assets/_Project/NERA/Prefabs/UI/Screens";

        private const string DynamicLayerName = "DynamicHUDCanvas";
        private const string GameplayLayerName = "GameplayHUDCanvas";
        private const string ModalLayerName = "ModalCanvas";

        private sealed class BlockSpec
        {
            public BlockSpec(
                string objectName,
                string prefabPath,
                string layerName)
            {
                ObjectName = objectName;
                PrefabPath = prefabPath;
                LayerName = layerName;
            }

            public string ObjectName { get; }
            public string PrefabPath { get; }
            public string LayerName { get; }
        }

        private static readonly BlockSpec[] Blocks =
        {
            new BlockSpec(
                "InventoryHint",
                HudFolder + "/P_HUD_InventoryHint.prefab",
                GameplayLayerName),
            new BlockSpec(
                "Quest_System",
                HudFolder + "/P_HUD_QuestTracker.prefab",
                GameplayLayerName),
            new BlockSpec(
                "CheckpointIndicator",
                HudFolder + "/P_HUD_Checkpoint.prefab",
                GameplayLayerName),
            new BlockSpec(
                "InventoryScreen",
                ScreenFolder + "/P_Screen_Inventory.prefab",
                ModalLayerName),
            new BlockSpec(
                "LaboratoryScreen",
                ScreenFolder + "/P_Screen_Laboratory.prefab",
                ModalLayerName),
            new BlockSpec(
                "TerminalScreen",
                ScreenFolder + "/P_Screen_Terminal.prefab",
                ModalLayerName),
            new BlockSpec(
                "UpgradeScreen",
                ScreenFolder + "/P_Screen_StationUpgrade.prefab",
                ModalLayerName)
        };

        [MenuItem("Tools/NERA/Migrate HUD Architecture")]
        public static void Migrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(
                    "HUD migration is available only outside Play Mode.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(MainScenePath);
            bool openedAdditively = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedAdditively)
                {
                    scene = EditorSceneManager.OpenScene(
                        MainScenePath,
                        OpenSceneMode.Additive);
                }

                GameObject sceneHud = FindSceneObject(scene, "HUD_Canvas");
                if (sceneHud == null)
                {
                    Debug.LogError(
                        "HUD migration failed: MainScene/HUD_Canvas " +
                        "was not found.");
                    return;
                }

                Dictionary<string, GameObject> sources =
                    CollectSceneSources(sceneHud);
                bool sceneAlreadyMigrated =
                    FindDirectChild(sceneHud.transform, DynamicLayerName) !=
                    null &&
                    sources.Count == 0;

                if (!sceneAlreadyMigrated && sources.Count != Blocks.Length)
                {
                    Debug.LogError(
                        "HUD migration stopped before changing assets: " +
                        "one or more expected direct HUD blocks are missing.");
                    return;
                }

                EnsureFolder(HudFolder);
                EnsureFolder(ScreenFolder);

                if (!sceneAlreadyMigrated)
                {
                    foreach (BlockSpec block in Blocks)
                    {
                        SaveBlockPrefab(
                            sources[block.ObjectName],
                            block.PrefabPath);
                    }
                }
                ConfigureBlockPrefabLayers();

                Canvas sceneCanvas = sceneHud.GetComponent<Canvas>();
                AdditionalCanvasShaderChannels shaderChannels =
                    sceneCanvas != null
                        ? sceneCanvas.additionalShaderChannels
                        : AdditionalCanvasShaderChannels.None;
                RebuildHudPrefab(shaderChannels);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (!sceneAlreadyMigrated)
                    RemoveLegacySceneOverrides(sceneHud);

                EditorUtility.SetDirty(sceneHud);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();

                Debug.Log(
                    "HUD architecture migrated: reusable HUD/screen prefabs, " +
                    "three Canvas layers, responsive anchors and " +
                    "CanvasScaler configuration are now applied.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (openedAdditively && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Dictionary<string, GameObject> CollectSceneSources(
            GameObject sceneHud)
        {
            Dictionary<string, GameObject> result =
                new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (BlockSpec block in Blocks)
            {
                Transform child = FindDirectChild(
                    sceneHud.transform,
                    block.ObjectName);
                if (child != null)
                    result.Add(block.ObjectName, child.gameObject);
            }

            return result;
        }

        private static void SaveBlockPrefab(
            GameObject source,
            string prefabPath)
        {
            GameObject temporary = Object.Instantiate(source);
            temporary.name = source.name;
            temporary.transform.SetParent(null, false);
            try
            {
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    temporary,
                    prefabPath,
                    out bool success);
                if (!success || prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Failed to save HUD block prefab: {prefabPath}");
                }
            }
            finally
            {
                Object.DestroyImmediate(temporary);
            }
        }

        private static void RebuildHudPrefab(
            AdditionalCanvasShaderChannels shaderChannels)
        {
            GameObject root =
                PrefabUtility.LoadPrefabContents(HudPrefabPath);
            try
            {
                ConfigureRoot(root, shaderChannels);

                RectTransform dynamicLayer = GetOrCreateCanvasLayer(
                    root,
                    DynamicLayerName,
                    10,
                    false,
                    shaderChannels);
                RectTransform gameplayLayer = GetOrCreateCanvasLayer(
                    root,
                    GameplayLayerName,
                    20,
                    false,
                    shaderChannels);
                RectTransform modalLayer = GetOrCreateCanvasLayer(
                    root,
                    ModalLayerName,
                    100,
                    true,
                    shaderChannels);

                MoveInteractionPrompt(root, gameplayLayer);
                MoveCompassController(root, dynamicLayer);

                Dictionary<string, RectTransform> layers =
                    new Dictionary<string, RectTransform>
                    {
                        { DynamicLayerName, dynamicLayer },
                        { GameplayLayerName, gameplayLayer },
                        { ModalLayerName, modalLayer }
                    };

                Dictionary<string, GameObject> instances =
                    new Dictionary<string, GameObject>(
                        StringComparer.Ordinal);
                foreach (BlockSpec block in Blocks)
                {
                    RectTransform parent = layers[block.LayerName];
                    Transform existing = FindNestedPrefabInstance(
                        parent,
                        block);
                    if (existing != null)
                        Object.DestroyImmediate(existing.gameObject);

                    GameObject instance = InstantiateNestedPrefab(
                        block.PrefabPath,
                        parent);
                    instance.name = block.ObjectName;
                    NormalizeBlock(instance, block.ObjectName);
                    instances[block.ObjectName] = instance;
                }

                ConfigureInventoryController(
                    root,
                    instances,
                    dynamicLayer.gameObject,
                    instances["Quest_System"]);

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    HudPrefabPath,
                    out bool success);
                if (!success || saved == null)
                {
                    throw new InvalidOperationException(
                        "Failed to save the rebuilt HUD root prefab.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ConfigureRoot(
            GameObject root,
            AdditionalCanvasShaderChannels shaderChannels)
        {
            root.layer = LayerMask.NameToLayer("UI") >= 0
                ? LayerMask.NameToLayer("UI")
                : root.layer;

            RectTransform rootRect = root.transform as RectTransform;
            ConfigureStretch(rootRect);

            Canvas canvas = root.GetComponent<Canvas>() ??
                root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.additionalShaderChannels = shaderChannels;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>() ??
                root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (root.GetComponent<ResponsiveCanvasLayout>() == null)
                root.AddComponent<ResponsiveCanvasLayout>();

            GraphicRaycaster[] raycasters =
                root.GetComponents<GraphicRaycaster>();
            foreach (GraphicRaycaster raycaster in raycasters)
                Object.DestroyImmediate(raycaster);
        }

        private static RectTransform GetOrCreateCanvasLayer(
            GameObject root,
            string layerName,
            int sortingOrder,
            bool requiresRaycaster,
            AdditionalCanvasShaderChannels shaderChannels)
        {
            Transform existing = FindDirectChild(root.transform, layerName);
            GameObject layer = existing != null
                ? existing.gameObject
                : new GameObject(
                    layerName,
                    typeof(RectTransform),
                    typeof(Canvas));

            RectTransform rect = layer.transform as RectTransform;
            if (rect.parent != root.transform)
                rect.SetParent(root.transform, false);
            ConfigureStretch(rect);
            SetLayerRecursively(layer, root.layer);

            Canvas canvas = layer.GetComponent<Canvas>() ??
                layer.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            canvas.additionalShaderChannels = shaderChannels;

            GraphicRaycaster raycaster =
                layer.GetComponent<GraphicRaycaster>();
            if (requiresRaycaster && raycaster == null)
                layer.AddComponent<GraphicRaycaster>();
            else if (!requiresRaycaster && raycaster != null)
                Object.DestroyImmediate(raycaster);

            return rect;
        }

        private static void MoveInteractionPrompt(
            GameObject root,
            RectTransform gameplayLayer)
        {
            Transform prompt = FindDescendant(
                root.transform,
                "InteractionPrompt");
            if (prompt == null)
                return;

            if (prompt.parent != gameplayLayer)
                prompt.SetParent(gameplayLayer, false);
            SetLayerRecursively(
                prompt.gameObject,
                gameplayLayer.gameObject.layer);

            ConfigureFixed(
                prompt as RectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0.5f),
                new Vector2(460f, 52f),
                new Vector2(0f, 238f));
        }

        private static void MoveCompassController(
            GameObject root,
            RectTransform dynamicLayer)
        {
            QuestCompassHUDController rootController =
                root.GetComponent<QuestCompassHUDController>();
            QuestCompassHUDController layerController =
                dynamicLayer.GetComponent<QuestCompassHUDController>();

            if (layerController == null)
            {
                layerController = dynamicLayer.gameObject
                    .AddComponent<QuestCompassHUDController>();
                if (rootController != null)
                    EditorUtility.CopySerialized(
                        rootController,
                        layerController);
            }

            if (rootController != null)
                Object.DestroyImmediate(rootController);
        }

        private static GameObject InstantiateNestedPrefab(
            string prefabPath,
            Transform parent)
        {
            GameObject asset =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"HUD prefab is missing: {prefabPath}");
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(asset, parent) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"Could not instantiate nested prefab: {prefabPath}");
            }

            return instance;
        }

        private static void ConfigureBlockPrefabLayers()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer < 0)
                return;

            foreach (BlockSpec block in Blocks)
            {
                GameObject contents =
                    PrefabUtility.LoadPrefabContents(block.PrefabPath);
                try
                {
                    SetLayerRecursively(contents, uiLayer);
                    GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                        contents,
                        block.PrefabPath,
                        out bool success);
                    if (!success || saved == null)
                    {
                        throw new InvalidOperationException(
                            "Failed to configure the UI layer for: " +
                            block.PrefabPath);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        private static Transform FindNestedPrefabInstance(
            Transform parent,
            BlockSpec block)
        {
            Transform named = FindDirectChild(parent, block.ObjectName);
            if (named != null)
                return named;

            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                string assetPath =
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                        child.gameObject);
                if (assetPath == block.PrefabPath)
                    return child;
            }

            return null;
        }

        private static void NormalizeBlock(
            GameObject block,
            string objectName)
        {
            RectTransform rect = block.transform as RectTransform;
            switch (objectName)
            {
                case "InventoryHint":
                    ConfigureFixed(
                        rect,
                        Vector2.right,
                        Vector2.right,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(300f, 70f),
                        new Vector2(-150f, 35f));
                    break;

                case "CheckpointIndicator":
                    ConfigureFixed(
                        rect,
                        Vector2.zero,
                        Vector2.zero,
                        new Vector2(0.5f, 0.5f),
                        new Vector2(300f, 70f),
                        new Vector2(150f, 35f));
                    break;

                case "Quest_System":
                    ConfigureStretch(rect);
                    ConfigureQuestPanel(
                        block.transform,
                        "background_QuestMain",
                        new Vector2(-200f, -100f));
                    ConfigureQuestPanel(
                        block.transform,
                        "background_QuestSide",
                        new Vector2(-200f, -300f));
                    break;

                default:
                    ConfigureStretch(rect);
                    break;
            }
        }

        private static void ConfigureQuestPanel(
            Transform root,
            string panelName,
            Vector2 anchoredPosition)
        {
            RectTransform panel = FindDescendant(root, panelName)
                as RectTransform;
            ConfigureFixed(
                panel,
                Vector2.one,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                new Vector2(400f, 200f),
                anchoredPosition);
        }

        private static void ConfigureInventoryController(
            GameObject root,
            IReadOnlyDictionary<string, GameObject> instances,
            GameObject dynamicHudLayer,
            GameObject questTrackerHud)
        {
            InventoryLabHUDController controller =
                root.GetComponent<InventoryLabHUDController>() ??
                root.AddComponent<InventoryLabHUDController>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("dynamicHudLayer").objectReferenceValue =
                dynamicHudLayer;
            serialized.FindProperty("questTrackerHud").objectReferenceValue =
                questTrackerHud;
            serialized.FindProperty("inventoryPanel").objectReferenceValue =
                instances["InventoryScreen"];
            serialized.FindProperty("laboratoryPanel").objectReferenceValue =
                instances["LaboratoryScreen"];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveLegacySceneOverrides(GameObject sceneHud)
        {
            foreach (BlockSpec block in Blocks)
            {
                Transform direct = FindDirectChild(
                    sceneHud.transform,
                    block.ObjectName);
                if (direct != null)
                    Object.DestroyImmediate(direct.gameObject);
            }

            InventoryLabHUDController[] controllers =
                sceneHud.GetComponents<InventoryLabHUDController>();
            foreach (InventoryLabHUDController controller in controllers)
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(
                    controller) == null)
                {
                    Object.DestroyImmediate(controller);
                }
            }
        }

        private static void ConfigureStretch(RectTransform rect)
        {
            if (rect == null)
                return;

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            Vector3 position = rect.localPosition;
            position.z = 0f;
            rect.localPosition = position;
        }

        private static void ConfigureFixed(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 size,
            Vector2 anchoredPosition)
        {
            if (rect == null)
                return;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            Vector3 position = rect.localPosition;
            position.z = 0f;
            rect.localPosition = position;
        }

        private static void SetLayerRecursively(
            GameObject root,
            int layer)
        {
            root.layer = layer;
            for (int index = 0;
                 index < root.transform.childCount;
                 index++)
            {
                SetLayerRecursively(
                    root.transform.GetChild(index).gameObject,
                    layer);
            }
        }

        private static Transform FindDirectChild(
            Transform parent,
            string childName)
        {
            if (parent == null)
                return null;

            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == childName)
                    return child;
            }

            return null;
        }

        private static Transform FindDescendant(
            Transform root,
            string objectName)
        {
            if (root == null)
                return null;
            if (root.name == objectName)
                return root;

            for (int index = 0; index < root.childCount; index++)
            {
                Transform result = FindDescendant(
                    root.GetChild(index),
                    objectName);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static GameObject FindSceneObject(
            Scene scene,
            string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform result = FindDescendant(
                    root.transform,
                    objectName);
                if (result != null)
                    return result.gameObject;
            }

            return null;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] parts = folderPath.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
