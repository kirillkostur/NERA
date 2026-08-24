using System.Collections.Generic;
using NERA.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NERA.Editor
{
    public static class QuestHUDPrefabSetup
    {
        private const string MainScenePath =
            "Assets/_Project/NERA/Scenes/MainScene.unity";
        private const string UiFolder =
            "Assets/_Project/NERA/Prefabs/UI";
        private const string QuestFolder = UiFolder + "/Quest";
        private const string TitlePrefabPath =
            QuestFolder + "/P_QuestTitle_Text.prefab";
        private const string ObjectivePrefabPath =
            QuestFolder + "/P_Quest_Text.prefab";
        private const string GroupPrefabPath =
            QuestFolder + "/P_QuestGroupView.prefab";

        [MenuItem("NERA/Quests/Rebuild HUD Prefabs")]
        public static void Rebuild()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != MainScenePath)
            {
                Debug.LogError(
                    "Open MainScene before rebuilding the quest HUD prefabs.");
                return;
            }

            EnsureFolder();
            GameObject titleSource = FindSceneObject(
                scene,
                "Text - QuestTitle") ??
                AssetDatabase.LoadAssetAtPath<GameObject>(TitlePrefabPath);
            GameObject objectiveSource = FindSceneObject(
                scene,
                "Text - Quest") ??
                AssetDatabase.LoadAssetAtPath<GameObject>(ObjectivePrefabPath);
            GameObject mainBackground = FindSceneObject(
                scene,
                "background_QuestMain");
            GameObject sideBackground = FindSceneObject(
                scene,
                "background_QuestSide");
            QuestHUDController controller = Object
                .FindFirstObjectByType<QuestHUDController>(
                    FindObjectsInactive.Include);

            if (titleSource == null || objectiveSource == null ||
                mainBackground == null || sideBackground == null ||
                controller == null)
            {
                Debug.LogError(
                    "Quest HUD migration failed: required reference objects " +
                    "were not found.");
                return;
            }

            GameObject titlePrefab = BuildTitlePrefab(titleSource);
            GameObject objectivePrefab = BuildObjectivePrefab(objectiveSource);
            GameObject groupPrefab = BuildGroupPrefab(titlePrefab);
            if (titlePrefab == null || objectivePrefab == null ||
                groupPrefab == null)
            {
                Debug.LogError("Quest HUD prefab creation failed.");
                return;
            }

            RectTransform mainContent = RebuildBackground(mainBackground);
            RectTransform sideContent = RebuildBackground(sideBackground);
            controller.ConfigureView(
                mainBackground,
                mainContent,
                sideBackground,
                sideContent,
                groupPrefab.GetComponent<QuestGroupView>(),
                objectivePrefab.GetComponent<QuestObjectiveView>());

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "Quest HUD prefabs rebuilt and MainScene migrated successfully.");
        }

        private static GameObject BuildTitlePrefab(GameObject source)
        {
            GameObject temporary = Object.Instantiate(source);
            temporary.name = "QuestTitle_Text";
            temporary.transform.SetParent(null, false);
            ConfigureTextRect(temporary);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                temporary,
                TitlePrefabPath);
            Object.DestroyImmediate(temporary);
            return prefab;
        }

        private static GameObject BuildObjectivePrefab(GameObject source)
        {
            GameObject temporary = Object.Instantiate(source);
            temporary.name = "Quest_Text";
            temporary.transform.SetParent(null, false);
            TMP_Text label = ConfigureTextRect(temporary);
            QuestObjectiveView view =
                temporary.GetComponent<QuestObjectiveView>() ??
                temporary.AddComponent<QuestObjectiveView>();
            view.ConfigureTemplate(label);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                temporary,
                ObjectivePrefabPath);
            Object.DestroyImmediate(temporary);
            return prefab;
        }

        private static GameObject BuildGroupPrefab(GameObject titlePrefab)
        {
            GameObject temporary = new GameObject(
                "QuestGroupView",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter),
                typeof(LayoutElement),
                typeof(QuestGroupView));
            RectTransform rect = (RectTransform)temporary.transform;
            rect.sizeDelta = new Vector2(400f, 60f);
            ConfigureVerticalLayout(
                temporary.GetComponent<VerticalLayoutGroup>(),
                0f);
            ConfigureFitter(temporary.GetComponent<ContentSizeFitter>());

            GameObject title = PrefabUtility.InstantiatePrefab(titlePrefab)
                as GameObject;
            title.name = "QuestTitle_Text";
            title.transform.SetParent(temporary.transform, false);

            GameObject objectives = new GameObject(
                "Objectives",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            RectTransform objectivesRect =
                (RectTransform)objectives.transform;
            objectivesRect.SetParent(temporary.transform, false);
            ConfigureTopStretch(objectivesRect);
            ConfigureVerticalLayout(
                objectives.GetComponent<VerticalLayoutGroup>(),
                0f);
            ConfigureFitter(objectives.GetComponent<ContentSizeFitter>());

            temporary.GetComponent<QuestGroupView>().ConfigureTemplate(
                title.GetComponent<TMP_Text>(),
                objectivesRect);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                temporary,
                GroupPrefabPath);
            Object.DestroyImmediate(temporary);
            return prefab;
        }

        private static RectTransform RebuildBackground(
            GameObject background)
        {
            List<GameObject> children = new List<GameObject>();
            for (int index = 0;
                 index < background.transform.childCount;
                 index++)
            {
                children.Add(
                    background.transform.GetChild(index).gameObject);
            }
            for (int index = 0; index < children.Count; index++)
                Object.DestroyImmediate(children[index]);

            GameObject content = new GameObject(
                "Content",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            RectTransform contentRect = (RectTransform)content.transform;
            contentRect.SetParent(background.transform, false);
            ConfigureTopStretch(contentRect);
            ConfigureVerticalLayout(
                content.GetComponent<VerticalLayoutGroup>(),
                8f);
            ConfigureFitter(content.GetComponent<ContentSizeFitter>());

            if (background.TryGetComponent(out Graphic image))
                image.raycastTarget = false;
            return contentRect;
        }

        private static TMP_Text ConfigureTextRect(GameObject target)
        {
            RectTransform rect = (RectTransform)target.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(400f, 30f);

            TMP_Text label = target.GetComponent<TMP_Text>();
            label.raycastTarget = false;
            LayoutElement layout = target.GetComponent<LayoutElement>() ??
                target.AddComponent<LayoutElement>();
            layout.minHeight = 30f;
            layout.preferredHeight = 30f;
            layout.flexibleHeight = 0f;
            return label;
        }

        private static void ConfigureTopStretch(RectTransform rect)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void ConfigureVerticalLayout(
            VerticalLayoutGroup layout,
            float spacing)
        {
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static void ConfigureFitter(ContentSizeFitter fitter)
        {
            fitter.horizontalFit =
                ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static GameObject FindSceneObject(
            Scene scene,
            string objectName)
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform current = transforms[index];
                if (current.gameObject.scene == scene &&
                    current.name == objectName)
                {
                    return current.gameObject;
                }
            }
            return null;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(QuestFolder))
                AssetDatabase.CreateFolder(UiFolder, "Quest");
        }
    }
}
