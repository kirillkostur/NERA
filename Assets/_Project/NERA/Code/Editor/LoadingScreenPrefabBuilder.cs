using System;
using NERA.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NERA.EditorTools
{
    public static class LoadingScreenPrefabBuilder
    {
        public const string ConfigPath =
            "Assets/_Project/NERA/Configs/UI/CFG_LoadingScreen.asset";
        public const string PrefabPath =
            "Assets/_Project/NERA/Resources/UI/P_LoadingScreen.prefab";

        private const string ImageFolder =
            "Assets/_Project/NERA/Content/UI/LoadImage";

        private static readonly TipSeed[] TipSeeds =
        {
            new TipSeed(
                "loading.tip.station_power",
                "Watch the battery charge: station systems stop working without power."),
            new TipSeed(
                "loading.tip.sandstorm",
                "Clean equipment after sandstorms to preserve its efficiency."),
            new TipSeed(
                "loading.tip.antenna",
                "Upgrade the antenna to discover more distant unknown signals."),
            new TipSeed(
                "loading.tip.expedition",
                "Check your gear before an expedition—the return trip may be harder.")
        };

        [InitializeOnLoadMethod]
        private static void RebuildMissingAssetsAfterCompilation()
        {
            EditorApplication.delayCall += () =>
            {
                bool prefabExists =
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
                bool configExists = AssetDatabase.LoadAssetAtPath<
                    LoadingScreenConfig>(ConfigPath) != null;
                if (EditorApplication.isPlayingOrWillChangePlaymode ||
                    (prefabExists && configExists))
                {
                    return;
                }

                Rebuild();
            };
        }

        [MenuItem("NERA/UI/Rebuild Loading Screen")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Loading screen cannot be rebuilt in Play Mode.");
                return;
            }

            EnsureFolder("Assets/_Project/NERA/Configs/UI");
            EnsureFolder("Assets/_Project/NERA/Resources/UI");
            LoadingScreenConfig config = BuildConfig();
            GameObject root = BuildPrefabContents(config);
            try
            {
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null)
                    throw new InvalidOperationException("Prefab save failed.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Loading screen rebuilt: {PrefabPath}. " +
                $"Images: {config.Images.Count}; tips: {config.Tips.Count}.");
        }

        private static LoadingScreenConfig BuildConfig()
        {
            LoadingScreenConfig config =
                AssetDatabase.LoadAssetAtPath<LoadingScreenConfig>(ConfigPath);
            if (config != null)
                return config;

            config = ScriptableObject.CreateInstance<LoadingScreenConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);

            string[] imageGuids = AssetDatabase.FindAssets(
                "t:Texture2D",
                new[] { ImageFolder });
            Array.Sort(imageGuids, StringComparer.Ordinal);

            SerializedObject serialized = new SerializedObject(config);
            serialized.FindProperty("minimumDisplaySeconds").floatValue = 3f;

            SerializedProperty images = serialized.FindProperty("images");
            images.arraySize = imageGuids.Length;
            for (int index = 0; index < imageGuids.Length; index++)
            {
                string path = AssetDatabase.GUIDToAssetPath(imageGuids[index]);
                images.GetArrayElementAtIndex(index).objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            SerializedProperty tips = serialized.FindProperty("tips");
            tips.arraySize = TipSeeds.Length;
            for (int index = 0; index < TipSeeds.Length; index++)
            {
                SerializedProperty tip = tips.GetArrayElementAtIndex(index);
                tip.FindPropertyRelative("localizationKey").stringValue =
                    TipSeeds[index].LocalizationKey;
                tip.FindPropertyRelative("fallbackText").stringValue =
                    TipSeeds[index].FallbackText;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            return config;
        }

        private static GameObject BuildPrefabContents(
            LoadingScreenConfig config)
        {
            GameObject root = new GameObject(
                "P_LoadingScreen",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster),
                typeof(LoadingScreenController));
            Stretch((RectTransform)root.transform);

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject cameraObject = new GameObject(
                "LoadingCamera",
                typeof(Camera),
                typeof(UniversalAdditionalCameraData));
            cameraObject.transform.SetParent(root.transform, false);
            Camera loadingCamera = cameraObject.GetComponent<Camera>();
            loadingCamera.clearFlags = CameraClearFlags.SolidColor;
            loadingCamera.backgroundColor = Color.black;
            loadingCamera.cullingMask = 0;
            loadingCamera.depth = -100f;
            loadingCamera.allowHDR = false;
            loadingCamera.allowMSAA = false;
            loadingCamera.useOcclusionCulling = false;
            loadingCamera.enabled = false;

            GameObject window = CreateImage(
                root.transform,
                "LoadingWindow",
                Color.black,
                true);
            Stretch((RectTransform)window.transform);
            window.AddComponent<RectMask2D>();

            GameObject artObject = new GameObject(
                "LoadImage",
                typeof(RectTransform),
                typeof(RawImage),
                typeof(AspectRatioFitter));
            artObject.transform.SetParent(window.transform, false);
            Stretch((RectTransform)artObject.transform);
            RawImage art = artObject.GetComponent<RawImage>();
            art.color = Color.white;
            art.raycastTarget = false;
            AspectRatioFitter aspect = artObject.GetComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = 16f / 9f;

            GameObject shade = CreateImage(
                window.transform,
                "BottomShade",
                new Color(0f, 0f, 0f, 0.62f),
                false);
            RectTransform shadeRect = (RectTransform)shade.transform;
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = new Vector2(1f, 0.19f);
            shadeRect.offsetMin = Vector2.zero;
            shadeRect.offsetMax = Vector2.zero;

            TextMeshProUGUI tip = CreateText(
                window.transform,
                "TipText",
                "Следите за зарядом батареи: без энергии системы станции перестанут работать.",
                32f,
                TextAlignmentOptions.BottomLeft);
            RectTransform tipRect = tip.rectTransform;
            tipRect.anchorMin = new Vector2(0.04f, 0.035f);
            tipRect.anchorMax = new Vector2(0.76f, 0.16f);
            tipRect.offsetMin = Vector2.zero;
            tipRect.offsetMax = Vector2.zero;

            TextMeshProUGUI status = CreateText(
                window.transform,
                "LoadingText",
                "Загрузка...",
                38f,
                TextAlignmentOptions.BottomRight);
            status.gameObject.AddComponent<SequentialEllipsisText>();
            RectTransform statusRect = status.rectTransform;
            statusRect.anchorMin = new Vector2(0.77f, 0.035f);
            statusRect.anchorMax = new Vector2(0.97f, 0.16f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;

            SerializedObject controller = new SerializedObject(
                root.GetComponent<LoadingScreenController>());
            controller.FindProperty("config").objectReferenceValue = config;
            controller.FindProperty("loadingCamera").objectReferenceValue =
                loadingCamera;
            controller.FindProperty("windowRoot").objectReferenceValue = window;
            controller.FindProperty("loadingImage").objectReferenceValue = art;
            controller.FindProperty("imageAspectRatio").objectReferenceValue =
                aspect;
            controller.FindProperty("tipText").objectReferenceValue = tip;
            controller.FindProperty("loadingText").objectReferenceValue = status;
            controller.ApplyModifiedPropertiesWithoutUndo();

            window.SetActive(false);
            return root;
        }

        private static GameObject CreateImage(
            Transform parent,
            string name,
            Color color,
            bool raycastTarget)
        {
            GameObject created = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image));
            created.transform.SetParent(parent, false);
            Image image = created.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return created;
        }

        private static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string value,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject created = new GameObject(
                name,
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            created.transform.SetParent(parent, false);
            TextMeshProUGUI text = created.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }

        private readonly struct TipSeed
        {
            public TipSeed(string localizationKey, string fallbackText)
            {
                LocalizationKey = localizationKey;
                FallbackText = fallbackText;
            }

            public string LocalizationKey { get; }
            public string FallbackText { get; }
        }
    }
}
