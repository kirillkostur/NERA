using System;
using NERA.Energy;
using NERA.Quests;
using UnityEngine;

namespace NERA.Station
{
    public sealed class StationPowerController : MonoBehaviour
    {
        [SerializeField] private StationPowerState initialState = StationPowerState.Offline;

        private StationPowerState? lastReportedQuestState;
        private EnergySystemController subscribedEnergy;

        public static StationPowerController Instance { get; private set; }
        public static event Action<StationPowerController> InstanceChanged;

        public event Action<StationPowerState> StateChanged;

        public StationPowerState State { get; private set; }
        public bool IsPowered => State == StationPowerState.Online;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            State = initialState;
            EnergySystemController.InstanceChanged += HandleEnergyInstanceChanged;
            InstanceChanged?.Invoke(this);
        }

        private void Start()
        {
            BindEnergy(EnergySystemController.Instance, true);
        }

        public bool RestorePower()
        {
            if (IsPowered)
                return false;

            EnergySystemController energy = EnergySystemController.Instance;
            if (energy != null)
            {
                energy.SetGridEnabled(true);
                SyncFromEnergy(energy);
            }
            else
            {
                SetState(StationPowerState.Online);
            }

            if (!IsPowered)
                return false;

            Debug.Log("StationPowerController: Station power restored.", this);
            return true;
        }

        public void SetState(StationPowerState newState)
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy != null)
                energy.SetGridEnabled(newState == StationPowerState.Online);

            if (State == newState)
            {
                ReportQuestState(State, true);
                return;
            }

            State = newState;
            StateChanged?.Invoke(State);
            ReportQuestState(State);
        }

        private void HandleEnergyStateChanged(EnergyState _)
        {
            EnergySystemController energy = EnergySystemController.Instance;
            SyncFromEnergy(
                energy,
                energy != null && energy.IsRestoringState);
        }

        private void HandleEnergyInstanceChanged(EnergySystemController energy)
        {
            BindEnergy(energy, true);
        }

        private void BindEnergy(
            EnergySystemController energy,
            bool synchronizeOnly)
        {
            if (subscribedEnergy == energy)
            {
                if (energy != null)
                    SyncFromEnergy(energy, synchronizeOnly);
                return;
            }

            if (subscribedEnergy != null)
                subscribedEnergy.StateChanged -= HandleEnergyStateChanged;
            subscribedEnergy = energy;
            if (subscribedEnergy != null)
            {
                subscribedEnergy.StateChanged += HandleEnergyStateChanged;
                SyncFromEnergy(subscribedEnergy, synchronizeOnly);
            }
            else
            {
                ReportQuestState(State, true);
            }
        }

        private void SyncFromEnergy(
            EnergySystemController energy,
            bool synchronizeOnly = false)
        {
            if (energy == null)
                return;

            StationPowerState target =
                energy.HasUsablePower
                    ? StationPowerState.Online
                    : StationPowerState.Offline;

            if (State == target)
            {
                if (synchronizeOnly)
                    ReportQuestState(State, true);
                return;
            }

            State = target;
            StateChanged?.Invoke(State);
            ReportQuestState(State, synchronizeOnly);
        }

        private void ReportQuestState(
            StationPowerState state,
            bool synchronizeOnly = false)
        {
            QuestController quests = QuestController.Instance;
            if (quests == null)
            {
                return;
            }

            QuestSignalType signalType = state == StationPowerState.Online
                ? QuestSignalType.StationPowerOnline
                : QuestSignalType.StationPowerOffline;
            if (synchronizeOnly)
            {
                quests.SynchronizeState(
                    signalType,
                    "station_power",
                    "Station Power");
                return;
            }

            if (lastReportedQuestState == state)
                return;

            lastReportedQuestState = state;
            quests.Report(signalType, "station_power", "Station Power");
        }

        private void OnDestroy()
        {
            EnergySystemController.InstanceChanged -= HandleEnergyInstanceChanged;
            if (subscribedEnergy != null)
                subscribedEnergy.StateChanged -= HandleEnergyStateChanged;

            if (Instance == this)
            {
                Instance = null;
                InstanceChanged?.Invoke(null);
            }
        }
    }
}
