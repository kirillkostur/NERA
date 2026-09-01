using NERA.Inventory;
using NERA.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Tests
{
    public sealed class HUDArchitectureTests
    {
        private const string HudPath =
            "Assets/_Project/NERA/Prefabs/UI/P_HUD_Canvas.prefab";

        private GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = PrefabUtility.LoadPrefabContents(HudPath);
            Assert.That(root, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
                PrefabUtility.UnloadPrefabContents(root);
        }

        [Test]
        public void RootUsesResponsiveScalerAndDedicatedCanvasLayers()
        {
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            Assert.That(scaler, Is.Not.Null);
            Assert.That(
                scaler.uiScaleMode,
                Is.EqualTo(CanvasScaler.ScaleMode.ScaleWithScreenSize));
            AssertVector(
                scaler.referenceResolution,
                new Vector2(1920f, 1080f));
            Assert.That(
                scaler.screenMatchMode,
                Is.EqualTo(
                    CanvasScaler.ScreenMatchMode.MatchWidthOrHeight));
            Assert.That(
                root.GetComponent<ResponsiveCanvasLayout>(),
                Is.Not.Null);
            Assert.That(
                root.GetComponent<GraphicRaycaster>(),
                Is.Null,
                "The root canvas must not rebuild raycast data.");

            RectTransform dynamicLayer = RequireDirect(
                root.transform,
                "DynamicHUDCanvas");
            RectTransform gameplayLayer = RequireDirect(
                root.transform,
                "GameplayHUDCanvas");
            RectTransform modalLayer = RequireDirect(
                root.transform,
                "ModalCanvas");

            AssertStretch(dynamicLayer);
            AssertStretch(gameplayLayer);
            AssertStretch(modalLayer);
            AssertCanvasLayer(dynamicLayer, 10, false);
            AssertCanvasLayer(gameplayLayer, 20, false);
            AssertCanvasLayer(modalLayer, 100, true);

            Assert.That(
                dynamicLayer.GetComponent<QuestCompassHUDController>(),
                Is.Not.Null);
            Assert.That(
                root.GetComponent<QuestCompassHUDController>(),
                Is.Null);
            Assert.That(
                root.GetComponent<InventoryLabHUDController>(),
                Is.Not.Null);
            SerializedObject inventoryController = new SerializedObject(
                root.GetComponent<InventoryLabHUDController>());
            Assert.That(
                inventoryController.FindProperty("dynamicHudLayer")
                    .objectReferenceValue,
                Is.EqualTo(dynamicLayer.gameObject));
            Assert.That(
                inventoryController.FindProperty("questTrackerHud")
                    .objectReferenceValue,
                Is.EqualTo(RequireDirect(
                    gameplayLayer,
                    "Quest_System").gameObject));
        }

        [Test]
        public void GameplayBlocksAreNestedPrefabsWithResponsiveAnchors()
        {
            RectTransform layer = RequireDirect(
                root.transform,
                "GameplayHUDCanvas");

            RectTransform prompt = RequireDirect(
                layer,
                "InteractionPrompt");
            AssertFixed(
                prompt,
                new Vector2(0.5f, 0f),
                new Vector2(460f, 52f),
                new Vector2(0f, 238f));

            RectTransform hint = RequireNestedPrefab(
                layer,
                "InventoryHint",
                "Assets/_Project/NERA/Prefabs/UI/HUD/" +
                "P_HUD_InventoryHint.prefab");
            AssertFixed(
                hint,
                Vector2.right,
                new Vector2(300f, 70f),
                new Vector2(-150f, 35f));

            RectTransform quickAccess = RequireNestedPrefab(
                layer,
                "Slot_Invent_Equipment",
                "Assets/_Project/NERA/Prefabs/UI/HUD/" +
                "P_HUD_QuickAccess.prefab");
            AssertFixed(
                quickAccess,
                new Vector2(0.5f, 0f),
                new Vector2(500f, 150f),
                new Vector2(0f, 88f));

            RectTransform checkpoint = RequireNestedPrefab(
                layer,
                "CheckpointIndicator",
                "Assets/_Project/NERA/Prefabs/UI/HUD/" +
                "P_HUD_Checkpoint.prefab");
            AssertFixed(
                checkpoint,
                Vector2.zero,
                new Vector2(300f, 70f),
                new Vector2(150f, 35f));

            RectTransform quest = RequireNestedPrefab(
                layer,
                "Quest_System",
                "Assets/_Project/NERA/Prefabs/UI/HUD/" +
                "P_HUD_QuestTracker.prefab");
            AssertStretch(quest);
            AssertFixed(
                RequireDescendant(
                    quest,
                    "background_QuestMain"),
                Vector2.one,
                new Vector2(400f, 200f),
                new Vector2(-200f, -100f));
            AssertFixed(
                RequireDescendant(
                    quest,
                    "background_QuestSide"),
                Vector2.one,
                new Vector2(400f, 200f),
                new Vector2(-200f, -300f));
        }

        [Test]
        public void ModalScreensAreFullStretchNestedPrefabs()
        {
            RectTransform layer = RequireDirect(
                root.transform,
                "ModalCanvas");

            AssertScreen(
                layer,
                "InventoryScreen",
                "P_Screen_Inventory.prefab");
            AssertScreen(
                layer,
                "LaboratoryScreen",
                "P_Screen_Laboratory.prefab");
            AssertScreen(
                layer,
                "TerminalScreen",
                "P_Screen_Terminal.prefab");
            AssertScreen(
                layer,
                "UpgradeScreen",
                "P_Screen_StationUpgrade.prefab");
        }

        private static void AssertScreen(
            RectTransform layer,
            string objectName,
            string assetName)
        {
            RectTransform screen = RequireNestedPrefab(
                layer,
                objectName,
                "Assets/_Project/NERA/Prefabs/UI/Screens/" +
                assetName);
            AssertStretch(screen);
        }

        private static void AssertCanvasLayer(
            RectTransform layer,
            int expectedOrder,
            bool requiresRaycaster)
        {
            Canvas canvas = layer.GetComponent<Canvas>();
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.overrideSorting, Is.True);
            Assert.That(canvas.sortingOrder, Is.EqualTo(expectedOrder));
            Assert.That(
                layer.GetComponent<GraphicRaycaster>() != null,
                Is.EqualTo(requiresRaycaster));
        }

private static RectTransform RequireNestedPrefab(
            Transform parent,
            string objectName,
            string expectedPath)
        {
            RectTransform child = RequireDirect(parent, objectName);
            Assert.That(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    child.gameObject),
                Is.EqualTo(expectedPath));

            int uiLayer = LayerMask.NameToLayer("UI");
            int stationUiLayer = LayerMask.NameToLayer("StationUI");
            Assert.That(uiLayer, Is.GreaterThanOrEqualTo(0));
            Assert.That(stationUiLayer, Is.GreaterThanOrEqualTo(0));

            foreach (Transform descendant in
                     child.GetComponentsInChildren<Transform>(true))
            {
                if (descendant is RectTransform)
                {
                    Assert.That(
                        descendant.gameObject.layer,
                        Is.EqualTo(uiLayer),
                        $"'{descendant.name}' must use the UI layer.");
                    continue;
                }

                Assert.That(
                    descendant.gameObject.layer,
                    Is.EqualTo(uiLayer).Or.EqualTo(stationUiLayer),
                    $"'{descendant.name}' must use UI or StationUI.");
            }
            return child;
        }

        private static RectTransform RequireDirect(
            Transform parent,
            string objectName)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == objectName)
                {
                    Assert.That(child, Is.TypeOf<RectTransform>());
                    return (RectTransform)child;
                }
            }

            Assert.Fail(
                $"Direct UI child '{objectName}' was not found under " +
                $"'{parent.name}'.");
            return null;
        }

        private static RectTransform RequireDescendant(
            Transform rootTransform,
            string objectName)
        {
            Transform[] children =
                rootTransform.GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name == objectName)
                {
                    Assert.That(child, Is.TypeOf<RectTransform>());
                    return (RectTransform)child;
                }
            }

            Assert.Fail(
                $"UI descendant '{objectName}' was not found.");
            return null;
        }

        private static void AssertStretch(RectTransform rect)
        {
            AssertVector(rect.anchorMin, Vector2.zero);
            AssertVector(rect.anchorMax, Vector2.one);
            AssertVector(rect.anchoredPosition, Vector2.zero);
            AssertVector(rect.sizeDelta, Vector2.zero);
            AssertVector(rect.localScale, Vector3.one);
        }

        private static void AssertFixed(
            RectTransform rect,
            Vector2 anchor,
            Vector2 size,
            Vector2 position)
        {
            AssertVector(rect.anchorMin, anchor);
            AssertVector(rect.anchorMax, anchor);
            AssertVector(rect.pivot, new Vector2(0.5f, 0.5f));
            AssertVector(rect.sizeDelta, size);
            AssertVector(rect.anchoredPosition, position);
            AssertVector(rect.localScale, Vector3.one);
        }

        private static void AssertVector(
            Vector2 actual,
            Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
        }

        private static void AssertVector(
            Vector3 actual,
            Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.01f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.01f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.01f));
        }
    }
}
