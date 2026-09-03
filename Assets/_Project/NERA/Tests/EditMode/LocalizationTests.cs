using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NERA.Interaction;
using NERA.Items;
using NERA.Localization;
using NERA.Quests;
using NERA.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
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

        private static readonly HashSet<string> RussianCodeOnlyEntries =
            new HashSet<string>
            {
                "save.date_format"
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
        public void NullSelectedLocaleIsRestoredBeforeReadingStrings()
        {
            Locale previous = LocalizationSettings.SelectedLocale;
            try
            {
                LocalizationSettings.SelectedLocale = null;

                string localized = NERALocalization.Get(
                    NERALocalization.CommonTable,
                    "common.yes");

                Assert.That(localized, Is.Not.Empty);
                Assert.That(
                    LocalizationSettings.SelectedLocale,
                    Is.Not.Null);
            }
            finally
            {
                LocalizationSettings.SelectedLocale = previous;
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
        public void EnglishTablesDoNotContainRussianFallbacks()
        {
            foreach (string collectionName in RequiredCollections)
            {
                StringTableCollection collection =
                    LocalizationEditorSettings.GetStringTableCollection(
                        collectionName);
                Assert.That(collection, Is.Not.Null, collectionName);
                StringTable english = collection.StringTables.First(
                    table => table.LocaleIdentifier.Code ==
                        NERALocalization.EnglishCode);

                foreach (SharedTableData.SharedTableEntry sharedEntry in
                         collection.SharedData.Entries)
                {
                    string value = english.GetEntry(sharedEntry.Id)?.Value;
                    Assert.That(
                        value,
                        Does.Not.Match("[А-Яа-яЁё]"),
                        $"{collectionName}/{sharedEntry.Key}: English text " +
                        "contains a Russian fallback.");
                }
            }
        }

        [Test]
        public void ApprovedEnglishUiCopyIsUsed()
        {
            StringTable terminal = EnglishTable(
                NERALocalization.TerminalTable);
            StringTable quests = EnglishTable(
                NERALocalization.QuestsTable);

            Assert.That(
                terminal.GetEntry("map.travel_confirmation")?.Value,
                Is.EqualTo("Travel to this location?"));
            Assert.That(
                terminal.GetEntry("map.state.signalfound")?.Value,
                Is.EqualTo("SIGNAL FOUND"));
            Assert.That(
                quests.GetEntry(
                    "quest.main.restore_station.stage.03.title")?.Value,
                Is.EqualTo("Enable the Cleaning Systems"));
            Assert.That(
                quests.GetEntry(
                    "quest.main.restore_station.stage.03.description")?.Value,
                Is.EqualTo("Clean the station equipment."));
            Assert.That(
                quests.GetEntry(
                    "quest.main.expedition_01.stage.05.title")?.Value,
                Is.EqualTo("Start the Laboratory"));
            Assert.That(
                quests.GetEntry(
                    "quest.main.expedition_01.stage.06.title")?.Value,
                Is.EqualTo("Analyze the Sample"));
        }

        [Test]
        public void LocalizedCopyDoesNotUseUnknownSignalWording()
        {
            foreach (string collectionName in RequiredCollections)
            {
                StringTableCollection collection =
                    LocalizationEditorSettings.GetStringTableCollection(
                        collectionName);
                Assert.That(collection, Is.Not.Null, collectionName);

                foreach (StringTable table in collection.StringTables)
                {
                    foreach (SharedTableData.SharedTableEntry sharedEntry in
                             collection.SharedData.Entries)
                    {
                        string value =
                            table.GetEntry(sharedEntry.Id)?.Value ?? string.Empty;
                        Assert.That(
                            value.ToLowerInvariant(),
                            Does.Not.Contain("unknown signal"),
                            $"{collectionName}/{sharedEntry.Key}");
                        Assert.That(
                            value.ToLowerInvariant(),
                            Does.Not.Contain("неизвестн"),
                            $"{collectionName}/{sharedEntry.Key}");
                    }
                }
            }
        }

        [Test]
        public void EveryQuestStageHasEnglishAndRussianEntries()
        {
            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(
                    NERALocalization.QuestsTable);
            Assert.That(collection, Is.Not.Null);
            StringTable english = collection.StringTables.First(
                table => table.LocaleIdentifier.Code ==
                    NERALocalization.EnglishCode);
            StringTable russian = collection.StringTables.First(
                table => table.LocaleIdentifier.Code ==
                    NERALocalization.RussianCode);

            foreach (string guid in AssetDatabase.FindAssets(
                         "t:QuestDefinition",
                         new[] { "Assets/_Project/NERA/Configs/Quests" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                QuestDefinition quest =
                    AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                Assert.That(quest, Is.Not.Null, path);
                string baseKey = "quest." + quest.QuestId;
                AssertQuestEntry(baseKey + ".title", path);
                AssertQuestEntry(baseKey + ".description", path);
                for (int index = 0; index < quest.Stages.Count; index++)
                {
                    string stageKey =
                        $"{baseKey}.stage.{index + 1:00}";
                    AssertQuestEntry(stageKey + ".title", path);
                    AssertQuestEntry(stageKey + ".description", path);
                }
            }

            void AssertQuestEntry(string key, string path)
            {
                Assert.That(
                    english.GetEntry(key)?.Value,
                    Is.Not.Null.And.Not.Empty,
                    $"Missing English quest text '{key}' for {path}");
                Assert.That(
                    russian.GetEntry(key)?.Value,
                    Is.Not.Null.And.Not.Empty,
                    $"Missing Russian quest text '{key}' for {path}");
            }
        }

        private static StringTable EnglishTable(string collectionName)
        {
            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(
                    collectionName);
            Assert.That(collection, Is.Not.Null, collectionName);
            return collection.StringTables.First(
                table => table.LocaleIdentifier.Code ==
                    NERALocalization.EnglishCode);
        }

        [Test]
        public void RussianTablesDoNotContainUntranslatedEnglishFallbacks()
        {
            foreach (string collectionName in RequiredCollections)
            {
                StringTableCollection collection =
                    LocalizationEditorSettings.GetStringTableCollection(
                        collectionName);
                Assert.That(collection, Is.Not.Null, collectionName);
                StringTable russian = collection.StringTables.First(
                    table => table.LocaleIdentifier.Code ==
                        NERALocalization.RussianCode);

                foreach (SharedTableData.SharedTableEntry sharedEntry in
                         collection.SharedData.Entries)
                {
                    if (RussianCodeOnlyEntries.Contains(sharedEntry.Key) ||
                        Regex.IsMatch(
                            sharedEntry.Key,
                            @"^(?:location|target)\.unknownsignal\d+\.name$"))
                        continue;

                    string value = russian.GetEntry(sharedEntry.Id)?.Value;
                    Assert.That(
                        value,
                        Does.Match("[А-Яа-яЁё]"),
                        $"{collectionName}/{sharedEntry.Key}: Russian text " +
                        "does not contain a Russian translation.");
                }
            }
        }

        [Test]
        public void RuntimeInteractionPromptsHaveRussianTranslations()
        {
            string[] requiredKeys =
            {
                "interaction.action.clean_solar_panel",
                "interaction.action.clean_antenna",
                "interaction.action.clean_turret",
                "interaction.action.clean_drone",
                "interaction.action.service_device",
                "interaction.action.use_laboratory",
                "interaction.action.start_laboratory",
                "interaction.action.use_terminal",
                "interaction.action.start_terminal",
                "interaction.action.start_object",
                "interaction.action.configure_object",
                "interaction.unavailable.laboratory_has_no_power",
                "interaction.unavailable.terminal_offline_restore_power_first",
                "interaction.unavailable.station_power_is_unavailable.",
                "interaction.unavailable.maintenance_is_unavailable",
                "interaction.unavailable.cleaning_is_in_progress",
                "interaction.unavailable.battery_charge_below"
            };
            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(
                    NERALocalization.HudTable);
            Assert.That(collection, Is.Not.Null);
            StringTable russian = collection.StringTables.First(
                table => table.LocaleIdentifier.Code ==
                    NERALocalization.RussianCode);

            foreach (string key in requiredKeys)
            {
                Assert.That(
                    russian.GetEntry(key)?.Value,
                    Does.Match("[А-Яа-яЁё]"),
                    $"Missing Russian interaction text: {key}");
            }
        }

        [Test]
        public void LockedMapStateHasRussianTranslation()
        {
            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(
                    NERALocalization.TerminalTable);
            Assert.That(collection, Is.Not.Null);
            StringTable russian = collection.StringTables.First(
                table => table.LocaleIdentifier.Code ==
                    NERALocalization.RussianCode);

            Assert.That(
                russian.GetEntry("map.state.locked")?.Value,
                Is.EqualTo("ЗАБЛОКИРОВАН"));
        }

        [Test]
        public void DynamicInteractionPromptsAreComposedInRussian()
        {
            Locale previous = LocalizationSettings.SelectedLocale;
            Locale russian = LocalizationEditorSettings.GetLocale(
                NERALocalization.RussianCode);
            Assert.That(russian, Is.Not.Null);

            try
            {
                LocalizationSettings.SelectedLocale = russian;
                Assert.That(
                    Prompt("Configure Батарея").ActionText,
                    Is.EqualTo("Настроить: Батарея"));
                Assert.That(
                    Prompt("Start Турель 1").ActionText,
                    Is.EqualTo("Запустить: Турель 1"));
                Assert.That(
                    Prompt(
                        "Use Terminal",
                        "Station power is unavailable.").UnavailableReason,
                    Is.EqualTo("Питание станции недоступно."));
                Assert.That(
                    Prompt(
                        "Use Terminal",
                        "Maintenance is unavailable").UnavailableReason,
                    Is.EqualTo("Обслуживание недоступно"));
                Assert.That(
                    Prompt(
                        "Use Terminal",
                        "Battery charge below 15%.").UnavailableReason,
                    Is.EqualTo("Заряд батареи ниже 15%."));
            }
            finally
            {
                LocalizationSettings.SelectedLocale = previous;
            }
        }

        private static InteractionPrompt Prompt(
            string action,
            string unavailableReason = "")
        {
            return new InteractionPrompt(
                action,
                NERA.Interaction.InteractionMode.Press,
                0f,
                string.IsNullOrEmpty(unavailableReason),
                unavailableReason);
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

            Assert.That(guids, Has.Length.EqualTo(25));
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
                string normalizedPath = path.Replace('\\', '/');
                if (normalizedPath.Contains("/Code/Editor/"))
                    continue;

                string source = File.ReadAllText(path);
                Assert.That(
                    Regex.IsMatch(
                        source,
                        @"enableAutoSizing\s*=\s*true|" +
                        @"resizeTextForBestFit\s*=\s*true|" +
                        @"autoSizeTextContainer\s*=\s*true|" +
                        @"<size="),
                    Is.False,
                    $"Runtime code must not enable automatic text sizing: {path}.");
            }
        }
    }
}
