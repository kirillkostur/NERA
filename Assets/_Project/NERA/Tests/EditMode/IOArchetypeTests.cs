using System.Collections.Generic;
using System.Linq;
using NERA.Enemies;
using NERA.Items;
using NERA.Library;
using NERA.Localization;
using NERA.Research;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace NERA.Tests
{
    public sealed class IOArchetypeTests
    {
        public sealed class Expected
        {
            public string Color;
            public string EnemyId;
            public string ItemId;
            public string ResearchId;
            public string AbilityType;
            public float Health;
        }

        private static readonly Expected[] Archetypes =
        {
            new Expected
            {
                Color = "Green",
                EnemyId = "io_green_regenerator",
                ItemId = "io_green_node_02",
                ResearchId = "research_io_green_node_02",
                AbilityType = nameof(IORegenerationPulseAbility),
                Health = 80f
            },
            new Expected
            {
                Color = "Yellow",
                EnemyId = "io_yellow_hunter",
                ItemId = "io_yellow_lens_03",
                ResearchId = "research_io_yellow_lens_03",
                AbilityType = nameof(IOHunterBurstAbility),
                Health = 130f
            },
            new Expected
            {
                Color = "Red",
                EnemyId = "io_red_enforcer",
                ItemId = "io_red_core_04",
                ResearchId = "research_io_red_core_04",
                AbilityType = nameof(IOExplosiveShotAbility),
                Health = 220f
            },
            new Expected
            {
                Color = "Violet",
                EnemyId = "io_violet_overseer",
                ItemId = "io_violet_core_05",
                ResearchId = "research_io_violet_core_05",
                AbilityType = nameof(IOOverseerSummonAbility),
                Health = 400f
            }
        };

        [Test]
        public void AllFiveEnemyIdsAreUnique()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:IOEnemyConfig",
                new[] { "Assets/_Project/NERA/Configs/IO" });
            List<string> ids = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<IOEnemyConfig>)
                .Where(config => config != null)
                .Select(config => config.EnemyId)
                .ToList();

            Assert.That(ids, Does.Contain("io_blue_weak"));
            foreach (Expected expected in Archetypes)
                Assert.That(ids, Does.Contain(expected.EnemyId));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
        }

        [TestCaseSource(nameof(Archetypes))]
        public void ArchetypeContentChainIsComplete(Expected expected)
        {
            IOEnemyConfig config =
                LoadById<IOEnemyConfig>("enemyId", expected.EnemyId);
            ItemData item =
                LoadById<ItemData>("itemId", expected.ItemId);
            ResearchDefinition research =
                LoadById<ResearchDefinition>(
                    "researchId",
                    expected.ResearchId);
            LibraryEntryData library =
                LoadById<LibraryEntryData>(
                    "entryId",
                    expected.ItemId);

            Assert.That(config, Is.Not.Null, expected.EnemyId);
            Assert.That(item, Is.Not.Null, expected.ItemId);
            Assert.That(research, Is.Not.Null, expected.ResearchId);
            Assert.That(library, Is.Not.Null, expected.ItemId);
            Assert.That(config.MaxHealth, Is.EqualTo(expected.Health));
            Assert.That(config.DeathDropPrefab, Is.Not.Null);
            Assert.That(item.WorldPrefab, Is.Not.Null);
            Assert.That(item.ResearchDefinition, Is.SameAs(research));
            Assert.That(research.UnlockedEntry, Is.SameAs(library));
            Assert.That(
                config.DeathDropPrefab.GetComponent<WorldItem>(),
                Is.SameAs(item.WorldPrefab));

            WorldItem worldItem =
                config.DeathDropPrefab.GetComponent<WorldItem>();
            Assert.That(worldItem.ItemData, Is.SameAs(item));
            Rigidbody body = worldItem.GetComponent<Rigidbody>();
            Assert.That(body, Is.Not.Null);
            Assert.That(body.isKinematic, Is.True);
            Assert.That(body.useGravity, Is.False);
        }

        [TestCaseSource(nameof(Archetypes))]
        public void EnemyPrefabHasConfiguredAbilityAndVisual(
            Expected expected)
        {
            string path =
                "Assets/_Project/NERA/Prefabs/IO/IO_" +
                expected.Color + "_" +
                RoleFor(expected.Color) + ".prefab";
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);

            IOEnemyController enemy =
                prefab.GetComponent<IOEnemyController>();
            Assert.That(enemy, Is.Not.Null);
            Assert.That(enemy.Config, Is.Not.Null);
            Assert.That(enemy.Config.EnemyId, Is.EqualTo(expected.EnemyId));
            Assert.That(
                prefab.GetComponents<IOEnemyAbility>()
                    .Select(ability => ability.GetType().Name),
                Does.Contain(expected.AbilityType));
            Assert.That(
                prefab.GetComponentsInChildren<Renderer>(true).Length,
                Is.GreaterThan(0));
            Assert.That(prefab.GetComponent<Collider>(), Is.Not.Null);
        }

        [Test]
        public void OverseerHasTwoValidReinforcementPrefabs()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/NERA/Prefabs/IO/IO_Violet_Overseer.prefab");
            IOOverseerSummonAbility ability =
                prefab.GetComponent<IOOverseerSummonAbility>();
            SerializedObject serialized = new SerializedObject(ability);
            SerializedProperty reinforcements =
                serialized.FindProperty("reinforcementPrefabs");

            Assert.That(reinforcements.arraySize, Is.EqualTo(2));
            for (int index = 0; index < reinforcements.arraySize; index++)
            {
                Assert.That(
                    reinforcements.GetArrayElementAtIndex(index)
                        .objectReferenceValue,
                    Is.Not.Null);
            }
        }

        [TestCaseSource(nameof(Archetypes))]
        public void ArchetypeContentIsLocalizedInEnglishAndRussian(
            Expected expected)
        {
            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(
                    NERALocalization.ContentTable);
            StringTable english =
                collection.GetTable("en") as StringTable;
            StringTable russian =
                collection.GetTable("ru") as StringTable;
            string[] keys =
            {
                "enemy." + expected.EnemyId + ".name",
                "item." + expected.ItemId + ".name",
                "item." + expected.ItemId + ".description",
                "research." + expected.ResearchId + ".name",
                "library." + expected.ItemId + ".title",
                "library." + expected.ItemId + ".description"
            };

            foreach (string key in keys)
            {
                string en = english.GetEntry(key)?.Value;
                string ru = russian.GetEntry(key)?.Value;
                Assert.That(en, Is.Not.Null.And.Not.Empty, key);
                Assert.That(ru, Is.Not.Null.And.Not.Empty, key);
                Assert.That(ru, Is.Not.EqualTo(en), key);
            }
        }

        private static string RoleFor(string color)
        {
            switch (color)
            {
                case "Green": return "Regenerator";
                case "Yellow": return "Hunter";
                case "Red": return "Enforcer";
                default: return "Overseer";
            }
        }

        private static T LoadById<T>(
            string propertyName,
            string expectedId) where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets(
                         "t:" + typeof(T).Name,
                         new[] { "Assets/_Project/NERA/Configs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset == null)
                    continue;

                SerializedObject serialized = new SerializedObject(asset);
                SerializedProperty id =
                    serialized.FindProperty(propertyName);
                if (id != null && id.stringValue == expectedId)
                    return asset;
            }

            return null;
        }
    }
}
