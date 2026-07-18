using System;
using System.Collections;
using NERA.Core;
using NERA.Drone;
using NERA.Energy;
using NERA.Expeditions;
using NERA.Inventory;
using NERA.Research;
using NERA.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NERA.Tests
{
    public sealed class StationEnergySceneLifecycleTests
    {
        [UnityTearDown]
        public IEnumerator TearDownPersistentBootRoot()
        {
            BootInitializer boot =
                Object.FindFirstObjectByType<BootInitializer>();
            if (boot != null)
                Object.Destroy(boot.gameObject);

            yield return null;
        }

        [UnityTest]
        public IEnumerator LaboratoryIsUnavailableUntilGridStarts()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            LaboratoryTableInteractable laboratory =
                UnityEngine.Object.FindFirstObjectByType<LaboratoryTableInteractable>();

            Assert.That(energy, Is.Not.Null);
            Assert.That(laboratory, Is.Not.Null);

            energy.RestoreState(energy.TotalCapacity, false);
            Assert.That(laboratory.GetPrompt().IsAvailable, Is.False);

            energy.SetGridEnabled(true);
            Assert.That(laboratory.GetPrompt().IsAvailable, Is.True);
        }

        [UnityTest]
        public IEnumerator ReturningToStationDoesNotDuplicateEnergySources()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            StationEnvironmentController environment =
                StationEnvironmentController.Instance;

            Assert.That(energy, Is.Not.Null);
            Assert.That(environment, Is.Not.Null);

            environment.SetTime(12f);
            environment.SetWeather(StationWeather.Clear);
            energy.AdvanceSimulation(0.1f);

            Assert.That(energy.TotalCapacity, Is.EqualTo(1000f));
            Assert.That(
                energy.CurrentGeneration,
                Is.EqualTo(energy.Config.ClearDayGeneration).Within(0.01f)
            );

            SceneManager.LoadScene("Expedition_01");
            yield return WaitForScene("Expedition_01");

            SceneManager.LoadScene("Player_Station");
            yield return WaitForScene("Player_Station");
            yield return null;

            environment.SetTime(12f);
            environment.SetWeather(StationWeather.Clear);
            energy.AdvanceSimulation(0.1f);

            Assert.That(energy.TotalCapacity, Is.EqualTo(1000f));
            Assert.That(
                energy.CurrentGeneration,
                Is.EqualTo(energy.Config.ClearDayGeneration).Within(0.01f)
            );
        }

        [UnityTest]
        public IEnumerator DroneCanSurveySecondLocationAfterRecharge()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            EnergySystemController energy = EnergySystemController.Instance;
            ExpeditionDiscoveryController discovery =
                ExpeditionDiscoveryController.Instance;
            DroneScanController drone = DroneScanController.Instance;

            Assert.That(energy, Is.Not.Null);
            Assert.That(discovery, Is.Not.Null);
            Assert.That(drone, Is.Not.Null);
            Assert.That(discovery.KnownLocations.Count, Is.GreaterThanOrEqualTo(2));

            ExpeditionLocationData first = discovery.KnownLocations[0];
            ExpeditionLocationData second = discovery.KnownLocations[1];
            discovery.RestoreDiscovered(Array.Empty<string>());
            energy.RestoreState(energy.TotalCapacity, true);
            yield return null;

            Assert.That(drone.LaunchScan(first), Is.True);
            drone.AdvanceScan(first.DroneScanDuration);
            Assert.That(discovery.IsDiscovered(first), Is.True);
            Assert.That(drone.IsCharging, Is.True);
            Assert.That(drone.CanLaunchScan(second), Is.False);

            drone.AdvanceRecharge(energy.Config.DroneRechargeDuration);

            Assert.That(drone.State, Is.EqualTo(DroneState.Ready));
            Assert.That(drone.IsCharging, Is.False);
            Assert.That(drone.LaunchScan(second), Is.True);
            Assert.That(drone.ScanLocation, Is.EqualTo(second));
        }

        [UnityTest]
        public IEnumerator BackpackUsesConfiguredAuthoredSlotPoints()
        {
            SceneManager.LoadScene("Boot");
            yield return WaitForScene("Player_Station");
            yield return null;
            yield return DisablePersistenceForTest();

            InventoryLabHUDController hud = InventoryLabHUDController.Instance;
            PlayerInventory inventory =
                Object.FindFirstObjectByType<PlayerInventory>();

            Assert.That(hud, Is.Not.Null);
            Assert.That(inventory, Is.Not.Null);
            Assert.That(inventory.Config, Is.Not.Null);

            Transform content = hud.transform.Find(
                "InventoryPanel/Backpack/Scroll View/Viewport/Content"
            );
            Assert.That(content, Is.Not.Null);

            for (int i = 0; i < InventoryConfig.MaxBackpackCapacity; i++)
            {
                Transform spawnPoint = content.Find($"Slot_{i + 1}");
                Assert.That(spawnPoint, Is.Not.Null);
                Assert.That(
                    spawnPoint.gameObject.activeSelf,
                    Is.EqualTo(i < inventory.BackpackCapacity)
                );

                if (i < inventory.BackpackCapacity)
                {
                    Assert.That(
                        spawnPoint.GetComponentInChildren<InventorySlotView>(true),
                        Is.Not.Null
                    );
                }
            }
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            while (SceneManager.GetActiveScene().name != sceneName)
                yield return null;
        }

        private static IEnumerator DisablePersistenceForTest()
        {
            SaveGameController save =
                Object.FindFirstObjectByType<SaveGameController>();
            if (save != null)
                Object.Destroy(save);

            yield return null;
        }
    }
}
