using UnityEngine;
using System.Collections;

/// <summary>
/// Управление выбором ремонтируемых объектов и взаимодействием с ними.
/// Также теперь подсвечивает пауков при выборе.
/// </summary>
public class PlayerTargeting : MonoBehaviour
{
    [Header("Настройки таргета")]
    public float targetSearchRadius = 8f;
    public LayerMask targetMask;
    public float searchInterval = 0.15f;

    [Header("Поворот")]
    public float rotationSpeed = 120f;

    [Header("Сброс таргета")]
    public float moveClearDelay = 0.2f;
    public float forgetDistanceExtra = 1.5f;

    [Header("Взаимодействие")]
    public float interactRange = 3.5f;

    private ITargetable currentTarget;
    private PlayerController controller;
    private float moveStartTime = -1f;
    private IInteractable currentInteractable;

    private const int MaxColliders = 20;
    private readonly Collider[] hitBuffer = new Collider[MaxColliders];

    private void Start()
    {
        controller = GetComponent<PlayerController>();
        StartCoroutine(SearchRoutine());
    }

    private void Update()
    {
        bool isMoving = controller != null && controller.IsMoving();

        if (isMoving)
        {
            if (moveStartTime < 0f) moveStartTime = Time.time;
            if (currentTarget != null && Time.time - moveStartTime >= moveClearDelay)
            {
                CancelInteraction();
                ClearTarget();
            }
            return;
        }
        else moveStartTime = -1f;

        if (currentTarget != null)
        {
            if (!currentTarget.IsAlive() ||
                Vector3.Distance(transform.position, currentTarget.GetTransform().position) > targetSearchRadius + forgetDistanceExtra)
            {
                CancelInteraction();
                ClearTarget();
            }
        }

        if (currentTarget != null)
        {
            RotateTowardsTarget();

            float dist = Vector3.Distance(transform.position, currentTarget.GetTransform().position);
            if (dist <= interactRange)
            {
                currentInteractable = currentTarget.GetTransform().GetComponent<IInteractable>();

                if (Input.GetKeyDown(KeyCode.E) && currentInteractable != null)
                    currentInteractable.StartInteract(gameObject);
                else if (Input.GetKey(KeyCode.E) && currentInteractable != null)
                    currentInteractable.HoldInteract();
                else if (Input.GetKeyUp(KeyCode.E) || !Input.GetKey(KeyCode.E))
                    CancelInteraction();
            }
            else CancelInteraction();
        }
        else CancelInteraction();
    }

    private IEnumerator SearchRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(searchInterval);
        while (true)
        {
            if (currentTarget == null) FindNewTarget();
            yield return wait;
        }
    }

    private void RotateTowardsTarget()
    {
        Vector3 dir = currentTarget.GetTransform().position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    private void CancelInteraction()
    {
        if (currentInteractable != null)
        {
            currentInteractable.CancelInteract();
            currentInteractable = null;
        }
    }

    private void FindNewTarget()
    {
        int hits = Physics.OverlapSphereNonAlloc(transform.position, targetSearchRadius, hitBuffer, targetMask);
        if (hits == 0) return;

        ITargetable closest = null;
        float closestDist = Mathf.Infinity;

        for (int i = 0; i < hits; i++)
        {
            var h = hitBuffer[i];
            if (h == null) continue;

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
        SetTarget(closest);
    }

    private void SetTarget(ITargetable target)
    {
        // Отключаем подсветку старой цели
        if (currentTarget != null)
        {
            var oldRepairable = currentTarget.GetTransform().GetComponent<RepairableObject>();
            if (oldRepairable != null && oldRepairable.targetHighlightEffect != null)
                oldRepairable.targetHighlightEffect.SetActive(false);

            var oldSpider = currentTarget.GetTransform().GetComponent<SpiderAI_NavMesh>();
            if (oldSpider != null && oldSpider.targetHighlightEffect != null)
                oldSpider.targetHighlightEffect.SetActive(false);
        }

        currentTarget = target;

        // Включаем подсветку новой цели
        if (currentTarget != null)
        {
            var newRepairable = currentTarget.GetTransform().GetComponent<RepairableObject>();
            if (newRepairable != null && newRepairable.targetHighlightEffect != null)
                newRepairable.targetHighlightEffect.SetActive(true);

            var spider = currentTarget.GetTransform().GetComponent<SpiderAI_NavMesh>();
            if (spider != null && spider.targetHighlightEffect != null)
                spider.targetHighlightEffect.SetActive(true);
        }
    }

    public void ClearTarget()
    {
        if (currentTarget != null)
        {
            var oldRepairable = currentTarget.GetTransform().GetComponent<RepairableObject>();
            if (oldRepairable != null && oldRepairable.targetHighlightEffect != null)
                oldRepairable.targetHighlightEffect.SetActive(false);

            var spider = currentTarget.GetTransform().GetComponent<SpiderAI_NavMesh>();
            if (spider != null && spider.targetHighlightEffect != null)
                spider.targetHighlightEffect.SetActive(false);
        }
        currentTarget = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, targetSearchRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
