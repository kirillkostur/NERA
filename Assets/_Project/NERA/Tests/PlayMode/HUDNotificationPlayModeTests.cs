using System.Collections;
using System.Reflection;
using NERA.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace NERA.Tests
{
    public sealed class HUDNotificationPlayModeTests
    {
        private GameObject root;
        private HUDNotificationCatalog testCatalog;
        private HUDNotificationController controller;

        [SetUp]
        public void SetUp()
        {
            HUDNotificationService.ClearPending();
            root = new GameObject(
                "NotificationTestRoot",
                typeof(RectTransform));
            root.SetActive(false);
            controller = root.AddComponent<HUDNotificationController>();
            testCatalog = Object.Instantiate(
                HUDNotificationCatalog.LoadDefault());
            foreach (HUDNotificationDefinition definition in
                     testCatalog.Entries)
            {
                SetField(definition, "visibleSeconds", 0.5f);
            }
            SetField(controller, "catalog", testCatalog);
            SetField(controller, "fadeInSeconds", 0.01f);
            SetField(controller, "fadeOutSeconds", 0.01f);
            root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            HUDNotificationService.ClearPending();
            if (root != null)
                Object.DestroyImmediate(root);
            if (testCatalog != null)
                Object.DestroyImmediate(testCatalog);
        }

        [UnityTest]
        public IEnumerator PublishedEventsShowOnlyOneNotificationAtATime()
        {
            HUDNotificationService.Publish(
                HUDNotificationIds.StormStarted);
            HUDNotificationService.Publish(
                HUDNotificationIds.BatteryLow,
                25);
            HUDNotificationService.Publish(
                HUDNotificationIds.DroneReturned);
            HUDNotificationService.Publish(
                HUDNotificationIds.StormEnded);
            yield return null;

            HUDNotificationView[] views =
                root.GetComponentsInChildren<HUDNotificationView>(true);
            Assert.That(controller.ActiveCount, Is.EqualTo(1));
            Assert.That(controller.QueuedCount, Is.EqualTo(3));
            Assert.That(views.Length, Is.EqualTo(1));
            RectTransform template = Resources
                .Load<HUDNotificationView>("UI/P_HUD_Notification")
                .transform as RectTransform;
            RectTransform first = views[0].transform as RectTransform;
            Assert.That(first, Is.Not.Null);
            Assert.That(template, Is.Not.Null);
            Assert.That(
                first.anchoredPosition,
                Is.EqualTo(template.anchoredPosition));
            foreach (HUDNotificationView view in views)
            {
                Assert.That(view.NotificationId, Is.Not.Empty);
                Assert.That(view.Message, Is.Not.Empty);
            }
        }

        [UnityTest]
        public IEnumerator QueuedNotificationsUseSeverityPriority()
        {
            HUDNotificationService.Publish(
                HUDNotificationIds.StormEnded);
            yield return null;
            Assert.That(
                controller.ActiveNotificationId,
                Is.EqualTo(HUDNotificationIds.StormEnded));

            HUDNotificationService.Publish(
                HUDNotificationIds.BatteryLow,
                25);
            HUDNotificationService.Publish(
                HUDNotificationIds.DroneDeparted);
            HUDNotificationService.Publish(
                HUDNotificationIds.StormStarted);

            Assert.That(controller.ActiveCount, Is.EqualTo(1));
            Assert.That(controller.QueuedCount, Is.EqualTo(3));

            yield return WaitForActive(HUDNotificationIds.StormStarted);
            yield return WaitForActive(HUDNotificationIds.BatteryLow);
            yield return WaitForActive(HUDNotificationIds.DroneDeparted);
        }

        private IEnumerator WaitForActive(string expectedId)
        {
            float timeoutAt = Time.realtimeSinceStartup + 2f;
            while (controller.ActiveNotificationId != expectedId &&
                   Time.realtimeSinceStartup < timeoutAt)
            {
                yield return null;
            }

            Assert.That(
                controller.ActiveNotificationId,
                Is.EqualTo(expectedId));
            Assert.That(controller.ActiveCount, Is.EqualTo(1));
        }

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance |
                BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
