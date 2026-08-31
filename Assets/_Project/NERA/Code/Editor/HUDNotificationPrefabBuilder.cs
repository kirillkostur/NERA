using System;
using System.IO;
using NERA.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NERA.EditorTools
{
    public static class HUDNotificationPrefabBuilder
    {
        public const string CatalogPath =
            "Assets/_Project/NERA/Resources/UI/" +
            "HUDNotificationCatalog_Default.asset";
        public const string NotificationPrefabPath =
            "Assets/_Project/NERA/Resources/UI/P_HUD_Notification.prefab";
        public const string FeedPrefabPath =
            "Assets/_Project/NERA/Prefabs/UI/HUD/" +
            "P_HUD_NotificationFeed.prefab";
        public const string HudPrefabPath =
            "Assets/_Project/NERA/Prefabs/UI/P_HUD_Canvas.prefab";

        private static readonly NotificationSeed[] Seeds =
        {
            new NotificationSeed(
                HUDNotificationIds.StormStarted,
                "notification.weather.storm_started",
                HUDNotificationSeverity.Critical,
                5f),
            new NotificationSeed(
                HUDNotificationIds.StormEnded,
                "notification.weather.storm_ended",
                HUDNotificationSeverity.Success,
                4f),
            new NotificationSeed(
                HUDNotificationIds.BatteryLow,
                "notification.energy.battery_low",
                HUDNotificationSeverity.Warning,
                5f),
            new NotificationSeed(
                HUDNotificationIds.BatteryDisabled,
                "notification.energy.battery_disabled",
                HUDNotificationSeverity.Critical,
                5f),
            new NotificationSeed(
                HUDNotificationIds.BatteryEnabled,
                "notification.energy.battery_enabled",
                HUDNotificationSeverity.Success,
                4f),
            new NotificationSeed(
                HUDNotificationIds.PowerLost,
                "notification.energy.power_lost",
                HUDNotificationSeverity.Critical,
                5f),
            new NotificationSeed(
                HUDNotificationIds.PowerRestored,
                "notification.energy.power_restored",
                HUDNotificationSeverity.Success,
                4f),
            new NotificationSeed(
                HUDNotificationIds.DroneDeparted,
                "notification.drone.departed",
                HUDNotificationSeverity.Warning,
                4f),
            new NotificationSeed(
                HUDNotificationIds.DroneReturned,
                "notification.drone.returned",
                HUDNotificationSeverity.Success,
                4f),
            new NotificationSeed(
                HUDNotificationIds.DroneLocationDiscovered,
                "notification.drone.location_discovered",
                HUDNotificationSeverity.Success,
                5f),
            new NotificationSeed(
                HUDNotificationIds.DroneNoNewLocations,
                "notification.drone.no_new_locations",
                HUDNotificationSeverity.Warning,
                4f),
            new NotificationSeed(
                HUDNotificationIds.AntennaSignalFound,
                "notification.antenna.signal_found",
                HUDNotificationSeverity.Success,
                5f),
            new NotificationSeed(
                HUDNotificationIds.AntennaSignalNotFound,
                "notification.antenna.signal_not_found",
                HUDNotificationSeverity.Warning,
                4f),
            new NotificationSeed(
                HUDNotificationIds.ResearchCompleted,
                "notification.research.completed",
                HUDNotificationSeverity.Success,
                5f)
        };

        [MenuItem("NERA/UI/Setup HUD Notifications")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError(
                    "HUD notifications cannot be rebuilt in Play Mode.");
                return;
            }

            EnsureFolder("Assets/_Project/NERA/Resources/UI");
            EnsureFolder("Assets/_Project/NERA/Prefabs/UI/HUD");

            HUDNotificationCatalog catalog = BuildCatalog();
            HUDNotificationView notification = BuildNotificationPrefab();
            GameObject feed = BuildFeedPrefab(catalog, notification);
            IntegrateWithHud(feed);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"HUD notifications configured: {Seeds.Length} events, " +
                $"feed '{FeedPrefabPath}'.");
        }

        private static HUDNotificationCatalog BuildCatalog()
        {
            HUDNotificationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<HUDNotificationCatalog>(
                    CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<
                    HUDNotificationCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("entries");
            foreach (NotificationSeed seed in Seeds)
            {
                if (FindEntry(entries, seed.Id) >= 0)
                    continue;

                int index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("id").stringValue = seed.Id;
                entry.FindPropertyRelative("localizationKey").stringValue =
                    seed.LocalizationKey;
                entry.FindPropertyRelative("severity").enumValueIndex =
                    (int)seed.Severity;
                entry.FindPropertyRelative("visibleSeconds").floatValue =
                    seed.VisibleSeconds;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static int FindEntry(
            SerializedProperty entries,
            string id)
        {
            for (int index = 0; index < entries.arraySize; index++)
            {
                string existingId = entries.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("id").stringValue;
                if (string.Equals(
                        existingId,
                        id,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
            return -1;
        }

        private static HUDNotificationView BuildNotificationPrefab()
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(
                NotificationPrefabPath);
            if (existing != null)
            {
                HUDNotificationView existingView =
                    existing.GetComponent<HUDNotificationView>();
                if (existingView == null)
                {
                    throw new InvalidOperationException(
                        $"'{NotificationPrefabPath}' requires a " +
                        $"{nameof(HUDNotificationView)} component.");
                }

                return existingView;
            }

            GameObject root = new GameObject(
                "P_HUD_Notification",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(HUDNotificationView));
            SetUiLayer(root);
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.sizeDelta = new Vector2(620f, 48f);
            rootRect.anchoredPosition = new Vector2(0f, -70f);

            Image background = root.GetComponent<Image>();
            background.color = Color.white;
            background.raycastTarget = false;
            CanvasGroup canvasGroup = root.GetComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            GameObject accentObject = new GameObject(
                "Accent",
                typeof(RectTransform),
                typeof(Image));
            accentObject.transform.SetParent(root.transform, false);
            SetUiLayer(accentObject);
            RectTransform accentRect = (RectTransform)accentObject.transform;
            accentRect.anchorMin = Vector2.zero;
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.sizeDelta = new Vector2(6f, 0f);
            accentRect.anchoredPosition = Vector2.zero;
            Image accent = accentObject.GetComponent<Image>();
            accent.color = Color.white;
            accent.raycastTarget = false;

            GameObject textObject = new GameObject(
                "Message",
                typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(root.transform, false);
            SetUiLayer(textObject);
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 3f);
            textRect.offsetMax = new Vector2(-18f, -3f);
            TextMeshProUGUI message = textObject.GetComponent<
                TextMeshProUGUI>();
            message.text = string.Empty;
            message.color = Color.white;
            message.alignment = TextAlignmentOptions.MidlineLeft;
            message.textWrappingMode = TextWrappingModes.NoWrap;
            message.overflowMode = TextOverflowModes.Ellipsis;
            message.raycastTarget = false;
            SerializedObject messageSizing = new SerializedObject(message);
            messageSizing.FindProperty("m_fontSize").floatValue = 23f;
            messageSizing.FindProperty("m_enableAutoSizing").boolValue = false;
            messageSizing.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject view = new SerializedObject(
                root.GetComponent<HUDNotificationView>());
            view.FindProperty("background").objectReferenceValue = background;
            view.FindProperty("accent").objectReferenceValue = accent;
            view.FindProperty("messageText").objectReferenceValue = message;
            view.FindProperty("canvasGroup").objectReferenceValue = canvasGroup;
            view.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                root,
                NotificationPrefabPath);
            Object.DestroyImmediate(root);
            if (saved == null)
                throw new InvalidOperationException(
                    "HUD notification prefab save failed.");
            return saved.GetComponent<HUDNotificationView>();
        }

        private static GameObject BuildFeedPrefab(
            HUDNotificationCatalog catalog,
            HUDNotificationView notification)
        {
            GameObject root = new GameObject(
                "P_HUD_NotificationFeed",
                typeof(RectTransform),
                typeof(HUDNotificationController));
            SetUiLayer(root);
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = Vector2.zero;

            SerializedObject controller = new SerializedObject(
                root.GetComponent<HUDNotificationController>());
            controller.FindProperty("catalog").objectReferenceValue = catalog;
            controller.FindProperty("notificationPrefab").objectReferenceValue =
                notification;
            controller.ApplyModifiedPropertiesWithoutUndo();

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                root,
                FeedPrefabPath);
            Object.DestroyImmediate(root);
            if (saved == null)
                throw new InvalidOperationException(
                    "HUD notification feed prefab save failed.");
            return saved;
        }

        private static void IntegrateWithHud(GameObject feedPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);
            try
            {
                Transform dynamicLayer = FindDirect(
                    root.transform,
                    "DynamicHUDCanvas");
                if (dynamicLayer == null)
                {
                    throw new InvalidOperationException(
                        "DynamicHUDCanvas is missing from the HUD prefab.");
                }

                Transform existing = FindDirect(
                    dynamicLayer,
                    "NotificationFeed");
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);

                GameObject feed = (GameObject)PrefabUtility.InstantiatePrefab(
                    feedPrefab,
                    dynamicLayer);
                feed.name = "NotificationFeed";
                RectTransform rect = (RectTransform)feed.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;

                if (root.GetComponent<StationHUDNotificationBridge>() == null)
                    root.AddComponent<StationHUDNotificationBridge>();

                if (PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath) == null)
                    throw new InvalidOperationException("HUD prefab save failed.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Transform FindDirect(Transform parent, string name)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == name)
                    return child;
            }
            return null;
        }

        private static void SetUiLayer(GameObject target)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
                target.layer = uiLayer;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                throw new InvalidOperationException($"Invalid folder: {path}");
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private readonly struct NotificationSeed
        {
            public NotificationSeed(
                string id,
                string localizationKey,
                HUDNotificationSeverity severity,
                float visibleSeconds)
            {
                Id = id;
                LocalizationKey = localizationKey;
                Severity = severity;
                VisibleSeconds = visibleSeconds;
            }

            public string Id { get; }
            public string LocalizationKey { get; }
            public HUDNotificationSeverity Severity { get; }
            public float VisibleSeconds { get; }
        }
    }
}
