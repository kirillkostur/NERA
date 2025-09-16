using UnityEngine;

public class PlayerTargeting : MonoBehaviour
{
    [Header("Настройки таргета")]
    public float targetSearchRadius = 10f;
    public LayerMask targetMask;

    [Header("Скорость поворота")]
    public float rotationSpeed = 8f;

    [Header("Сброс таргета")]
    [Tooltip("Задержка перед сбросом цели после начала движения (сек).")]
    public float moveClearDelay = 0.2f;

    [Tooltip("Насколько дальше радиуса поиска держать цель, прежде чем забыть.")]
    public float forgetDistanceExtra = 1f;

    private ITargetable currentTarget;
    private PlayerController controller;

    private float moveStartTime = -1f;

    void Start()
    {
        controller = GetComponent<PlayerController>();
    }

    void Update()
    {
        bool isMoving = controller != null && controller.IsMoving();

        // Если начали двигаться — запускаем таймер и через короткую задержку сбрасываем цель.
        if (isMoving)
        {
            if (moveStartTime < 0f) moveStartTime = Time.time;

            if (currentTarget != null && Time.time - moveStartTime >= moveClearDelay)
                ClearTarget();

            // во время движения не разворачиваемся на цель
            return;
        }
        else
        {
            // стоим — сбрасываем таймер движения
            moveStartTime = -1f;
        }

        // Валидируем текущую цель: жива ли и не слишком ли далеко
        if (currentTarget != null)
        {
            if (!currentTarget.IsAlive())
            {
                ClearTarget();
            }
            else
            {
                float dist = Vector3.Distance(transform.position, currentTarget.GetTransform().position);
                if (dist > targetSearchRadius + forgetDistanceExtra)
                    ClearTarget();
            }
        }

        // Если цели нет — ищем новую
        if (currentTarget == null)
            FindNewTarget();

        // Поворот к цели (на месте)
        if (currentTarget != null)
        {
            Vector3 dir = currentTarget.GetTransform().position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRot,
                    rotationSpeed * Time.deltaTime
                );
            }
        }
    }

    void FindNewTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, targetSearchRadius, targetMask);

        if (hits.Length > 0)
        {
            ITargetable closest = null;
            float closestDist = Mathf.Infinity;

            foreach (var h in hits)
            {
                ITargetable t = h.GetComponent<ITargetable>();
                if (t != null && t.IsAlive())
                {
                    float dist = Vector3.Distance(transform.position, h.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = t;
                    }
                }
            }
            currentTarget = closest;
        }
    }

    public void ClearTarget()
    {
        currentTarget = null;
    }

    public Transform GetCurrentTarget() => currentTarget?.GetTransform();

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, targetSearchRadius);
    }
}
