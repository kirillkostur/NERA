using System;
using NERA.Energy;
using UnityEngine;

namespace NERA.Station
{
    public sealed class StationPowerController : MonoBehaviour
    {
        [SerializeField] private StationPowerState initialState = StationPowerState.Offline;

        public static StationPowerController Instance { get; private set; }

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
        }

        private void Start()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null)
                return;

            energy.StateChanged += HandleEnergyStateChanged;
            SyncFromEnergy(energy);
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
                return;

            State = newState;
            StateChanged?.Invoke(State);
        }

        private void HandleEnergyStateChanged(EnergyState _)
        {
            SyncFromEnergy(EnergySystemController.Instance);
        }

        private void SyncFromEnergy(EnergySystemController energy)
        {
            if (energy == null)
                return;

            StationPowerState target =
                energy.HasUsablePower
                    ? StationPowerState.Online
                    : StationPowerState.Offline;

            if (State == target)
                return;

            State = target;
            StateChanged?.Invoke(State);
        }

        private void OnDestroy()
        {
            if (EnergySystemController.Instance != null)
                EnergySystemController.Instance.StateChanged -= HandleEnergyStateChanged;

            if (Instance == this)
                Instance = null;
        }
    }
}
