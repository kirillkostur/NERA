using System;
using UnityEngine;

[DisallowMultipleComponent]
public class StationPowerController : MonoBehaviour
{
    public static StationPowerController Instance { get; private set; }

    [Header("Power State")]
    [SerializeField] private StationPowerState initialState = StationPowerState.Offline;
    [SerializeField] private StationPowerState currentState = StationPowerState.Offline;

    public StationPowerState CurrentState => currentState;
    public bool IsOnline => currentState == StationPowerState.Online;

    public event Action<StationPowerState> PowerStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"StationPowerController duplicate destroyed: {name}");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentState = initialState;

        Debug.Log($"StationPowerController initialized. State: {currentState}");
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RestorePower()
    {
        SetPowerState(StationPowerState.Online);
    }

    public void ShutdownPower()
    {
        SetPowerState(StationPowerState.Offline);
    }

    public void SetPowerState(StationPowerState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;

        Debug.Log($"StationPowerController: Power state changed to {currentState}");

        PowerStateChanged?.Invoke(currentState);
    }
}