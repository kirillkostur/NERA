using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NERA.Enemies;
using NERA.Maintenance;
using NERA.Station;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NERA.Tests
{
    public sealed class IOAbilityPlayModeTests
    {
        private readonly List<Object> createdObjects =
            new List<Object>();

        [UnityTest]
        public IEnumerator RegeneratorPulseHealsAfterItsInterval()
        {
            IOEnemyController enemy =
                CreateEnemy<IORegenerationPulseAbility>(80f);
            IORegenerationPulseAbility ability =
                enemy.GetComponent<IORegenerationPulseAbility>();
            SetField(ability, "pulseInterval", 10f);
            SetField(ability, "telegraphDuration", 0f);
            SetField(ability, "healAmount", 18f);

            enemy.gameObject.SetActive(true);
            enemy.TakeDamage(20f, null);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(60f));
            SetField(ability, "nextPulseAt", 0f);

            yield return null;
            yield return null;

            Assert.That(enemy.CurrentHealth, Is.EqualTo(78f));
        }

        [UnityTest]
        public IEnumerator HunterFiresThreeShotBurst()
        {
            IOEnemyController enemy =
                CreateEnemy<IOHunterBurstAbility>(130f);
            enemy.gameObject.SetActive(true);
            Transform target = CreateObject("HunterTarget").transform;
            target.position = new Vector3(0f, 0f, 20f);

            enemy.GetComponent<IOHunterBurstAbility>()
                .TickAttack(target);
            yield return new WaitForSeconds(0.45f);

            IOEnergyProjectile[] activeProjectiles =
                Object.FindObjectsByType<IOEnergyProjectile>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            Assert.That(activeProjectiles.Length, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator EnforcerShotCarriesExplosionRadius()
        {
            IOEnemyController enemy =
                CreateEnemy<IOExplosiveShotAbility>(220f);
            enemy.gameObject.SetActive(true);
            Transform target = CreateObject("EnforcerTarget").transform;
            target.position = new Vector3(0f, 0f, 20f);

            enemy.GetComponent<IOExplosiveShotAbility>()
                .TickAttack(target);
            yield return null;

            IOEnergyProjectile projectile =
                Object.FindObjectsByType<IOEnergyProjectile>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Single();
            Assert.That(
                GetField<float>(projectile, "explosionRadius"),
                Is.EqualTo(3.5f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator OverseerSummonsTwoNoLootReinforcements()
        {
            IOEnemyController blueTemplate =
                CreatePassiveEnemy(30f);
            blueTemplate.gameObject.name = "BlueTemplate";
            blueTemplate.gameObject.SetActive(true);
            IOEnemyController yellowTemplate =
                CreatePassiveEnemy(130f);
            yellowTemplate.gameObject.name = "YellowTemplate";
            yellowTemplate.gameObject.SetActive(true);

            IOEnemyController overseer =
                CreateEnemy<IOOverseerSummonAbility>(400f);
            IOOverseerSummonAbility ability =
                overseer.GetComponent<IOOverseerSummonAbility>();
            SetField(
                ability,
                "reinforcementPrefabs",
                new[]
                {
                    blueTemplate.gameObject,
                    yellowTemplate.gameObject
                });
            overseer.gameObject.SetActive(true);

            overseer.TakeDamage(150f, null);
            yield return null;

            IOEnemyController[] summoned =
                IOEnemyController.ActiveEnemies
                    .Where(enemy =>
                        enemy != null &&
                        enemy.name.EndsWith("_Summoned"))
                    .ToArray();
            Assert.That(summoned, Has.Length.EqualTo(2));
            foreach (IOEnemyController enemy in summoned)
            {
                Assert.That(
                    GetField<bool>(enemy, "runtimeDropsEnabled"),
                    Is.False);
                Assert.That(enemy.PersistentKey, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator IOProjectileDoesNotContaminateStationTurret()
        {
            IOEnemyController source = CreatePassiveEnemy(30f);
            source.transform.position = new Vector3(-2f, 0f, 0f);

            GameObject target = CreateObject("Test_StationTurret");
            target.transform.position = new Vector3(0f, 0f, 2f);
            target.AddComponent<BoxCollider>();
            MaintainableObject maintenance =
                target.AddComponent<MaintainableObject>();
            target.AddComponent<StationTurretController>();

            GameObject projectileObject = CreateObject("Test_IOProjectile");
            IOEnergyProjectile projectile =
                projectileObject.AddComponent<IOEnergyProjectile>();
            projectile.Initialize(
                Vector3.forward,
                20f,
                25f,
                1f,
                source.gameObject);
            Physics.SyncTransforms();

            yield return new WaitForSeconds(0.25f);

            Assert.That(
                maintenance.Condition,
                Is.EqualTo(1f).Within(0.001f),
                "IO projectile hits must not turn station damage into sand.");
            Object.Destroy(target);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (IOEnemyController enemy in
                     Object.FindObjectsByType<IOEnemyController>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (enemy != null)
                    Object.Destroy(enemy.gameObject);
            }

            foreach (IOProjectilePool pool in
                     Object.FindObjectsByType<IOProjectilePool>(
                         FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (pool != null)
                    Object.Destroy(pool.gameObject);
            }

            foreach (Object created in createdObjects)
            {
                if (created != null && !(created is GameObject))
                    Object.Destroy(created);
            }
            createdObjects.Clear();
            yield return null;
        }

        private IOEnemyController CreateEnemy<TAbility>(float health)
            where TAbility : IOEnemyAbility
        {
            GameObject root = CreateObject(
                "Test_" + typeof(TAbility).Name);
            root.SetActive(false);
            root.AddComponent<SphereCollider>();

            IOEnemyConfig config =
                ScriptableObject.CreateInstance<IOEnemyConfig>();
            createdObjects.Add(config);
            SetField(config, "maxHealth", health);
            SetField(config, "detectionRadius", 20f);
            SetField(config, "attackRange", 15f);
            SetField(config, "attackCooldown", 0.5f);
            SetField(config, "projectileSpeed", 4f);
            SetField(config, "projectileLifetime", 3f);
            SetField(config, "projectileDamage", 7f);
            SetField(config, "projectileScale", 0.2f);

            IOEnemyController enemy =
                root.AddComponent<IOEnemyController>();
            SetField(enemy, "config", config);
            root.AddComponent<TAbility>();
            return enemy;
        }

        private GameObject CreateObject(string name)
        {
            GameObject result = new GameObject(name);
            createdObjects.Add(result);
            return result;
        }

        private static void SetField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private static T GetField<T>(
            object target,
            string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (T)field.GetValue(target);
        }

private IOEnemyController CreatePassiveEnemy(float health)
        {
            GameObject root = CreateObject("Test_PassiveEnemy");
            root.SetActive(false);
            root.AddComponent<SphereCollider>();

            IOEnemyConfig config =
                ScriptableObject.CreateInstance<IOEnemyConfig>();
            createdObjects.Add(config);
            SetField(config, "maxHealth", health);

            IOEnemyController enemy =
                root.AddComponent<IOEnemyController>();
            SetField(enemy, "config", config);
            return enemy;
        }
}
}
