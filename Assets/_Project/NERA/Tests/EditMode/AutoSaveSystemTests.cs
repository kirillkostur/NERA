using System;
using System.IO;
using NERA.Energy;
using NERA.Save;
using NUnit.Framework;
using UnityEngine;

namespace NERA.Tests
{
    public sealed class AutoSaveSystemTests
    {
        private string testRoot;

        [SetUp]
        public void SetUp()
        {
            testRoot = Path.Combine(
                Path.GetTempPath(),
                "NERA_AutoSaveSystemTests",
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
        public void CheckpointUsesASeparateFileInsideTheSelectedSlot()
        {
            Assert.That(
                SaveSlotStorage.GetCheckpointPath(testRoot, 2),
                Does.EndWith("nera_save_2.checkpoint.json"));
            Assert.That(
                SaveSlotStorage.GetCheckpointBackupPath(testRoot, 2),
                Does.EndWith("nera_save_2.checkpoint.backup.json"));
        }

        [Test]
        public void SaveRootCanBeRedirectedWithoutTouchingPlayerFiles()
        {
            string previous = Environment.GetEnvironmentVariable(
                SaveSlotStorage.SaveRootEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(
                    SaveSlotStorage.SaveRootEnvironmentVariable,
                    testRoot);

                Assert.That(
                    SaveSlotStorage.GetSlotPath(1),
                    Is.EqualTo(Path.Combine(testRoot, "nera_save_1.json")));
            }
            finally
            {
                Environment.SetEnvironmentVariable(
                    SaveSlotStorage.SaveRootEnvironmentVariable,
                    previous);
            }
        }

        [Test]
        public void WorldStateSnapshotRestoresConsumedItemsAndEnemies()
        {
            GameObject root = new GameObject("WorldState_Test");
            WorldStateController worldState =
                root.AddComponent<WorldStateController>();
            try
            {
                worldState.MarkConsumed("Expedition_01/Item_A");
                worldState.MarkEnemyDefeated("Expedition_01/Enemy_A");
                SaveGameData checkpoint = new SaveGameData();
                worldState.Capture(checkpoint);

                worldState.MarkConsumed("Expedition_01/Item_AfterCheckpoint");
                worldState.MarkEnemyDefeated(
                    "Expedition_01/Enemy_AfterCheckpoint");
                worldState.Restore(checkpoint);

                Assert.That(
                    worldState.IsConsumed("Expedition_01/Item_A"),
                    Is.True);
                Assert.That(
                    worldState.IsEnemyDefeated("Expedition_01/Enemy_A"),
                    Is.True);
                Assert.That(
                    worldState.IsConsumed(
                        "Expedition_01/Item_AfterCheckpoint"),
                    Is.False);
                Assert.That(
                    worldState.IsEnemyDefeated(
                        "Expedition_01/Enemy_AfterCheckpoint"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WorldStateSnapshotRollsCustomFlagsBackToCheckpoint()
        {
            GameObject root = new GameObject("WorldFlags_Test");
            WorldStateController worldState =
                root.AddComponent<WorldStateController>();
            try
            {
                worldState.SetWorldFlagCompleted(
                    "Expedition_01/Puzzle_A",
                    true);
                SaveGameData checkpoint = new SaveGameData();
                worldState.Capture(checkpoint);

                worldState.SetWorldFlagCompleted(
                    "Expedition_01/Door_AfterCheckpoint",
                    true);
                worldState.SetWorldFlagCompleted(
                    "Expedition_01/Puzzle_A",
                    false);
                worldState.Restore(checkpoint);

                Assert.That(
                    worldState.IsWorldFlagCompleted(
                        "expedition_01/puzzle_a"),
                    Is.True);
                Assert.That(
                    worldState.IsWorldFlagCompleted(
                        "expedition_01/door_aftercheckpoint"),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PersistentKeysAreCaseAndSlashStable()
        {
            Assert.That(
                PersistentSceneIdentity.Normalize(
                    " Expedition_01\\Loot\\BlueShard "),
                Is.EqualTo("expedition_01/loot/blueshard"));
        }

        [Test]
        public void AuthoredPersistentKeyDoesNotChangeWhenSiblingIsRemoved()
        {
            GameObject parent = new GameObject("LootRoot");
            GameObject first = new GameObject("FirstLoot");
            GameObject second = new GameObject("SecondLoot");
            first.transform.SetParent(parent.transform);
            second.transform.SetParent(parent.transform);
            string keyBeforeRemoval = PersistentSceneIdentity.CreateKey(
                second.transform,
                "loot-7d7167f83e2b42f397a41d08cba83cc1");

            UnityEngine.Object.DestroyImmediate(first);

            Assert.That(
                PersistentSceneIdentity.CreateKey(
                    second.transform,
                    "loot-7d7167f83e2b42f397a41d08cba83cc1"),
                Is.EqualTo(keyBeforeRemoval));
            UnityEngine.Object.DestroyImmediate(parent);
        }

        [Test]
        public void MissingAuthoredPersistentIdDoesNotUseHierarchyFallback()
        {
            GameObject tracked = new GameObject("TrackedObject");
            try
            {
                Assert.That(
                    PersistentSceneIdentity.CreateKey(tracked.transform),
                    Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tracked);
            }
        }

        [Test]
        public void BackupRotationKeepsIndependentOrderedGenerations()
        {
            string primary = SaveSlotStorage.GetSlotPath(testRoot, 2);
            File.WriteAllText(primary, "version-1");
            SaveSlotStorage.RotateBackups(testRoot, 2, 3);

            File.WriteAllText(primary, "version-2");
            SaveSlotStorage.RotateBackups(testRoot, 2, 3);

            Assert.That(
                File.ReadAllText(
                    SaveSlotStorage.GetBackupPath(testRoot, 2, 1)),
                Is.EqualTo("version-2"));
            Assert.That(
                File.ReadAllText(
                    SaveSlotStorage.GetBackupPath(testRoot, 2, 2)),
                Is.EqualTo("version-1"));
            Assert.That(
                File.Exists(
                    SaveSlotStorage.GetBackupPath(testRoot, 1, 1)),
                Is.False);
        }

        [Test]
        public void LoadCandidatesNeverRenumberSelectedSlot()
        {
            string[] candidates = SaveSlotStorage.GetLoadCandidates(
                testRoot,
                3,
                3);

            Assert.That(candidates[0], Does.EndWith("nera_save_3.json"));
            Assert.That(
                candidates[1],
                Does.EndWith("nera_save_3.backup_1.json"));
            Assert.That(
                candidates[2],
                Does.EndWith("nera_save_3.backup_2.json"));
            Assert.That(
                candidates[3],
                Does.EndWith("nera_save_3.backup_3.json"));
        }

        [Test]
        public void CheckpointMetadataAndWorldStateRoundTripThroughJson()
        {
            SaveGameData source = new SaveGameData
            {
                checkpointSceneName = "Expedition_02",
                checkpointSpawnPointId = "quest/find_relay",
                checkpointUsesWorldPose = true,
                checkpointPositionX = 12.5f,
                checkpointPositionY = 3f,
                checkpointPositionZ = -7.25f,
                checkpointRotationY = 0.7071068f,
                checkpointRotationW = 0.7071068f,
                hasDroneBatteryCharge = true,
                droneBatteryCharge = 42.5f
            };
            source.consumedWorldObjectIds.Add("expedition_02/loot_a");
            source.defeatedEnemyObjectIds.Add("expedition_02/enemy_a");
            source.completedWorldFlagIds.Add("expedition_02/puzzle_a");

            SaveGameData restored = JsonUtility.FromJson<SaveGameData>(
                JsonUtility.ToJson(source));

            Assert.That(restored.version, Is.EqualTo(19));
            Assert.That(restored.hasDroneBatteryCharge, Is.True);
            Assert.That(restored.droneBatteryCharge, Is.EqualTo(42.5f));
            Assert.That(
                restored.checkpointSceneName,
                Is.EqualTo("Expedition_02"));
            Assert.That(
                restored.checkpointSpawnPointId,
                Is.EqualTo("quest/find_relay"));
            Assert.That(restored.checkpointUsesWorldPose, Is.True);
            Assert.That(restored.checkpointPositionX, Is.EqualTo(12.5f));
            Assert.That(restored.checkpointPositionY, Is.EqualTo(3f));
            Assert.That(restored.checkpointPositionZ, Is.EqualTo(-7.25f));
            Assert.That(
                restored.consumedWorldObjectIds,
                Is.EquivalentTo(new[] { "expedition_02/loot_a" }));
            Assert.That(
                restored.defeatedEnemyObjectIds,
                Is.EquivalentTo(new[] { "expedition_02/enemy_a" }));
            Assert.That(
                restored.completedWorldFlagIds,
                Is.EquivalentTo(new[] { "expedition_02/puzzle_a" }));
        }

        [Test]
        public void StationChargeChangeMarksDirtyWithoutChangingEnergyState()
        {
            GameObject root = new GameObject("EnergyAutoSave_Test");
            try
            {
                EnergySystemController energy =
                    root.AddComponent<EnergySystemController>();
                AutoSaveService autoSave =
                    root.AddComponent<AutoSaveService>();

                energy.RegisterBattery("station_battery", 1000f, 1000f);
                energy.RestoreState(750f, true);
                autoSave.InitializeSession();

                Assert.That(energy.State, Is.EqualTo(EnergyState.Normal));
                Assert.That(
                    energy.TrySpendEnergy(25f),
                    Is.True,
                    "The configured grid should allow spending station energy.");
                Assert.That(energy.State, Is.EqualTo(EnergyState.Normal));
                Assert.That(
                    autoSave.IsDirty,
                    Is.True,
                    "Changing charge inside one EnergyState must dirty the save.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
