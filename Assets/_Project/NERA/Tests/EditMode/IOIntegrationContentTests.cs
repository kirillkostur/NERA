using System.Linq;
using NERA.Combat;
using NERA.Items;
using NERA.Localization;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace NERA.Tests
{
    public sealed class IOIntegrationContentTests
    {
        [TestCase(
            "io_blue_shard_01",
            "io_blue_discharge",
            AnomalyIntegrationEffect.EnableElectronics,
            8f,
            0f,
            8f)]
        [TestCase(
            "io_green_node_02",
            "io_green_restoration",
            AnomalyIntegrationEffect.RestoreFullHealth,
            8f,
            0f,
            0f)]
        [TestCase(
            "io_yellow_lens_03",
            "io_yellow_scan",
            AnomalyIntegrationEffect.RevealThroughWalls,
            10f,
            0f,
            6f)]
        [TestCase(
            "io_red_core_04",
            "io_red_blast",
            AnomalyIntegrationEffect.DamageAnomalies,
            8f,
            40f,
            0f)]
        [TestCase(
            "io_violet_core_05",
            "io_violet_overload",
            AnomalyIntegrationEffect
                .DisableElectronicsPermanently,
            12.5f,
            400f,
            0f)]
        public void StoneIsLinkedToExpectedWeaponIntegration(
            string itemId,
            string integrationId,
            AnomalyIntegrationEffect effect,
            float radius,
            float damage,
            float duration)
        {
            ItemData item = FindItem(itemId);
            Assert.That(item, Is.Not.Null, itemId);

            AnomalyIntegrationDefinition definition =
                item.AnomalyIntegrationDefinition;
            Assert.That(definition, Is.Not.Null, itemId);
            Assert.That(
                definition.IntegrationId,
                Is.EqualTo(integrationId));
            Assert.That(definition.Effect, Is.EqualTo(effect));
            Assert.That(
                definition.Radius,
                Is.EqualTo(radius).Within(0.001f));
            Assert.That(
                definition.AnomalyDamage,
                Is.EqualTo(damage).Within(0.001f));
            Assert.That(
                definition.EffectDuration,
                Is.EqualTo(duration).Within(0.001f));
        }

        [Test]
        public void VioletRadiusIsTwentyFivePercentAboveLargestNormalRadius()
        {
            AnomalyIntegrationDefinition violet =
                FindItem("io_violet_core_05")
                    .AnomalyIntegrationDefinition;
            float largestNormalRadius = new[]
                {
                    "io_blue_shard_01",
                    "io_green_node_02",
                    "io_yellow_lens_03",
                    "io_red_core_04"
                }
                .Select(id =>
                    FindItem(id)
                        .AnomalyIntegrationDefinition
                        .Radius)
                .Max();

            Assert.That(largestNormalRadius, Is.EqualTo(10f));
            Assert.That(
                violet.Radius,
                Is.EqualTo(largestNormalRadius * 1.25f)
                    .Within(0.001f));
        }

        [Test]
        public void AllIntegrationNamesAreLocalizedInEnglishAndRussian()
        {
            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(
                    NERALocalization.ContentTable);
            Assert.That(collection, Is.Not.Null);

            StringTable english =
                collection.GetTable("en") as StringTable;
            StringTable russian =
                collection.GetTable("ru") as StringTable;
            string[] ids =
            {
                "io_blue_discharge",
                "io_green_restoration",
                "io_yellow_scan",
                "io_red_blast",
                "io_violet_overload"
            };

            foreach (string id in ids)
            {
                string key = "integration." + id + ".name";
                string en = english.GetEntry(key)?.Value;
                string ru = russian.GetEntry(key)?.Value;
                Assert.That(en, Is.Not.Null.And.Not.Empty, key);
                Assert.That(ru, Is.Not.Null.And.Not.Empty, key);
                Assert.That(ru, Is.Not.EqualTo(en), key);
            }
        }

        private static ItemData FindItem(string itemId)
        {
            foreach (string guid in AssetDatabase.FindAssets(
                         "t:ItemData",
                         new[]
                         {
                             "Assets/_Project/NERA/Configs/Items"
                         }))
            {
                ItemData item =
                    AssetDatabase.LoadAssetAtPath<ItemData>(
                        AssetDatabase.GUIDToAssetPath(guid));
                if (item != null && item.ItemId == itemId)
                    return item;
            }

            return null;
        }
    }
}
