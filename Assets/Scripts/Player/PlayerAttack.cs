using UnityEngine;

/// <summary>
/// Атака игрока через анимационные события.
/// </summary>
[RequireComponent(typeof(PlayerTargeting))]
[RequireComponent(typeof(Animator))]
public class PlayerAttack : MonoBehaviour
{
    [Header("Настройки атаки")]
    public int damage = 15;                // Урон по пауку
    public float attackCooldown = 0.8f;    // Время между атаками
    public string attackTrigger = "Attack";// Имя триггера анимации атаки

    private PlayerTargeting targeting;
    private Animator animator;
    private Transform cachedTarget;  // кешируем цель, чтобы ударить именно её
    private float nextAttackTime;

    private void Awake()
    {
        targeting = GetComponent<PlayerTargeting>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        // Запускаем анимацию атаки по ЛКМ, но урон не наносим здесь
        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
        {
            Transform target = GetCurrentSpider();
            if (target != null)
            {
                cachedTarget = target; // сохраняем текущего паука
                if (animator != null && !string.IsNullOrEmpty(attackTrigger))
                    animator.SetTrigger(attackTrigger);

                nextAttackTime = Time.time + attackCooldown;
            }
        }
    }

    /// <summary>
    /// Этот метод вызывается АНИМАЦИОННЫМ СОБЫТИЕМ на ударном кадре.
    /// </summary>
    public void DealDamage()
    {
        if (cachedTarget == null) return;

        SpiderHealth health = cachedTarget.GetComponent<SpiderHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
        }
    }

    /// <summary>
    /// Получает Transform текущего паука-таргета.
    /// </summary>
    private Transform GetCurrentSpider()
    {
        var field = typeof(PlayerTargeting).GetField("currentTarget",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        ITargetable currentTarget = field != null ? field.GetValue(targeting) as ITargetable : null;

        if (currentTarget == null) return null;
        Transform targetTransform = currentTarget.GetTransform();
        if (targetTransform == null) return null;

        SpiderAI_NavMesh spider = targetTransform.GetComponent<SpiderAI_NavMesh>();
        if (spider != null && spider.IsAlive())
            return targetTransform;

        return null;
    }
}
