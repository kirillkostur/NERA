using UnityEngine;
using System.Collections;

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

    private bool blockTargeting = false;

    public Transform CurrentTargetTransform => currentTarget != null ? currentTarget.GetTransform() : null;

    private void OnEnable() { blockTargeting = false; }

    private void Start()
    {
        controller = GetComponent<PlayerController>();
        StartCoroutine(SearchRoutine());
    }

    private void OnDisable() { ClearTargetInternal(); }
    private void OnDestroy() { ClearTargetInternal(); }

    private void Update()
    {
        if (blockTargeting) return;

        bool isMoving = controller != null && controller.IsMoving();
        if (isMoving)
        {
            if (moveStartTime < 0f) moveStartTime = Time.time;
            if (currentTarget != null && Time.time - moveStartTime >= moveClearDelay)
            {
                CancelInteraction();
                ClearTargetInternal();
            }
        }
        else moveStartTime = -1f;

        if (currentTarget != null)
        {
            if (!currentTarget.IsAlive() ||
                Vector3.Distance(transform.position, currentTarget.GetTransform().position) >
                targetSearchRadius + forgetDistanceExtra)
            {
                CancelInteraction();
                ClearTargetInternal();
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
            if (!blockTargeting && currentTarget == null) FindNewTarget();
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
        if (blockTargeting) return;

        // выключаем подсветку у прошлого
        if (currentTarget != null)
        {
            var oldTr = currentTarget.GetTransform();

            var oldLoot = oldTr.GetComponent<LootableObject>();
            if (oldLoot != null) oldLoot.ToggleHighlight(false);

            var oldRepairable = oldTr.GetComponent<RepairableObject>();
            if (oldRepairable != null && oldRepairable.targetHighlightEffect != null)
                oldRepairable.targetHighlightEffect.SetActive(false);
        }

        currentTarget = target;

        // включаем подсветку у нового
        if (currentTarget != null)
        {
            var tr = currentTarget.GetTransform();

            var loot = tr.GetComponent<LootableObject>();
            if (loot != null) loot.ToggleHighlight(true);

            var repairable = tr.GetComponent<RepairableObject>();
            if (repairable != null && repairable.targetHighlightEffect != null)
                repairable.targetHighlightEffect.SetActive(true);
        }
    }

    public void ClearTarget()
    {
        blockTargeting = true;
        ClearTargetInternal();
    }

    private void ClearTargetInternal()
    {
        if (currentTarget != null)
        {
            Transform targetTransform = null;
            try { targetTransform = currentTarget.GetTransform(); }
            catch (MissingReferenceException)
            {
                currentTarget = null;
                return;
            }

            if (targetTransform == null)
            {
                currentTarget = null;
                return;
            }

            var oldLoot = targetTransform.GetComponent<LootableObject>();
            if (oldLoot != null) oldLoot.ToggleHighlight(false);

            var oldRepairable = targetTransform.GetComponent<RepairableObject>();
            if (oldRepairable != null && oldRepairable.targetHighlightEffect != null)
                oldRepairable.targetHighlightEffect.SetActive(false);
        }

        currentTarget = null;
    }
}
