using UnityEngine;

public class PlayerTargeting : MonoBehaviour
{
    [Header("Настройки таргета")]
    public float targetSearchRadius = 8f;
    public LayerMask targetMask;

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

    void Start()
    {
        controller = GetComponent<PlayerController>();
    }

    void Update()
    {
        bool isMoving = controller != null && controller.IsMoving();

        // Сбрасываем таргет при движении
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
        else
        {
            moveStartTime = -1f;
        }

        // Проверяем актуальность таргета
        if (currentTarget != null)
        {
            if (!currentTarget.IsAlive() ||
                Vector3.Distance(transform.position, currentTarget.GetTransform().position) > targetSearchRadius + forgetDistanceExtra)
            {
                CancelInteraction();
                ClearTarget();
            }
        }

        if (currentTarget == null)
            FindNewTarget();

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
            else
            {
                CancelInteraction();
            }
        }
        else
        {
            CancelInteraction();
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
        }

        currentTarget = target;

        // Включаем подсветку новой цели
        if (currentTarget != null)
        {
            var newRepairable = currentTarget.GetTransform().GetComponent<RepairableObject>();
            if (newRepairable != null && newRepairable.targetHighlightEffect != null)
                newRepairable.targetHighlightEffect.SetActive(true);
        }
    }

    public void ClearTarget()
    {
        if (currentTarget != null)
        {
            var oldRepairable = currentTarget.GetTransform().GetComponent<RepairableObject>();
            if (oldRepairable != null && oldRepairable.targetHighlightEffect != null)
                oldRepairable.targetHighlightEffect.SetActive(false);
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
