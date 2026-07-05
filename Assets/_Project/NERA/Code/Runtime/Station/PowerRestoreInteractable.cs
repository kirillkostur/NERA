using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PowerRestoreInteractable : BaseInteractable
{
    [Header("Power Restore")]
    [SerializeField] private bool disableAfterRestore = true;

    protected override void OnInteractCompleted()
    {
        if (StationPowerController.Instance == null)
        {
            Debug.LogError($"{name}: StationPowerController not found.");
            return;
        }

        if (StationPowerController.Instance.IsOnline)
        {
            Debug.Log($"{name}: Station power is already online.");

            if (disableAfterRestore)
                SetCanInteract(false);

            return;
        }

        StationPowerController.Instance.RestorePower();

        Debug.Log($"{name}: Station power restored.");


        if (disableAfterRestore)
            SetCanInteract(false);
    }

    private void Start()
    {
        if (StationPowerController.Instance == null)
            return;

        if (StationPowerController.Instance.IsOnline && disableAfterRestore)
            SetCanInteract(false);
    }
}