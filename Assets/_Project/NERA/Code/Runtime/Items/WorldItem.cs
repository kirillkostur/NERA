using UnityEngine;

[RequireComponent(typeof(Collider))]
public class WorldItem : BaseInteractable
{
    [Header("Item")]
    [SerializeField] private ItemData itemData;

    [Header("Pickup")]
    [SerializeField] private bool destroyAfterPickup = true;
    [SerializeField] private bool disableAfterPickup = true;

    [Header("Session State")]
    [SerializeField] private bool hideIfAlreadyFound = true;
    [SerializeField] private bool disableColliderWhenAlreadyFound = true;
    [SerializeField] private bool disableRendererWhenAlreadyFound = true;

    private void Start()
    {
        ApplySessionState();
    }

    private void ApplySessionState()
    {
        if (!hideIfAlreadyFound)
            return;

        if (itemData == null)
            return;

        if (GameSessionState.Instance == null)
            return;

        if (!GameSessionState.Instance.IsItemFound(itemData.ItemId))
            return;

        Debug.Log($"{name}: Item '{itemData.ItemId}' already found. Hiding world item.");

        SetCanInteract(false);

        if (disableColliderWhenAlreadyFound)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = false;
        }

        if (disableRendererWhenAlreadyFound)
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
                renderers[i].enabled = false;
        }
    }

    protected override void OnInteractCompleted()
    {
        if (itemData == null)
        {
            Debug.LogError($"{name}: ItemData is missing.");
            return;
        }

        PlayerInventory playerInventory = FindPlayerInventory();

        if (playerInventory == null)
        {
            Debug.LogError($"{name}: PlayerInventory not found.");
            return;
        }

        bool added = playerInventory.TryAddItem(itemData);

        if (!added)
        {
            Debug.LogWarning($"{name}: Could not pick up '{itemData.GetItemName()}'.");
            return;
        }

        Debug.Log($"{name}: Picked up '{itemData.GetItemName()}'.");

        if (GameSessionState.Instance != null)
            GameSessionState.Instance.MarkItemFound(itemData.ItemId);

        ObjectiveTargetItem objectiveTarget = GetComponent<ObjectiveTargetItem>();

        if (objectiveTarget != null)
            objectiveTarget.CompleteObjective();

        if (disableAfterPickup)
            SetCanInteract(false);

        if (destroyAfterPickup)
            Destroy(gameObject);
    }

    private PlayerInventory FindPlayerInventory()
    {
        if (PersistentPlayer.Instance != null)
            return PersistentPlayer.Instance.GetComponent<PlayerInventory>();

        PlayerInventory inventory = FindFirstObjectByType<PlayerInventory>();

        if (inventory != null)
            return inventory;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null)
            return player.GetComponent<PlayerInventory>();

        return null;
    }
}