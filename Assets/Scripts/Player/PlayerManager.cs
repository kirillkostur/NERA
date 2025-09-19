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

        // 1) сохраняем ссылку на последнюю цель, затем снимаем таргет и выключаем систему
        Transform lastTarget = null;
        if (playerTargeting != null)
        {
            lastTarget = playerTargeting.CurrentTargetTransform;
            playerTargeting.ClearTarget();      // теперь блокирует новые назначения
            playerTargeting.enabled = false;
        }

        // 2) гасим подсветку у последней цели (если вдруг осталась)
        if (lastTarget != null)
        {
            var spider = lastTarget.GetComponent<SpiderAI_NavMesh>();
            if (spider != null && spider.targetHighlightEffect != null)
                spider.targetHighlightEffect.SetActive(false);

            var repairable = lastTarget.GetComponent<RepairableObject>();
            if (repairable != null && repairable.targetHighlightEffect != null)
                repairable.targetHighlightEffect.SetActive(false);
        }

        // 3) отключаем управление и атаку
        if (playerController != null) playerController.enabled = false;
        if (playerAttack != null) playerAttack.enabled = false;

        // 4) страхуемся: выключаем CharacterController
        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;

        // 5) чтобы нас не «видели» системы поиска
        gameObject.layer = LayerMask.NameToLayer("IgnoreRaycast");
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
        foreach (var s in spiders)
        {
            if (s != null && s.targetHighlightEffect != null)
                s.targetHighlightEffect.SetActive(false);
        }
        foreach (var r in reps)
        {
            if (r != null && r.targetHighlightEffect != null)
                r.targetHighlightEffect.SetActive(false);
        }
    }
}
