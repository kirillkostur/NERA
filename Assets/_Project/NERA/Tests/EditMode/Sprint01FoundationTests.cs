using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using NeraInteractionMode = NERA.Interaction.InteractionMode;
using NERA.Drone;
using NERA.Combat;
using NERA.Enemies;
using NERA.Expeditions;
using NERA.Station;

namespace NERA.Tests
{
    public sealed class Sprint01FoundationTests
    {
        private static readonly string[] RequiredBuildScenes =
        {
            "Assets/_Project/NERA/Scenes/Boot/Boot.unity",
            "Assets/_Project/NERA/Scenes/Station/Player_Station.unity",
            "Assets/_Project/NERA/Scenes/Expeditions/Expedition_01.unity"
        };

        [Test]
        public void RequiredScenesAreEnabledInBuildSettings()
        {
            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            foreach (string requiredScene in RequiredBuildScenes)
            {
                Assert.That(
                    enabledScenes,
                    Does.Contain(requiredScene),
                    $"Required scene is missing or disabled: {requiredScene}"
                );
            }
        }

        [Test]
        public void InteractionModesContainPressAndHold()
        {
            Assert.That(
                NeraInteractionMode.Press,
                Is.Not.EqualTo(NeraInteractionMode.Hold)
            );
        }
    }

    public sealed class Sprint02StationPowerTests
    {
        private GameObject root;
        private StationPowerController power;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Test_StationPower");
            power = root.AddComponent<StationPowerController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        [Test]
        public void RestorePowerTransitionsOfflineToOnlineAndRaisesEvent()
        {
            StationPowerState? observedState = null;
            power.StateChanged += state => observedState = state;

            bool restored = power.RestorePower();

            Assert.That(restored, Is.True);
            Assert.That(power.State, Is.EqualTo(StationPowerState.Online));
            Assert.That(power.IsPowered, Is.True);
            Assert.That(observedState, Is.EqualTo(StationPowerState.Online));
        }

        [Test]
        public void RestorePowerIsIdempotent()
        {
            Assert.That(power.RestorePower(), Is.True);
            Assert.That(power.RestorePower(), Is.False);
            Assert.That(power.State, Is.EqualTo(StationPowerState.Online));
        }
    }

    public sealed class Sprint03DroneStateTests
    {
        private GameObject root;
        private StationPowerController power;
        private ExpeditionDiscoveryController discovery;
        private DroneScanController drone;
        private ExpeditionLocationData location;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Test_DroneState");
            power = root.AddComponent<StationPowerController>();
            discovery = root.AddComponent<ExpeditionDiscoveryController>();
            drone = root.AddComponent<DroneScanController>();
            location = ScriptableObject.CreateInstance<ExpeditionLocationData>();
            SetPrivateField(drone, "stationPower", power);
            SetPrivateField(drone, "discovery", discovery);

            SerializedObject locationObject = new SerializedObject(location);
            locationObject.FindProperty("locationId").stringValue = "Test_Expedition";
            locationObject.FindProperty("droneScanDuration").floatValue = 2f;
            locationObject.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(location);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void DroneUnlocksWhenStationPowerComesOnline()
        {
            Assert.That(drone.State, Is.EqualTo(DroneState.Locked));

            power.RestorePower();
            drone.RefreshAvailability();

            Assert.That(drone.State, Is.EqualTo(DroneState.Ready));
        }

        [Test]
        public void DroneCannotLaunchWithoutConfiguredLocation()
        {
            power.RestorePower();
            drone.RefreshAvailability();

            Assert.That(drone.LaunchScan(), Is.False);
            Assert.That(drone.State, Is.EqualTo(DroneState.Ready));
        }

        [Test]
        public void DroneScanDiscoversConfiguredLocation()
        {
            SerializedObject droneObject = new SerializedObject(drone);
            droneObject.FindProperty("scanLocation").objectReferenceValue = location;
            droneObject.ApplyModifiedPropertiesWithoutUndo();

            power.RestorePower();
            drone.RefreshAvailability();

            Assert.That(drone.LaunchScan(), Is.True);
            Assert.That(drone.State, Is.EqualTo(DroneState.Scanning));

            drone.AdvanceScan(1f);
            Assert.That(drone.ScanProgress, Is.EqualTo(0.5f).Within(0.001f));

            drone.AdvanceScan(1f);
            Assert.That(drone.State, Is.EqualTo(DroneState.ScanComplete));
            Assert.That(discovery.IsDiscovered(location), Is.True);
        }

        private static void SetPrivateField(
            DroneScanController target,
            string fieldName,
            object value
        )
        {
            FieldInfo field = typeof(DroneScanController).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }

    public sealed class Sprint04IOEnemyTests
    {
        [Test]
        public void PlayerHealthReceivesEnergyDamage()
        {
            GameObject player = new GameObject("Test_Player");
            PlayerHealth health = player.AddComponent<PlayerHealth>();
            health.RestoreFullHealth();

            health.TakeDamage(25f, null);

            Assert.That(health.IsAlive, Is.True);
            Assert.That(health.CurrentHealth, Is.EqualTo(75f));
            Object.DestroyImmediate(player);
        }

        [Test]
        public void BlueIOImplementsDamageableContract()
        {
            GameObject enemy = new GameObject("Test_BlueIO");
            IOEnemyController controller =
                enemy.AddComponent<IOEnemyController>();

            Assert.That(controller, Is.InstanceOf<IDamageable>());
            Assert.That(controller.IsAlive, Is.True);
            Object.DestroyImmediate(enemy);
        }
    }
}
