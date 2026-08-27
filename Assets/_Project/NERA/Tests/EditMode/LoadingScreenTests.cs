using System.Linq;
using NERA.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NERA.Tests
{
    public sealed class LoadingScreenTests
    {
        private const string ConfigPath =
            "Assets/_Project/NERA/Configs/UI/CFG_LoadingScreen.asset";
        private const string PrefabPath =
            "Assets/_Project/NERA/Resources/UI/P_LoadingScreen.prefab";

        [Test]
        public void ConfigContainsIndependentImageAndLocalizedTipPools()
        {
            LoadingScreenConfig config =
                AssetDatabase.LoadAssetAtPath<LoadingScreenConfig>(ConfigPath);

            Assert.That(config, Is.Not.Null);
            Assert.That(config.MinimumDisplaySeconds, Is.GreaterThan(0f));
            Assert.That(config.Images.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(config.Images.All(image => image != null), Is.True);
            Assert.That(config.Tips.Count, Is.GreaterThanOrEqualTo(4));
            Assert.That(config.Tips.All(tip => tip != null), Is.True);
            string[] requiredKeys =
            {
                "loading.tip.station_power",
                "loading.tip.sandstorm",
                "loading.tip.antenna",
                "loading.tip.expedition"
            };
            Assert.That(
                config.Tips.Select(tip => tip.LocalizationKey),
                Is.SupersetOf(requiredKeys));
            Assert.That(
                config.Tips
                    .Where(tip => requiredKeys.Contains(tip.LocalizationKey))
                    .All(tip => tip.FallbackText.Split(' ').Length <= 20),
                Is.True);
        }

        [Test]
        public void PrefabContainsAuthoredFullscreenPresentation()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabPath);

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            LoadingScreenController controller =
                prefab.GetComponent<LoadingScreenController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.Config, Is.Not.Null);

            var serialized = new SerializedObject(controller);
            Assert.That(
                serialized.FindProperty("loadingCamera").objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("windowRoot").objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("loadingImage").objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("tipText").objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                serialized.FindProperty("loadingText").objectReferenceValue,
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find("LoadingCamera"),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find("LoadingWindow/TipText"),
                Is.Not.Null);
            Assert.That(
                prefab.transform.Find("LoadingWindow/LoadingText"),
                Is.Not.Null);
        }
    }
}
