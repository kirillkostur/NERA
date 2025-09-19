using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class SpiderAI_NavMesh : MonoBehaviour, ITargetable
{
    [Header("Параметры паука")]
    public Transform target;
    public float moveSpeed = 3.5f;
    public float stoppingDistance = 1.5f;
    public float detectionRadius = 50f;
    public float updateRate = 0.2f;
    public float rotationSpeed = 120f;

    private NavMeshAgent agent;
    private float nextUpdateTime;
    private bool isDead = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        ApplySettings();

        if (target == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                target = playerObj.transform;
        }
    }

    public void ApplySettings()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent == null) return;

        agent.speed = moveSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.angularSpeed = rotationSpeed;
        agent.updateRotation = true;
        agent.updatePosition = true;
    }

    void Update()
    {
        if (isDead || target == null) return;

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance <= detectionRadius)
        {
            if (distance > stoppingDistance)
            {
                if (Time.time >= nextUpdateTime)
                {
                    agent.updateRotation = true;
                    agent.SetDestination(target.position);
                    nextUpdateTime = Time.time + updateRate;
                }
            }
            else
            {
                if (agent.hasPath) agent.ResetPath();
                agent.updateRotation = false;
                LookAtTarget();
            }
        }
        else
        {
            if (agent.hasPath) agent.ResetPath();
        }
    }

    private void LookAtTarget()
    {
        Vector3 dir = (target.position - transform.position).normalized;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion lookRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * (rotationSpeed / 10f));
        }
    }

    // === Реализация ITargetable ===
    public Transform GetTransform() => transform;

    public bool IsAlive() => !isDead;

    // Вызвать этот метод при смерти паука
    public void Die()
    {
        isDead = true;
        if (agent != null) agent.ResetPath();
        // Здесь можно добавить анимацию смерти или уничтожение
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);
    }
}
