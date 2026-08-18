using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NERA.Items;
using NERA.Localization;
using NERA.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEditor.Localization;
using UnityEngine.Localization.Tables;

namespace NERA.Tests
{
    public sealed class LocalizationTests
    {
        private static readonly string[] RequiredCollections =
        {
            NERALocalization.CommonTable,
            NERALocalization.MainMenuTable,
            NERALocalization.HudTable,
            NERALocalization.TerminalTable,
            NERALocalization.InventoryLaboratoryTable,
            NERALocalization.ContentTable,
            NERALocalization.QuestsTable
        };

        [Test]
        public void LocalizationSettingsAndRequiredCollectionsExist()
        {
            Assert.That(
                LocalizationEditorSettings.ActiveLocalizationSettings,
                Is.Not.Null,
                "Active Localization Settings asset is not configured.");

            foreach (string collectionName in RequiredCollections)
            {
                Assert.That(
                    LocalizationEditorSettings.GetStringTableCollection(collectionName),
                    Is.Not.Null,
                    $"Missing String Table Collection: {collectionName}");
            }
        }

        [Test]
        public void EnglishAndRussianTablesHaveEveryEntryFilled()
        {
            foreach (string collectionName in RequiredCollections)
            {
                StringTableCollection collection =
                    LocalizationEditorSettings.GetStringTableCollection(collectionName);
                Assert.That(collection, Is.Not.Null, collectionName);

                StringTable english = collection.StringTables.FirstOrDefault(
                    table => table.LocaleIdentifier.Code == NERALocalization.EnglishCode);
                StringTable russian = collection.StringTables.FirstOrDefault(
                    table => table.LocaleIdentifier.Code == NERALocalization.RussianCode);
                Assert.That(english, Is.Not.Null, $"{collectionName}: English table missing");
                Assert.That(russian, Is.Not.Null, $"{collectionName}: Russian table missing");

                foreach (SharedTableData.SharedTableEntry sharedEntry in
                         collection.SharedData.Entries)
                {
                    StringTableEntry englishEntry = english.GetEntry(sharedEntry.Id);
                    StringTableEntry russianEntry = russian.GetEntry(sharedEntry.Id);
                    Assert.That(
                        englishEntry?.Value,
                        Is.Not.Null.And.Not.Empty,
                        $"{collectionName}/{sharedEntry.Key}: English value missing");
                    Assert.That(
                        russianEntry?.Value,
                        Is.Not.Null.And.Not.Empty,
                        $"{collectionName}/{sharedEntry.Key}: Russian value missing");
                }
            }
        }

        [Test]
        public void EveryItemHasEnglishAndRussianContentEntries()
        {
            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(
                    NERALocalization.ContentTable);
            Assert.That(collection, Is.Not.Null);

            StringTable english = collection.StringTables.First(
                table => table.LocaleIdentifier.Code == NERALocalization.EnglishCode);
            StringTable russian = collection.StringTables.First(
                table => table.LocaleIdentifier.Code == NERALocalization.RussianCode);

            foreach (string guid in AssetDatabase.FindAssets(
                         "t:ItemData",
                         new[] { "Assets/_Project/NERA" }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                Assert.That(item, Is.Not.Null, assetPath);
                Assert.That(item.ItemId, Is.Not.Null.And.Not.Empty, assetPath);

                AssertLocalizedItemEntry(
                    english,
                    russian,
                    $"item.{item.ItemId}.name",
                    assetPath);
                AssertLocalizedItemEntry(
                    english,
                    russian,
                    $"item.{item.ItemId}.description",
                    assetPath);
            }
        }

        [Test]
        public void EveryEngineeringPartHasSpecificEnglishAndRussianText()
        {
            const string engineeringPartsRoot =
                "Assets/_Project/NERA/Configs/Items/Item_EngineeringPart";
            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(
                    NERALocalization.ContentTable);
            Assert.That(collection, Is.Not.Null);

            StringTable english = collection.StringTables.First(
                table => table.LocaleIdentifier.Code ==
                    NERALocalization.EnglishCode);
            StringTable russian = collection.StringTables.First(
                table => table.LocaleIdentifier.Code ==
                    NERALocalization.RussianCode);
            string[] guids = AssetDatabase.FindAssets(
                "t:ItemData",
                new[] { engineeringPartsRoot });

            Assert.That(guids, Has.Length.EqualTo(21));
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(assetPath);
                SerializedObject serialized = new SerializedObject(item);
                string sourceName =
                    serialized.FindProperty("displayName").stringValue;
                string sourceDescription =
                    serialized.FindProperty("description").stringValue;
                string nameKey = $"item.{item.ItemId}.name";
                string descriptionKey = $"item.{item.ItemId}.description";
                string englishName = english.GetEntry(nameKey)?.Value;
                string russianName = russian.GetEntry(nameKey)?.Value;
                string englishDescription = english.GetEntry(descriptionKey)?.Value;
                string russianDescription = russian.GetEntry(descriptionKey)?.Value;

                Assert.That(
                    sourceDescription,
                    Is.Not.EqualTo(
                        "Engineering part used to restore and upgrade station mechanisms."),
                    assetPath);
                Assert.That(
                    englishName,
                    Is.EqualTo(sourceName),
                    $"English name is stale for {assetPath}");
                Assert.That(
                    englishDescription,
                    Is.EqualTo(sourceDescription),
                    $"English description is stale for {assetPath}");
                Assert.That(
                    russianName,
                    Does.Match("[А-Яа-яЁё]"),
                    $"Russian name is not localized for {assetPath}");
                Assert.That(
                    russianDescription,
                    Does.Match("[А-Яа-яЁё]"),
                    $"Russian description is not localized for {assetPath}");
                Assert.That(
                    russianDescription,
                    Is.Not.EqualTo(englishDescription),
                    $"Russian description duplicates English for {assetPath}");
            }
        }

        private static void AssertLocalizedItemEntry(
            StringTable english,
            StringTable russian,
            string key,
            string assetPath)
        {
            Assert.That(
                english.GetEntry(key)?.Value,
                Is.Not.Null.And.Not.Empty,
                $"Missing English localization '{key}' for {assetPath}");
            Assert.That(
                russian.GetEntry(key)?.Value,
                Is.Not.Null.And.Not.Empty,
                $"Missing Russian localization '{key}' for {assetPath}");
        }

        [TestCase(1080f, 1920f, 0f)]
        [TestCase(1920f, 1080f, 0f)]
        [TestCase(2560f, 1080f, 1f)]
        public void ResponsiveCanvasChoosesTheDimensionThatPreventsCropping(
            float width,
            float height,
            float expectedMatch)
        {
            Assert.That(
                ResponsiveCanvasLayout.CalculateMatchWidthOrHeight(
                    width,
                    height,
                    ResponsiveCanvasLayout.DefaultReferenceResolution),
                Is.EqualTo(expectedMatch));
        }

        [Test]
        public void HudPrefabUsesResponsiveCanvasLayout()
        {
            GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/NERA/Prefabs/UI/P_HUD_Canvas.prefab");
            Assert.That(hud, Is.Not.Null);
            Assert.That(
                hud.GetComponentInChildren<ResponsiveCanvasLayout>(true),
                Is.Not.Null,
                "HUD root must adapt its CanvasScaler to the screen aspect.");
        }

        [Test]
        public void ProductionUiOwnsAllTextSizing()
        {
            const string projectRoot = "Assets/_Project/NERA";
            foreach (string path in Directory.EnumerateFiles(
                         projectRoot,
                         "*.*",
                         SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(path);
                if (extension != ".prefab" &&
                    extension != ".unity" &&
                    extension != ".asset")
                {
                    continue;
                }

                string serializedAsset = File.ReadAllText(path);
                Assert.That(
                    Regex.IsMatch(
                        serializedAsset,
                        @"m_enableAutoSizing:\s*1\b|" +
                        @"m_ResizeTextForBestFit:\s*1\b|" +
                        @"m_BestFit:\s*1\b"),
                    Is.False,
                    $"Automatic text sizing is enabled in {path}.");
            }

            string codeRoot = Path.Combine(projectRoot, "Code");
            foreach (string path in Directory.EnumerateFiles(
                         codeRoot,
                         "*.cs",
                         SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(path);
                Assert.That(
                    Regex.IsMatch(
                        source,
                        @"\bfontSize\b|" +
                        @"enableAutoSizing|" +
                        @"resizeTextForBestFit|" +
                        @"autoSizeTextContainer|" +
                        @"<size="),
                    Is.False,
                    $"Text sizing must be configured in UI assets, not {path}.");
            }
        }
    }
}
