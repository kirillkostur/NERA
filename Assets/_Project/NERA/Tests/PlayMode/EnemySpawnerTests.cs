using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NERA.Enemies;
using NERA.Items;
using NERA.Quests;
using NERA.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NERA.Tests
{
    public sealed class EnemySpawnerTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [Test]
        public void MissingSpawnerIdIsGeneratedDuringValidation()
        {
            GameObject root = CreateGameObject("Test_EnemySpawner_Id");
            EnemySpawner spawner = root.AddComponent<EnemySpawner>();
            SetPrivateField(spawner, "spawnerId", string.Empty);

            MethodInfo onValidate = typeof(EnemySpawner).GetMethod(
                "OnValidate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(onValidate, Is.Not.Null);
            onValidate.Invoke(spawner, null);

            Assert.That(
                spawner.SpawnerId,
                Does.StartWith("enemy_spawner_"));
        }

        [UnityTest]
        public IEnumerator ManualSpawnerCreatesEnemiesInsideConfiguredRadius()
        {
            IOEnemyController enemyPrefab = CreateEnemyTemplate();
            EnemySpawner spawner = CreateSpawner(
                enemyPrefab,
                "test/manual_wave",
                8,
                4f,
                EnemySpawnerActivationMode.Manual,
                persistWaveState: false);

            int spawned = spawner.SpawnWave();
            yield return null;

            Assert.That(spawned, Is.EqualTo(8));
            Assert.That(spawner.AliveCount, Is.EqualTo(8));
            Assert.That(spawner.IsWaveActive, Is.True);
            IOEnemyController[] enemies = GetSpawnedEnemies(enemyPrefab);
            Assert.That(enemies, Has.Length.EqualTo(8));
            Assert.That(
                enemies.All(enemy => string.IsNullOrEmpty(
                    enemy.PersistentKey)),
                Is.True,
                "Spawner-created enemies must always use runtime identity.");
            Assert.That(
                enemies.All(enemy =>
                    Vector3.Distance(
                        spawner.transform.position,
                        enemy.transform.position) <=
                    spawner.SpawnRadius + 0.2f),
                Is.True);
        }

        [UnityTest]
        public IEnumerator OneShotWaveRestoresOnlySurvivorsAndUnpickedDrops()
        {
            Assert.That(WorldStateController.Instance, Is.Null);
            WorldStateController worldState = CreateGameObject(
                    "WorldStateController")
                .AddComponent<WorldStateController>();

            GameObject dropPrefab = CreateGameObject("Test_DropPrefab");
            WorldItem dropTemplate = dropPrefab.AddComponent<WorldItem>();
            IOEnemyConfig config =
                ScriptableObject.CreateInstance<IOEnemyConfig>();
            createdObjects.Add(config);
            SetPrivateField(config, "deathDropPrefab", dropPrefab);

            IOEnemyController enemyPrefab = CreateEnemyTemplate(config);
            const string spawnerId = "test/persistent_wave";
            EnemySpawner firstSpawner = CreateSpawner(
                enemyPrefab,
                spawnerId,
                2,
                4f,
                EnemySpawnerActivationMode.Manual);

            Assert.That(firstSpawner.SpawnWave(), Is.EqualTo(2));
            yield return null;

            IOEnemyController[] firstWave =
                GetSpawnedEnemies(enemyPrefab)
                    .OrderBy(enemy => enemy.PersistentKey)
                    .ToArray();
            Assert.That(firstWave, Has.Length.EqualTo(2));
            Assert.That(
                firstWave.Select(enemy => enemy.PersistentKey)
                    .Distinct()
                    .ToArray(),
                Has.Length.EqualTo(2));

            string defeatedKey = firstWave[0].PersistentKey;
            string survivorKey = firstWave[1].PersistentKey;
            Vector3 survivorPosition = firstWave[1].transform.position;
            firstWave[0].TakeDamage(float.MaxValue, null);
            yield return null;

            Assert.That(worldState.IsEnemyDefeated(defeatedKey), Is.True);
            Assert.That(GetSpawnedDrops(dropTemplate), Has.Length.EqualTo(1));

            Object.Destroy(firstSpawner.gameObject);
            DestroySpawnedDrops(dropTemplate);
            yield return null;

            EnemySpawner restoredSpawner = CreateSpawner(
                enemyPrefab,
                spawnerId,
                2,
                4f,
                EnemySpawnerActivationMode.Manual);
            yield return null;

            IOEnemyController[] restoredWave =
                GetSpawnedEnemies(enemyPrefab);
            Assert.That(restoredSpawner.AliveCount, Is.EqualTo(1));
            Assert.That(restoredWave, Has.Length.EqualTo(1));
            Assert.That(restoredWave[0].PersistentKey, Is.EqualTo(survivorKey));
            Assert.That(
                restoredWave[0].transform.position.x,
                Is.EqualTo(survivorPosition.x).Within(0.001f));
            Assert.That(
                restoredWave[0].transform.position.z,
                Is.EqualTo(survivorPosition.z).Within(0.001f));

            WorldItem[] restoredDrops = GetSpawnedDrops(dropTemplate);
            Assert.That(restoredDrops, Has.Length.EqualTo(1));
            Assert.That(restoredDrops[0].TracksWorldState, Is.True);
            Assert.That(
                restoredDrops[0].PersistentKey,
                Is.EqualTo(defeatedKey + "/drop"));

            worldState.MarkConsumed(defeatedKey + "/drop");
            Object.Destroy(restoredSpawner.gameObject);
            DestroySpawnedDrops(dropTemplate);
            yield return null;

            EnemySpawner afterPickupSpawner = CreateSpawner(
                enemyPrefab,
                spawnerId,
                2,
                4f,
                EnemySpawnerActivationMode.Manual);
            yield return null;

            Assert.That(afterPickupSpawner.AliveCount, Is.EqualTo(1));
            Assert.That(GetSpawnedDrops(dropTemplate), Is.Empty);
        }

        [UnityTest]
        public IEnumerator TwoQuestsCanReuseTheSameSceneSpawner()
        {
            Assert.That(QuestController.Instance, Is.Null);

            const string spawnerId = "test/quest_wave";
            QuestDefinition firstQuest = CreateQuest(
                "side.enemy_spawner_test_1",
                spawnerId,
                "test/start_enemy_wave_1",
                "test/finish_enemy_wave_1");
            QuestDefinition secondQuest = CreateQuest(
                "side.enemy_spawner_test_2",
                spawnerId,
                "test/start_enemy_wave_2",
                "test/finish_enemy_wave_2");
            QuestCatalog catalog = ScriptableObject.CreateInstance<QuestCatalog>();
            createdObjects.Add(catalog);
            SetPrivateField(
                catalog,
                "definitions",
                new List<QuestDefinition> { firstQuest, secondQuest });

            GameObject controllerObject = CreateGameObject("QuestController");
            QuestController controller =
                controllerObject.AddComponent<QuestController>();
            controller.Configure(catalog);

            IOEnemyController enemyPrefab = CreateEnemyTemplate();
            EnemySpawner spawner = CreateSpawner(
                enemyPrefab,
                spawnerId,
                2,
                3f,
                EnemySpawnerActivationMode.Manual);

            Assert.That(spawner.HasSpawned, Is.False);
            Assert.That(
                controller.Report(
                    QuestSignalType.Custom,
                    "test/start_enemy_wave_1"),
                Is.True);
            Assert.That(spawner.HasSpawned, Is.False);
            Assert.That(
                controller.Report(
                    QuestSignalType.Custom,
                    "test/finish_enemy_wave_1"),
                Is.True);
            Assert.That(spawner.HasSpawned, Is.True);
            Assert.That(spawner.AliveCount, Is.EqualTo(2));
            Assert.That(
                controller.IsCompleted("side.enemy_spawner_test_1"),
                Is.True);

            IOEnemyController[] spawnedEnemies =
                GetSpawnedEnemies(enemyPrefab);
            Assert.That(spawnedEnemies, Has.Length.EqualTo(2));
            foreach (IOEnemyController enemy in spawnedEnemies)
                enemy.TakeDamage(float.MaxValue, null);
            yield return null;

            Assert.That(spawner.IsWaveActive, Is.False);
            Assert.That(spawner.AliveCount, Is.Zero);

            Assert.That(
                controller.Report(
                    QuestSignalType.Custom,
                    "test/start_enemy_wave_2"),
                Is.True);
            Assert.That(
                controller.Report(
                    QuestSignalType.Custom,
                    "test/finish_enemy_wave_2"),
                Is.True);
            Assert.That(spawner.AliveCount, Is.EqualTo(2));
            Assert.That(
                controller.IsCompleted("side.enemy_spawner_test_2"),
                Is.True);
        }

        [UnityTest]
        public IEnumerator StageStartQueuesSpawnerFromAnUnloadedLocation()
        {
            Assert.That(QuestController.Instance, Is.Null);
            Assert.That(WorldStateController.Instance, Is.Null);
            WorldStateController worldState = CreateGameObject(
                    "WorldStateController")
                .AddComponent<WorldStateController>();

            const string questId = "side.remote_enemy_spawner_test";
            const string spawnerId = "remote_location/enemy_easy";
            QuestDefinition quest = CreateQuest(
                questId,
                spawnerId,
                "test/enter_remote_stage",
                "test/finish_remote_stage",
                spawnOnStart: true);
            QuestCatalog catalog = ScriptableObject.CreateInstance<QuestCatalog>();
            createdObjects.Add(catalog);
            SetPrivateField(
                catalog,
                "definitions",
                new List<QuestDefinition> { quest });

            QuestController controller = CreateGameObject("QuestController")
                .AddComponent<QuestController>();
            controller.Configure(catalog);

            Assert.That(
                controller.Report(
                    QuestSignalType.Custom,
                    "test/enter_remote_stage"),
                Is.True);

            SaveGameData saved = new SaveGameData();
            worldState.Capture(saved);
            Assert.That(saved.enemySpawnerWaves, Has.Count.EqualTo(1));
            Assert.That(
                saved.enemySpawnerWaves[0].spawnerId,
                Is.EqualTo(spawnerId));

            IOEnemyController enemyPrefab = CreateEnemyTemplate();
            EnemySpawner loadedLocationSpawner = CreateSpawner(
                enemyPrefab,
                spawnerId,
                2,
                3f,
                EnemySpawnerActivationMode.Manual);
            yield return null;

            Assert.That(loadedLocationSpawner.HasSpawned, Is.True);
            Assert.That(loadedLocationSpawner.AliveCount, Is.EqualTo(2));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = createdObjects.Count - 1; index >= 0; index--)
            {
                if (createdObjects[index] != null)
                    Object.Destroy(createdObjects[index]);
            }

            createdObjects.Clear();
            yield return null;
            Assert.That(QuestController.Instance, Is.Null);
        }

        private EnemySpawner CreateSpawner(
            IOEnemyController enemyPrefab,
            string spawnerId,
            int count,
            float radius,
            EnemySpawnerActivationMode activationMode,
            bool persistWaveState = true)
        {
            GameObject root = CreateGameObject("Test_EnemySpawner");
            root.SetActive(false);
            EnemySpawner spawner = root.AddComponent<EnemySpawner>();
            SetPrivateField(spawner, "spawnerId", spawnerId);
            SetPrivateField(
                spawner,
                "enemyPrefabs",
                new[] { enemyPrefab });
            SetPrivateField(spawner, "spawnCount", count);
            SetPrivateField(spawner, "spawnRadius", radius);
            SetPrivateField(spawner, "snapToGround", false);
            SetPrivateField(spawner, "randomizePrefab", false);
            SetPrivateField(spawner, "spawnedEnemiesRoot", root.transform);
            SetPrivateField(spawner, "activationMode", activationMode);
            SetPrivateField(
                spawner,
                "persistWaveState",
                persistWaveState);
            root.SetActive(true);
            return spawner;
        }

        private IOEnemyController CreateEnemyTemplate(
            IOEnemyConfig config = null)
        {
            GameObject template = CreateGameObject("Test_IOEnemyPrefab");
            template.SetActive(false);
            IOEnemyController enemy =
                template.AddComponent<IOEnemyController>();
            if (config != null)
                SetPrivateField(enemy, "config", config);
            return enemy;
        }

        private IOEnemyController[] GetSpawnedEnemies(
            IOEnemyController template)
        {
            return IOEnemyController.ActiveEnemies
                .Where(enemy => enemy != null && enemy != template)
                .ToArray();
        }

        private static WorldItem[] GetSpawnedDrops(WorldItem template)
        {
            return Object.FindObjectsByType<WorldItem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(item => item != null && item != template)
                .ToArray();
        }

        private static void DestroySpawnedDrops(WorldItem template)
        {
            foreach (WorldItem drop in GetSpawnedDrops(template))
                Object.Destroy(drop.gameObject);
        }

        private QuestDefinition CreateQuest(
            string questId,
            string spawnerId,
            string activationTargetId,
            string completionTargetId,
            bool spawnOnStart = false)
        {
            QuestConditionDefinition activation = CreateCondition(
                QuestSignalType.Custom,
                activationTargetId);
            QuestConditionDefinition completion = CreateCondition(
                QuestSignalType.Custom,
                completionTargetId);

            QuestStageDefinition stage = new QuestStageDefinition();
            SetPrivateField(stage, "title", "Destroy the enemy wave");
            SetPrivateField(
                stage,
                spawnOnStart
                    ? "enemySpawnerIdsOnStart"
                    : "enemySpawnerIdsOnCompletion",
                new List<string> { spawnerId });
            SetPrivateField(
                stage,
                "completionConditions",
                new List<QuestConditionDefinition> { completion });

            QuestDefinition definition =
                ScriptableObject.CreateInstance<QuestDefinition>();
            createdObjects.Add(definition);
            SetPrivateField(definition, "questId", questId);
            SetPrivateField(definition, "category", QuestCategory.Side);
            SetPrivateField(definition, "availability", QuestAvailability.Once);
            SetPrivateField(definition, "targetScope", QuestTargetScope.Single);
            SetPrivateField(definition, "title", "Enemy spawner test");
            SetPrivateField(
                definition,
                "activationConditions",
                new List<QuestConditionDefinition> { activation });
            SetPrivateField(
                definition,
                "stages",
                new List<QuestStageDefinition> { stage });
            return definition;
        }

        private static QuestConditionDefinition CreateCondition(
            QuestSignalType type,
            string targetId)
        {
            QuestConditionDefinition condition =
                new QuestConditionDefinition();
            SetPrivateField(condition, "signalType", type);
            SetPrivateField(
                condition,
                "target",
                QuestConditionTarget.SpecificObject);
            SetPrivateField(condition, "targetId", targetId);
            return condition;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdObjects.Add(gameObject);
            return gameObject;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
