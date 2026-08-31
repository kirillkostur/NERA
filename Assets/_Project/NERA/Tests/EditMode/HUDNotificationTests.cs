using System;
using System.Linq;
using System.Reflection;
using NERA.Localization;
using NERA.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using UnityEngine.UI;

namespace NERA.Tests
{
    public sealed class HUDNotificationTests
    {
        private const string CatalogPath =
            "Assets/_Project/NERA/Resources/UI/" +
            "HUDNotificationCatalog_Default.asset";
        private const string NotificationPrefabPath =
            "Assets/_Project/NERA/Resources/UI/P_HUD_Notification.prefab";
        private const string FeedPrefabPath =
            "Assets/_Project/NERA/Prefabs/UI/HUD/" +
            "P_HUD_NotificationFeed.prefab";
        private const string HudPrefabPath =
            "Assets/_Project/NERA/Prefabs/UI/P_HUD_Canvas.prefab";

        private static readonly string[] RequiredIds =
        {
            HUDNotificationIds.StormStarted,
            HUDNotificationIds.StormEnded,
            HUDNotificationIds.BatteryLow,
            HUDNotificationIds.BatteryDisabled,
            HUDNotificationIds.BatteryEnabled,
            HUDNotificationIds.PowerLost,
            HUDNotificationIds.PowerRestored,
            HUDNotificationIds.DroneDeparted,
            HUDNotificationIds.DroneReturned,
            HUDNotificationIds.DroneLocationDiscovered,
            HUDNotificationIds.DroneNoNewLocations,
            HUDNotificationIds.AntennaSignalFound,
            HUDNotificationIds.AntennaSignalNotFound,
            HUDNotificationIds.StationObjectContaminated,
            HUDNotificationIds.StationObjectDisabled,
            HUDNotificationIds.ResearchCompleted
        };

        [Test]
        public void CatalogContainsEveryConfiguredEvent()
        {
            HUDNotificationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<HUDNotificationCatalog>(
                    CatalogPath);
            Assert.That(catalog, Is.Not.Null);

            foreach (string id in RequiredIds)
            {
                Assert.That(
                    catalog.TryGet(id, out HUDNotificationDefinition entry),
                    Is.True,
                    $"Notification '{id}' is missing.");
                Assert.That(entry.LocalizationKey, Does.StartWith("notification."));
                Assert.That(entry.VisibleSeconds, Is.GreaterThanOrEqualTo(0.5f));
            }

            Assert.That(
                catalog.Entries.Count,
                Is.GreaterThanOrEqualTo(RequiredIds.Length));
        }

        [TestCase(HUDNotificationIds.StationObjectDisabled)]
        [TestCase(HUDNotificationIds.StationObjectContaminated)]
        public void StationObjectNotificationsAreWarnings(string id)
        {
            HUDNotificationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<HUDNotificationCatalog>(
                    CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                catalog.TryGet(
                    id,
                    out HUDNotificationDefinition definition),
                Is.True);
            Assert.That(
                definition.Severity,
                Is.EqualTo(HUDNotificationSeverity.Warning));
        }

        [Test]
        public void NotificationTextsExistInEnglishAndRussianAndAreOneLine()
        {
            HUDNotificationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<HUDNotificationCatalog>(
                    CatalogPath);
            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(
                    NERALocalization.HudTable);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(collection, Is.Not.Null);

            StringTable english = collection.StringTables.First(
                table => table.LocaleIdentifier.Code ==
                    NERALocalization.EnglishCode);
            StringTable russian = collection.StringTables.First(
                table => table.LocaleIdentifier.Code ==
                    NERALocalization.RussianCode);

            foreach (HUDNotificationDefinition definition in catalog.Entries)
            {
                AssertOneLine(
                    english.GetEntry(definition.LocalizationKey)?.Value,
                    $"EN/{definition.LocalizationKey}");
                AssertOneLine(
                    russian.GetEntry(definition.LocalizationKey)?.Value,
                    $"RU/{definition.LocalizationKey}");
            }
        }

        [Test]
        public void DiscoveryNotificationsUseGenericLocalizedCopy()
        {
            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(
                    NERALocalization.HudTable);
            Assert.That(collection, Is.Not.Null);
            StringTable english = collection.StringTables.First(
                table => table.LocaleIdentifier.Code ==
                    NERALocalization.EnglishCode);
            StringTable russian = collection.StringTables.First(
                table => table.LocaleIdentifier.Code ==
                    NERALocalization.RussianCode);

            AssertCopy(
                "notification.drone.location_discovered",
                "Drone discovered a new location",
                "Дрон обнаружил новую локацию");
            AssertCopy(
                "notification.antenna.signal_found",
                "Antenna detected a signal",
                "Антенна обнаружила сигнал");

            void AssertCopy(string key, string expectedEn, string expectedRu)
            {
                string englishValue = english.GetEntry(key)?.Value;
                string russianValue = russian.GetEntry(key)?.Value;
                Assert.That(englishValue, Is.EqualTo(expectedEn));
                Assert.That(russianValue, Is.EqualTo(expectedRu));
                Assert.That(englishValue, Does.Not.Contain("{0}"));
                Assert.That(russianValue, Does.Not.Contain("{0}"));
            }
        }

        [Test]
        public void CatalogDoesNotDuplicateLocalizedText()
        {
            const BindingFlags fields =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly;

            Assert.That(
                typeof(HUDNotificationDefinition).GetField(
                    "englishText",
                    fields),
                Is.Null);
            Assert.That(
                typeof(HUDNotificationDefinition).GetField(
                    "russianText",
                    fields),
                Is.Null);
            Assert.That(
                typeof(HUDNotificationDefinition).GetField("smart", fields),
                Is.Null);
        }

        [Test]
        public void PendingNotificationsUseSeverityPriorityAndStableOrder()
        {
            HUDNotificationCatalog catalog =
                AssetDatabase.LoadAssetAtPath<HUDNotificationCatalog>(
                    CatalogPath);
            Assert.That(catalog, Is.Not.Null);

            HUDNotificationService.ClearPending();
            try
            {
                HUDNotificationService.Publish(
                    HUDNotificationIds.StormEnded);
                HUDNotificationService.Publish(
                    HUDNotificationIds.BatteryLow,
                    25);
                HUDNotificationService.Publish(
                    HUDNotificationIds.DroneDeparted);
                HUDNotificationService.Publish(
                    HUDNotificationIds.StormStarted);

                AssertNext(HUDNotificationIds.StormStarted);
                AssertNext(HUDNotificationIds.BatteryLow);
                AssertNext(HUDNotificationIds.DroneDeparted);
                AssertNext(HUDNotificationIds.StormEnded);
            }
            finally
            {
                HUDNotificationService.ClearPending();
            }

            void AssertNext(string expectedId)
            {
                Assert.That(
                    HUDNotificationService.TryDequeueHighestPriority(
                        GetPriority,
                        out HUDNotificationRequest request),
                    Is.True);
                Assert.That(request.Id, Is.EqualTo(expectedId));
            }

            int GetPriority(HUDNotificationRequest request)
            {
                Assert.That(
                    catalog.TryGet(
                        request.Id,
                        out HUDNotificationDefinition definition),
                    Is.True);
                return definition.Severity switch
                {
                    HUDNotificationSeverity.Critical => 300,
                    HUDNotificationSeverity.Warning => 200,
                    _ => 100
                };
            }
        }

        [Test]
        public void HudUsesResponsiveNestedNotificationFeed()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(
                HudPrefabPath);
            try
            {
                Assert.That(
                    root.GetComponent<StationHUDNotificationBridge>(),
                    Is.Not.Null);
                Transform dynamicLayer = RequireDirect(
                    root.transform,
                    "DynamicHUDCanvas");
                RectTransform feed = RequireDirect(
                    dynamicLayer,
                    "NotificationFeed") as RectTransform;
                Assert.That(feed, Is.Not.Null);
                Assert.That(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                        feed.gameObject),
                    Is.EqualTo(FeedPrefabPath));
                AssertVector(feed.anchorMin, Vector2.zero);
                AssertVector(feed.anchorMax, Vector2.one);
                AssertVector(feed.pivot, new Vector2(0.5f, 0.5f));
                AssertVector(feed.anchoredPosition, Vector2.zero);
                AssertVector(feed.sizeDelta, Vector2.zero);
                Assert.That(
                    feed.GetComponent<HUDNotificationController>(),
                    Is.Not.Null);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void NotificationUiAndBridgeDoNotPollGameplayInUpdate()
        {
            AssertNoDeclaredUpdate(typeof(HUDNotificationController));
            AssertNoDeclaredUpdate(typeof(StationHUDNotificationBridge));
        }

        [Test]
        public void NotificationPrefabContainsDedicatedViewAndMessage()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                NotificationPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            HUDNotificationView view =
                prefab.GetComponent<HUDNotificationView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(
                prefab.GetComponentsInChildren<Component>(true).Any(
                    component =>
                        component.GetType().FullName ==
                        "TMPro.TextMeshProUGUI"),
                Is.True);
        }

        [Test]
        public void NotificationRectIsControlledByNotificationPrefab()
        {
            GameObject notification =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    NotificationPrefabPath);
            Assert.That(notification, Is.Not.Null);

            RectTransform notificationRect =
                notification.GetComponent<RectTransform>();
            AssertVector(notificationRect.anchorMin, new Vector2(0.5f, 1f));
            AssertVector(notificationRect.anchorMax, new Vector2(0.5f, 1f));
            Assert.That(notificationRect.sizeDelta.x, Is.GreaterThan(0f));
            Assert.That(notificationRect.sizeDelta.y, Is.GreaterThan(0f));
            Assert.That(notification.GetComponent<LayoutElement>(), Is.Null);

            GameObject feed = AssetDatabase.LoadAssetAtPath<GameObject>(
                FeedPrefabPath);
            Assert.That(feed, Is.Not.Null);
            Assert.That(feed.GetComponent<VerticalLayoutGroup>(), Is.Null);
            Assert.That(feed.GetComponent<ContentSizeFitter>(), Is.Null);
        }

        private static void AssertNoDeclaredUpdate(Type type)
        {
            MethodInfo update = type.GetMethod(
                "Update",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);
            Assert.That(
                update,
                Is.Null,
                $"{type.Name} must react to events instead of polling Update().");
        }

        private static void AssertOneLine(string value, string label)
        {
            Assert.That(value, Is.Not.Null.And.Not.Empty, label);
            Assert.That(value, Does.Not.Contain("\n"), label);
            Assert.That(value, Does.Not.Contain("\r"), label);
        }

        private static Transform RequireDirect(
            Transform parent,
            string name)
        {
            for (int index = 0; index < parent.childCount; index++)
            {
                Transform child = parent.GetChild(index);
                if (child.name == name)
                    return child;
            }

            Assert.Fail($"'{name}' was not found below '{parent.name}'.");
            return null;
        }

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.001f));
        }
    }
}
