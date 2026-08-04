using System;
using System.IO;
using NERA.Save;
using NUnit.Framework;
using UnityEditor;

namespace NERA.Tests
{
    public sealed class SavePathMigrationTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(
                Path.GetTempPath(),
                "NERA_SavePathMigrationTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(testRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(testRoot))
                Directory.Delete(testRoot, true);
        }

        [Test]
        public void ProductionIdentityIsLocked()
        {
            Assert.That(PlayerSettings.companyName, Is.EqualTo("Measured Field"));
            Assert.That(PlayerSettings.productName, Is.EqualTo("Nera"));
        }

        [Test]
        public void LegacyPathUsesPreviousCompanyAndProductFolders()
        {
            string currentPath = Path.Combine(
                testRoot,
                "Measured Field",
                "Nera",
                "nera_save.json");

            Assert.That(
                SavePathMigration.GetLegacySavePath(currentPath),
                Is.EqualTo(Path.Combine(
                    testRoot,
                    SavePathMigration.LegacyCompanyName,
                    SavePathMigration.LegacyProductName,
                    "nera_save.json")));
        }

        [Test]
        public void MigrationMovesLegacySaveWithoutChangingItsContents()
        {
            string currentPath = GetCurrentSavePath();
            string legacyPath =
                SavePathMigration.GetLegacySavePath(currentPath);
            Directory.CreateDirectory(Path.GetDirectoryName(legacyPath));
            File.WriteAllText(legacyPath, "{\"version\":14}");

            Assert.That(
                SavePathMigration.HasCurrentOrLegacySave(currentPath),
                Is.True);
            Assert.That(
                SavePathMigration.TryMigrateLegacySave(currentPath),
                Is.True);
            Assert.That(File.ReadAllText(currentPath),
                Is.EqualTo("{\"version\":14}"));
            Assert.That(File.Exists(legacyPath), Is.False);
        }

        [Test]
        public void MigrationNeverOverwritesCurrentSave()
        {
            string currentPath = GetCurrentSavePath();
            string legacyPath =
                SavePathMigration.GetLegacySavePath(currentPath);
            Directory.CreateDirectory(Path.GetDirectoryName(currentPath));
            Directory.CreateDirectory(Path.GetDirectoryName(legacyPath));
            File.WriteAllText(currentPath, "current");
            File.WriteAllText(legacyPath, "legacy");

            Assert.That(
                SavePathMigration.TryMigrateLegacySave(currentPath),
                Is.False);
            Assert.That(File.ReadAllText(currentPath), Is.EqualTo("current"));
            Assert.That(File.ReadAllText(legacyPath), Is.EqualTo("legacy"));
        }

        [Test]
        public void SaveSlotsUseThreeStableNumberedFileNames()
        {
            Assert.That(
                SaveSlotStorage.GetSlotFileName(1),
                Is.EqualTo("nera_save_1.json"));
            Assert.That(
                SaveSlotStorage.GetSlotFileName(2),
                Is.EqualTo("nera_save_2.json"));
            Assert.That(
                SaveSlotStorage.GetSlotFileName(3),
                Is.EqualTo("nera_save_3.json"));
        }

        [Test]
        public void LegacySingleSaveMigratesIntoSlotOneOnlyOnce()
        {
            string legacySinglePath = Path.Combine(
                testRoot,
                SaveSlotStorage.LegacySingleFileName);
            string slotOnePath = Path.Combine(
                testRoot,
                SaveSlotStorage.GetSlotFileName(1));
            File.WriteAllText(legacySinglePath, "legacy-single");

            Assert.That(
                SaveSlotStorage.TryMigrateLegacySingleSaveToSlotOne(
                    testRoot,
                    string.Empty),
                Is.True);
            Assert.That(File.ReadAllText(slotOnePath),
                Is.EqualTo("legacy-single"));
            Assert.That(File.Exists(legacySinglePath), Is.False);

            File.Delete(slotOnePath);
            File.WriteAllText(legacySinglePath, "must-not-resurrect");
            Assert.That(
                SaveSlotStorage.TryMigrateLegacySingleSaveToSlotOne(
                    testRoot,
                    string.Empty),
                Is.False);
            Assert.That(File.Exists(slotOnePath), Is.False);
            Assert.That(File.Exists(legacySinglePath), Is.True);
        }

        [Test]
        public void PreviousIdentitySingleSaveFallsBackIntoSlotOne()
        {
            string previousIdentityPath = Path.Combine(
                testRoot,
                "previous",
                SaveSlotStorage.LegacySingleFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(previousIdentityPath));
            File.WriteAllText(previousIdentityPath, "previous-identity");

            Assert.That(
                SaveSlotStorage.TryMigrateLegacySingleSaveToSlotOne(
                    testRoot,
                    previousIdentityPath),
                Is.True);
            Assert.That(
                File.ReadAllText(Path.Combine(
                    testRoot,
                    SaveSlotStorage.GetSlotFileName(1))),
                Is.EqualTo("previous-identity"));
            Assert.That(File.Exists(previousIdentityPath), Is.False);
        }

        private string GetCurrentSavePath()
        {
            return Path.Combine(
                testRoot,
                "Measured Field",
                "Nera",
                "nera_save.json");
        }
    }
}
