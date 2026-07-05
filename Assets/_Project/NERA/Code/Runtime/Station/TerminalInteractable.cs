using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TerminalInteractable : BaseInteractable
{
    [Header("Terminal")]
    [SerializeField] private bool requirePower = true;
    [SerializeField] private string offlineMessage = "Terminal is offline. Restore station power first.";
    [SerializeField] private string openedMessage = "Terminal opened.";

    protected override void OnInteractCompleted()
    {
        if (requirePower)
        {
            if (StationPowerController.Instance == null)
            {
                Debug.LogError($"{name}: StationPowerController not found.");
                return;
            }

            if (!StationPowerController.Instance.IsOnline)
            {
                Debug.Log($"{name}: {offlineMessage}");
                return;
            }
        }

        if (TerminalUI.Instance == null)
        {
            Debug.LogWarning($"{name}: TerminalUI not found. Fallback log only.");
            Debug.Log($"{name}: {openedMessage}");
            return;
        }

        TerminalUI.Instance.Open(transform);

        Debug.Log($"{name}: {openedMessage}");
    }
}