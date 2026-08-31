using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NERA.Combat;
using NERA.Enemies;
using NERA.Station;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NERA.Tests
{
    public sealed class IOPowerDisruptionPlayModeTests
    {
        private readonly List<Object> createdObjects =
            new List<Object>();
        private static readonly Vector3 Origin =
            new Vector3(1100f, 20f, 1100f);
        private StationSystemsController stationSystems;
        private bool ownsStationSystems;

        [UnityTest]
        public IEnumerator BlueNeverActivatesPlayerStationObjects()
        {
            PlayerEnergyWeaponController weapon =
                CreateWeaponController();
            AnomalyElectronicDevice stationDevice =
                CreateElectronic(
                    Origin + Vector3.forward * 3f,
                    false,
                    true);

            yield return null;
            Physics.SyncTransforms();

            Assert.That(
                weapon.TryActivateIntegration(
                    CreateDefinition(
                        AnomalyIntegrationEffect.EnableElectronics,
                        8f,
                        8f)),
                Is.True);
            Assert.That(stationDevice.IsPowered, Is.False);
        }

        [UnityTest]
        public IEnumerator EnemyPulseCancelsBlueAndDeviceStaysOff()
        {
            PlayerEnergyWeaponController weapon =
                CreateWeaponController();
            AnomalyElectronicDevice device =
                CreateElectronic(
                    Origin + Vector3.forward * 3f,
                    false,
                    false);

            yield return null;
            Physics.SyncTransforms();

            Assert.That(
                weapon.TryActivateIntegration(
                    CreateDefinition(
                        AnomalyIntegrationEffect.EnableElectronics,
                        8f,
                        0.15f)),
                Is.True);
            Assert.That(device.IsPowered, Is.True);

            IOPowerDisruptionAbility ability =
                CreateDisruptor(
                    Origin + Vector3.right * 2f,
                    8f,
                    0.01f);
            yield return new WaitForSeconds(0.04f);

            Assert.That(ability.CastCount, Is.EqualTo(1));
            Assert.That(device.IsPowered, Is.False);

            yield return new WaitForSeconds(0.18f);
            Assert.That(
                device.IsPowered,
                Is.False,
                "The cancelled Blue charge must not restore the device.");
        }

        [UnityTest]
        public IEnumerator VioletIntegrationDisablesStationTurretState()
        {
            StationSystemsController systems = EnsureStationSystems();
            StationObjectIdentity turret =
                CreateStationTurret(
                    Origin + Vector3.forward * 3f);
            Assert.That(
                systems.ForceSetRequestedActiveForDebug(
                    StationSystemType.Turret,
                    true,
                    turret.ObjectId),
                Is.True);

            PlayerEnergyWeaponController weapon =
                CreateWeaponController();
            yield return null;
            Physics.SyncTransforms();

            Assert.That(
                weapon.TryActivateIntegration(
                    CreateDefinition(
                        AnomalyIntegrationEffect
                            .DisableElectronicsPermanently,
                        12.5f,
                        0f)),
                Is.True);
            Assert.That(
                systems.IsRequestedActive(
                    StationSystemType.Turret,
                    turret.ObjectId),
                Is.False);
        }

        [UnityTest]
        public IEnumerator EnemyPulseDisablesStationTurretState()
        {
            StationSystemsController systems = EnsureStationSystems();
            StationObjectIdentity turret =
                CreateStationTurret(
                    Origin + Vector3.forward * 3f);
            Assert.That(
                systems.ForceSetRequestedActiveForDebug(
                    StationSystemType.Turret,
                    true,
                    turret.ObjectId),
                Is.True);

            IOPowerDisruptionAbility ability =
                CreateDisruptor(
                    Origin + Vector3.right * 2f,
                    8f,
                    0.01f);
            yield return new WaitForSeconds(0.04f);

            Assert.That(ability.CastCount, Is.EqualTo(1));
            Assert.That(
                systems.IsRequestedActive(
                    StationSystemType.Turret,
                    turret.ObjectId),
                Is.False);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (stationSystems != null && !ownsStationSystems)
            {
                stationSystems.ForceSetRequestedActiveForDebug(
                    StationSystemType.Turret,
                    true,
                    "station_turret_01");
            }

            foreach (Object created in createdObjects)
            {
                if (created != null)
                    Object.Destroy(created);
            }

            createdObjects.Clear();
            stationSystems = null;
            ownsStationSystems = false;
            yield return null;
        }

        private PlayerEnergyWeaponController CreateWeaponController()
        {
            GameObject root = CreateObject("PowerPulseTestPlayer");
            root.transform.position = Origin;
            root.SetActive(false);
            PlayerEnergyWeaponController weapon =
                root.AddComponent<PlayerEnergyWeaponController>();
            root.SetActive(true);
            return weapon;
        }

        private AnomalyElectronicDevice CreateElectronic(
            Vector3 position,
            bool initiallyPowered,
            bool stationObject)
        {
            GameObject root = CreateObject("PowerPulseTestDevice");
            root.SetActive(false);
            root.transform.position = position;
            root.AddComponent<BoxCollider>();
            if (stationObject)
            {
                StationObjectIdentity identity =
                    root.AddComponent<StationObjectIdentity>();
                identity.Configure(
                    StationSystemType.Turret,
                    "station_turret_01");
            }

            AnomalyElectronicDevice device =
                root.AddComponent<AnomalyElectronicDevice>();
            SetField(
                device,
                "initiallyPowered",
                initiallyPowered);
            root.SetActive(true);
            return device;
        }

        private StationObjectIdentity CreateStationTurret(
            Vector3 position)
        {
            GameObject root = CreateObject("PowerPulseTestTurret");
            root.transform.position = position;
            root.AddComponent<BoxCollider>();
            StationObjectIdentity identity =
                root.AddComponent<StationObjectIdentity>();
            identity.Configure(
                StationSystemType.Turret,
                "station_turret_01");
            return identity;
        }

        private IOPowerDisruptionAbility CreateDisruptor(
            Vector3 position,
            float radius,
            float initialDelay)
        {
            GameObject root = CreateObject("PowerPulseTestIO");
            root.SetActive(false);
            root.transform.position = position;
            root.AddComponent<SphereCollider>();

            IOEnemyConfig config =
                ScriptableObject.CreateInstance<IOEnemyConfig>();
            createdObjects.Add(config);
            SetField(config, "maxHealth", 100f);
            SetField(config, "detectionRadius", 0.1f);
            SetField(config, "attackRange", 0.1f);

            IOEnemyController enemy =
                root.AddComponent<IOEnemyController>();
            SetField(enemy, "config", config);
            IOPowerDisruptionAbility ability =
                root.AddComponent<IOPowerDisruptionAbility>();
            SetField(ability, "initialDelay", initialDelay);
            SetField(ability, "cooldown", 30f);
            SetField(ability, "radius", radius);
            SetField(
                ability,
                "affectedLayers",
                (LayerMask)(~0));
            root.SetActive(true);
            return ability;
        }

        private StationSystemsController EnsureStationSystems()
        {
            stationSystems = StationSystemsController.Instance;
            if (stationSystems != null)
                return stationSystems;

            GameObject root = CreateObject("PowerPulseStationSystems");
            stationSystems =
                root.AddComponent<StationSystemsController>();
            ownsStationSystems = true;
            return stationSystems;
        }

        private AnomalyIntegrationDefinition CreateDefinition(
            AnomalyIntegrationEffect effect,
            float radius,
            float duration)
        {
            AnomalyIntegrationDefinition definition =
                ScriptableObject
                    .CreateInstance<AnomalyIntegrationDefinition>();
            createdObjects.Add(definition);
            SetField(definition, "effect", effect);
            SetField(definition, "radius", radius);
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
