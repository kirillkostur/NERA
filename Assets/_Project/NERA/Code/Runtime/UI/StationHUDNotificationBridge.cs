using NERA.Antenna;
using NERA.Enemies;
using NERA.Drone;
using NERA.Energy;
using NERA.Expeditions;
using NERA.Maintenance;
using NERA.Research;
using NERA.Station;
using NERA.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NERA.UI
{
    public sealed class StationHUDNotificationBridge : MonoBehaviour
    {
        private EnergySystemController energy;
        private StationPowerController power;
        private StationSystemsController systems;
        private DroneScanController drone;
        private AntennaController antenna;
        private ResearchController research;

        private bool energySnapshotReady;
        private bool batteryWasLow;
        private bool powerSnapshotReady;
        private bool systemsSnapshotReady;
        private bool batteryWasEnabled;
        private bool suppressNextBatteryStateNotification;
        private bool silenceNextBatteryRestoreNotification;
        private StationPowerState? suppressedPowerState;
        private int lastPowerTransitionFrame = -1;
        private StationPowerState lastPowerTransitionState;

        private void OnEnable()
        {
            EnergySystemController.InstanceChanged += BindEnergy;
            StationPowerController.InstanceChanged += BindPower;
            StationSystemsController.InstanceChanged += BindSystems;
            DroneScanController.InstanceChanged += BindDrone;
            MaintainableObject.AnyContaminated += HandleObjectContaminated;
            StationWeatherController.AnySandstormStarted +=
                HandleSandstormStarted;
            StationWeatherController.AnySandstormEnded +=
                HandleSandstormEnded;
            SceneManager.sceneLoaded += HandleSceneLoaded;

            BindEnergy(EnergySystemController.Instance);
            BindPower(StationPowerController.Instance);
            BindSystems(StationSystemsController.Instance);
            BindDrone(DroneScanController.Instance);
            BindAntenna(AntennaController.Instance);
            BindResearch(ResearchController.Instance);
        }

        private void Start()
        {
            BindAntenna(AntennaController.Instance);
            BindResearch(ResearchController.Instance);
        }

        private void OnDisable()
        {
            EnergySystemController.InstanceChanged -= BindEnergy;
            StationPowerController.InstanceChanged -= BindPower;
            StationSystemsController.InstanceChanged -= BindSystems;
            DroneScanController.InstanceChanged -= BindDrone;
            MaintainableObject.AnyContaminated -= HandleObjectContaminated;
            StationWeatherController.AnySandstormStarted -=
                HandleSandstormStarted;
            StationWeatherController.AnySandstormEnded -=
                HandleSandstormEnded;
            SceneManager.sceneLoaded -= HandleSceneLoaded;

            BindEnergy(null);
            BindPower(null);
            BindSystems(null);
            BindDrone(null);
            BindAntenna(null);
            BindResearch(null);
        }

        private void HandleSceneLoaded(Scene _, LoadSceneMode __)
        {
            BindAntenna(AntennaController.Instance);
            BindResearch(ResearchController.Instance);
        }

        private static void HandleObjectContaminated(
            MaintainableObject maintainable)
        {
            if (maintainable == null)
                return;

            HUDNotificationService.Publish(
                HUDNotificationIds.StationObjectContaminated,
                maintainable.DisplayName);
        }

        private static void HandleSandstormStarted(float _)
        {
            HUDNotificationService.Publish(HUDNotificationIds.StormStarted);
        }

        private static void HandleSandstormEnded(bool _)
        {
            HUDNotificationService.Publish(HUDNotificationIds.StormEnded);
        }

        private void BindEnergy(EnergySystemController value)
        {
            if (energy == value)
            {
                SnapshotEnergy();
                return;
            }

            if (energy != null)
                energy.EnergyChanged -= HandleEnergyChanged;
            energy = value;
            if (energy != null)
                energy.EnergyChanged += HandleEnergyChanged;
            SnapshotEnergy();
        }

        private void SnapshotEnergy()
        {
            energySnapshotReady = energy != null;
            batteryWasLow = IsBatteryLow(energy);
        }

        private void HandleEnergyChanged()
        {
            if (energy == null)
                return;

            bool isLow = IsBatteryLow(energy);
            if (energySnapshotReady && !energy.IsRestoringState &&
                !batteryWasLow && isLow)
            {
                int thresholdPercent = Mathf.RoundToInt(
                    energy.Config.DefaultConsumerMinimumCharge01 * 100f);
                HUDNotificationService.Publish(
                    HUDNotificationIds.BatteryLow,
                    thresholdPercent);
            }

            batteryWasLow = isLow;
            energySnapshotReady = true;
        }

        private static bool IsBatteryLow(EnergySystemController value)
        {
            return value != null && value.TotalCapacity > 0f &&
                value.Charge01 <=
                value.Config.DefaultConsumerMinimumCharge01;
        }

        private void BindPower(StationPowerController value)
        {
            if (power == value)
            {
                SnapshotPower();
                return;
            }

            if (power != null)
                power.StateChanged -= HandlePowerChanged;
            power = value;
            if (power != null)
                power.StateChanged += HandlePowerChanged;
            SnapshotPower();
        }

        private void SnapshotPower()
        {
            powerSnapshotReady = power != null;
        }

        private void HandlePowerChanged(StationPowerState state)
        {
            bool wasReady = powerSnapshotReady;
            powerSnapshotReady = true;
            lastPowerTransitionFrame = Time.frameCount;
            lastPowerTransitionState = state;

            bool explicitlySuppressed = suppressedPowerState == state;
            bool silentFaultRecovery =
                silenceNextBatteryRestoreNotification &&
                state == StationPowerState.Online;
            if (explicitlySuppressed)
                suppressedPowerState = null;
            if (!wasReady || explicitlySuppressed || silentFaultRecovery ||
                EnergySystemController.Instance?.IsRestoringState == true)
            {
                return;
            }

            HUDNotificationService.Publish(
                state == StationPowerState.Online
                    ? HUDNotificationIds.PowerRestored
                    : HUDNotificationIds.PowerLost);
        }


        private void BindSystems(StationSystemsController value)
        {
            if (systems == value)
            {
                SnapshotSystems();
                return;
            }

            if (systems != null)
            {
                systems.SystemsChanged -= HandleSystemsChanged;
                systems.SystemFaulted -= HandleSystemFaulted;
            }
            systems = value;
            if (systems != null)
            {
                systems.SystemsChanged += HandleSystemsChanged;
                systems.SystemFaulted += HandleSystemFaulted;
            }
            SnapshotSystems();
        }

        private void SnapshotSystems()
        {
            systemsSnapshotReady = systems != null;
            if (systems != null)
            {
                batteryWasEnabled = systems.IsRequestedActive(
                    StationSystemType.Battery);
            }
        }

        private void HandleSystemFaulted(
            StationSystemDefinition definition,
            string _,
            string __,
            GameObject source)
        {
            if (definition == null || source == null ||
                source.GetComponentInParent<IOEnemyController>(true) == null)
            {
                return;
            }

            HUDNotificationService.Publish(
                HUDNotificationIds.StationObjectDisabled,
                definition.DisplayName);

            if (definition.SystemType != StationSystemType.Battery)
                return;

            suppressNextBatteryStateNotification = true;
            silenceNextBatteryRestoreNotification = true;
            suppressedPowerState = StationPowerState.Offline;
        }

        private void HandleSystemsChanged()
        {
            if (systems == null)
                return;

            bool enabled = systems.IsRequestedActive(
                StationSystemType.Battery);
            if (!systemsSnapshotReady || enabled == batteryWasEnabled)
            {
                batteryWasEnabled = enabled;
                systemsSnapshotReady = true;
                return;
            }

            bool suppressHudNotification =
                !enabled && suppressNextBatteryStateNotification ||
                enabled && silenceNextBatteryRestoreNotification;
            if (!enabled)
                suppressNextBatteryStateNotification = false;
            else
                silenceNextBatteryRestoreNotification = false;

            batteryWasEnabled = enabled;
            StationPowerState expectedPowerState = enabled
                ? StationPowerState.Online
                : StationPowerState.Offline;
            bool powerAlreadyReportedThisFrame =
                lastPowerTransitionFrame == Time.frameCount &&
                lastPowerTransitionState == expectedPowerState;

            if (!suppressHudNotification &&
                !powerAlreadyReportedThisFrame &&
                EnergySystemController.Instance?.IsRestoringState != true)
            {
                HUDNotificationService.Publish(
                    enabled
                        ? HUDNotificationIds.BatteryEnabled
                        : HUDNotificationIds.BatteryDisabled);
            }

            if (!powerAlreadyReportedThisFrame && power != null &&
                power.State != expectedPowerState)
            {
                suppressedPowerState = expectedPowerState;
            }
        }

        private void BindDrone(DroneScanController value)
        {
            if (drone == value)
                return;

            if (drone != null)
            {
                drone.StationPresenceChanged -= HandleDronePresenceChanged;
                drone.ScanCompleted -= HandleDroneScanCompleted;
            }
            drone = value;
            if (drone != null)
            {
                drone.StationPresenceChanged += HandleDronePresenceChanged;
                drone.ScanCompleted += HandleDroneScanCompleted;
            }
        }

        private static void HandleDronePresenceChanged(bool isAtStation)
        {
            HUDNotificationService.Publish(
                isAtStation
                    ? HUDNotificationIds.DroneReturned
                    : HUDNotificationIds.DroneDeparted);
        }

        private static void HandleDroneScanCompleted(DroneScanResult result)
        {
            if (result.NewlyDiscovered && result.Location != null)
            {
                HUDNotificationService.Publish(
                    HUDNotificationIds.DroneLocationDiscovered);
                return;
            }

            HUDNotificationService.Publish(
                HUDNotificationIds.DroneNoNewLocations);
        }

        private void BindAntenna(AntennaController value)
        {
            if (antenna == value)
                return;

            if (antenna != null)
            {
                antenna.SignalFound -= HandleAntennaSignalFound;
                antenna.SignalNotFound -= HandleAntennaSignalNotFound;
            }
            antenna = value;
            if (antenna != null)
            {
                antenna.SignalFound += HandleAntennaSignalFound;
                antenna.SignalNotFound += HandleAntennaSignalNotFound;
            }
        }

        private static void HandleAntennaSignalFound(
            ExpeditionLocationData _)
        {
            HUDNotificationService.Publish(
                HUDNotificationIds.AntennaSignalFound);
        }

        private static void HandleAntennaSignalNotFound()
        {
            HUDNotificationService.Publish(
                HUDNotificationIds.AntennaSignalNotFound);
        }

        private void BindResearch(ResearchController value)
        {
            if (research == value)
                return;

            if (research != null)
                research.ResearchAnalyzed -= HandleResearchAnalyzed;
            research = value;
            if (research != null)
                research.ResearchAnalyzed += HandleResearchAnalyzed;
        }

        private void HandleResearchAnalyzed(string researchId)
        {
            ResearchDefinition definition =
                research?.LoadedItem?.ResearchDefinition;
            string displayName = definition != null &&
                definition.ResearchId == researchId
                    ? definition.DisplayName
                    : researchId;
            HUDNotificationService.Publish(
                HUDNotificationIds.ResearchCompleted,
                displayName);
        }
    }
}
