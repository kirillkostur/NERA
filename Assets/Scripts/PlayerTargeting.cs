using UnityEngine;

public class PlayerTargeting : MonoBehaviour
{
    [Header("Настройки таргета")]
    public float targetSearchRadius = 10f;
    public LayerMask targetMask;

    [Header("Скорость поворота")]
    public float rotationSpeed = 8f;

    private ITargetable currentTarget;
    private PlayerController controller;

    void Start()
    {
        controller = GetComponent<PlayerController>();
    }

    void Update()
    {
        // ВРЕМЕННО отключаем сброс при движении
        // if (controller != null && controller.IsMoving()) { ClearTarget(); return; }

        if (currentTarget == null || !currentTarget.IsAlive())
            FindNewTarget();

        if (currentTarget != null)
        {
            Vector3 dir = currentTarget.GetTransform().position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation,
                    targetRot,
                    rotationSpeed * Time.deltaTime);
                Debug.Log("Поворачиваемся к: " + currentTarget.GetTransform().name);
            }
        }
    }


    void FindNewTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, targetSearchRadius, targetMask);
        Debug.Log("Overlap найдено: " + hits.Length);

        if (hits.Length > 0)
        {
            ITargetable closest = null;
            float closestDist = Mathf.Infinity;

            foreach (var h in hits)
            {
                Debug.Log("Проверяем: " + h.name);
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
            if (currentTarget != null)
                Debug.Log("Выбран таргет: " + currentTarget.GetTransform().name);
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
