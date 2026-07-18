using System;
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

        public bool RestorePower()
        {
            if (IsPowered)
                return false;

            SetState(StationPowerState.Online);
            Debug.Log("StationPowerController: Station power restored.", this);
            return true;
        }

        public void SetState(StationPowerState newState)
        {
            if (State == newState)
                return;

            State = newState;
            StateChanged?.Invoke(State);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
