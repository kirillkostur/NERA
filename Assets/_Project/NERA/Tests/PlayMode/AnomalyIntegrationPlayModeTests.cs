using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NERA.Combat;
using NERA.Enemies;
using NERA.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NERA.Tests
{
    public sealed class AnomalyIntegrationPlayModeTests
    {
        private readonly List<Object> createdObjects =
            new List<Object>();
        private static readonly Vector3 TestOrigin =
            new Vector3(800f, 20f, 800f);

        [UnityTest]
        public IEnumerator BlueTemporarilyPowersNearbyDevice()
        {
            PlayerEnergyWeaponController weapon =
                CreateWeaponController(TestOrigin);
            AnomalyElectronicDevice device =
                CreateElectronic(
                    TestOrigin + Vector3.forward * 3f,
                    false);

            yield return null;

            AnomalyIntegrationDefinition definition =
                CreateDefinition(
                    AnomalyIntegrationEffect.EnableElectronics,
                    8f,
                    0f,
                    0.05f);
            Assert.That(
                weapon.TryActivateIntegration(definition),
                Is.True);
            Assert.That(device.IsPowered, Is.True);

            yield return new WaitForSeconds(0.08f);
            Assert.That(device.IsPowered, Is.False);
        }

        [UnityTest]
        public IEnumerator GreenRestoresPlayerHealthToOneHundredPercent()
        {
            PlayerEnergyWeaponController weapon =
                CreateWeaponController(TestOrigin);
            PlayerHealth health =
                CreatePlayerHealth(weapon.gameObject);

            yield return null;

            health.TakeDamage(65f, null);
            Assert.That(health.CurrentHealth, Is.EqualTo(35f));

            AnomalyIntegrationDefinition definition =
                CreateDefinition(
                    AnomalyIntegrationEffect.RestoreFullHealth,
                    8f,
                    0f,
                    0f);
            Assert.That(
                weapon.TryActivateIntegration(definition),
                Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(100f));
        }

        [UnityTest]
        public IEnumerator YellowCreatesThroughWallRevealMarker()
        {
            PlayerEnergyWeaponController weapon =
                CreateWeaponController(TestOrigin);
            GameObject target = CreateObject("ScanTarget");
            target.transform.position =
                TestOrigin + Vector3.forward * 5f;
            target.AddComponent<BoxCollider>();
            target.AddComponent<BaseInteractable>();

            yield return null;
            Physics.SyncTransforms();

            AnomalyIntegrationDefinition definition =
                CreateDefinition(
                    AnomalyIntegrationEffect.RevealThroughWalls,
                    10f,
                    0f,
                    6f);
            Assert.That(
                weapon.TryActivateIntegration(definition),
                Is.True);

            AnomalyScanRevealController scanner =
                weapon.GetComponent<AnomalyScanRevealController>();
            Assert.That(scanner, Is.Not.Null);
            Assert.That(scanner.ActiveMarkerCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RedDealsFormerBlueAreaDamage()
        {
            PlayerEnergyWeaponController weapon =
                CreateWeaponController(TestOrigin);
            IOEnemyController enemy =
                CreateEnemy(
                    TestOrigin + Vector3.forward * 3f,
                    100f);

            yield return null;
            Physics.SyncTransforms();

            AnomalyIntegrationDefinition definition =
                CreateDefinition(
                    AnomalyIntegrationEffect.DamageAnomalies,
                    8f,
                    40f,
                    0f);
            Assert.That(
                weapon.TryActivateIntegration(definition),
                Is.True);
            Assert.That(enemy.CurrentHealth, Is.EqualTo(60f));
        }

        [UnityTest]
        public IEnumerator VioletPermanentlyDisablesAndDamagesEnemies()
        {
            PlayerEnergyWeaponController weapon =
                CreateWeaponController(TestOrigin);
            AnomalyElectronicDevice device =
                CreateElectronic(
                    TestOrigin + Vector3.forward * 11f,
                    true);
            IOEnemyController enemy =
                CreateEnemy(
                    TestOrigin + Vector3.right * 11f,
                    250f);

            yield return null;
            Physics.SyncTransforms();

            AnomalyIntegrationDefinition definition =
                CreateDefinition(
                    AnomalyIntegrationEffect
                        .DisableElectronicsPermanently,
                    12.5f,
                    400f,
                    0f);
            Assert.That(
                weapon.TryActivateIntegration(definition),
                Is.True);
            Assert.That(device.IsPowered, Is.False);
            Assert.That(enemy.IsAlive, Is.False);
            Assert.That(enemy.CurrentHealth, Is.Zero);

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object created in createdObjects)
            {
                if (created != null)
                    Object.Destroy(created);
            }

            createdObjects.Clear();
            yield return null;
        }

        private PlayerEnergyWeaponController CreateWeaponController(
            Vector3 position)
        {
            GameObject root = CreateObject("IntegrationTestPlayer");
            root.transform.position = position;
            root.SetActive(false);
            PlayerEnergyWeaponController weapon =
                root.AddComponent<PlayerEnergyWeaponController>();
            root.SetActive(true);
            return weapon;
        }

        private PlayerHealth CreatePlayerHealth(GameObject root)
        {
            bool wasActive = root.activeSelf;
            root.SetActive(false);

            GameObject ragdoll = new GameObject("TestRagdollBody");
            ragdoll.transform.SetParent(root.transform, false);
            ragdoll.AddComponent<SphereCollider>();
            ragdoll.AddComponent<Rigidbody>();

            PlayerHealth health = root.AddComponent<PlayerHealth>();
            SetField(health, "ragdollRoot", ragdoll.transform);
            SetField(health, "maxHealth", 100f);

            root.SetActive(wasActive);
            return health;
        }

        private AnomalyElectronicDevice CreateElectronic(
            Vector3 position,
            bool initiallyPowered)
        {
            GameObject root = CreateObject("TestElectronic");
            root.SetActive(false);
            root.transform.position = position;
            root.AddComponent<BoxCollider>();
            AnomalyElectronicDevice device =
                root.AddComponent<AnomalyElectronicDevice>();
            SetField(
                device,
                "initiallyPowered",
                initiallyPowered);
            root.SetActive(true);
            return device;
        }

        private IOEnemyController CreateEnemy(
            Vector3 position,
            float health)
        {
            GameObject root = CreateObject("TestIOEnemy");
            root.SetActive(false);
            root.transform.position = position;
            root.AddComponent<SphereCollider>();

            IOEnemyConfig config =
                ScriptableObject.CreateInstance<IOEnemyConfig>();
            createdObjects.Add(config);
            SetField(config, "maxHealth", health);
            SetField(config, "detectionRadius", 0.1f);
            SetField(config, "attackRange", 0.1f);

            IOEnemyController enemy =
                root.AddComponent<IOEnemyController>();
            SetField(enemy, "config", config);
            root.SetActive(true);
            return enemy;
        }

        private AnomalyIntegrationDefinition CreateDefinition(
            AnomalyIntegrationEffect effect,
            float radius,
            float damage,
            float duration)
        {
            AnomalyIntegrationDefinition definition =
                ScriptableObject
                    .CreateInstance<AnomalyIntegrationDefinition>();
            createdObjects.Add(definition);
            SetField(definition, "effect", effect);
            SetField(definition, "radius", radius);
            SetField(definition, "anomalyDamage", damage);
            SetField(definition, "electronicDuration", duration);
            SetField(
                definition,
                "affectedLayers",
                (LayerMask)(~0));
            return definition;
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
    }
}
