using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [Header("Ссылки на компоненты игрока")]
    public PlayerController playerController;
    public PlayerTargeting playerTargeting;
    public PlayerAttack playerAttack;

    private bool isDead = false;

    private void Awake()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (playerTargeting == null) playerTargeting = GetComponent<PlayerTargeting>();
        if (playerAttack == null) playerAttack = GetComponent<PlayerAttack>();
    }

    public void OnPlayerDeath()
    {
        if (isDead) return;
        isDead = true;

        Transform lastTarget = null;
        if (playerTargeting != null)
        {
            lastTarget = playerTargeting.CurrentTargetTransform;
            playerTargeting.ClearTarget();
            playerTargeting.enabled = false;
        }

        // ⛔ Сбрасываем ремонт, если был
        if (lastTarget != null)
        {
            var repairable = lastTarget.GetComponent<RepairableObject>();
            if (repairable != null)
                repairable.CancelInteract();

            if (repairable != null && repairable.targetHighlightEffect != null)
                repairable.targetHighlightEffect.SetActive(false);
        }

        if (playerController != null) playerController.enabled = false;
        if (playerAttack != null) playerAttack.enabled = false;

        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
    }


    private void ForceClearAllHighlights()
    {
#if UNITY_2023_1_OR_NEWER
        var spiders = FindObjectsByType<SpiderAI_NavMesh>(FindObjectsSortMode.None);
        var reps = FindObjectsByType<RepairableObject>(FindObjectsSortMode.None);
#else
        var spiders = FindObjectsOfType<SpiderAI_NavMesh>();
        var reps    = FindObjectsOfType<RepairableObject>();
#endif
        foreach (var r in reps)
        {
            if (r != null && r.targetHighlightEffect != null)
                r.targetHighlightEffect.SetActive(false);
        }
    }
}
