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

    [Header("Атака")]
    public float attackCooldown = 1.5f;
    private float nextAttackTime;
    [HideInInspector] public WaveConfig waveConfig;
    [HideInInspector] public int waveIndex;

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
        if (isDead) return;

        if (target != null)
        {
            var ph = target.GetComponent<PlayerHealth>();
            var cc = target.GetComponent<CharacterController>();
            bool playerDeadOrDisabled = (ph != null && ph.IsDead) || (cc != null && !cc.enabled);

            if (playerDeadOrDisabled)
            {
                if (agent != null && agent.hasPath) agent.ResetPath();
                agent.updateRotation = true;
                target = null;
                return;
            }
        }

        if (target == null) return;

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

                if (Time.time >= nextAttackTime)
                {
                    var health = target.GetComponent<PlayerHealth>();
                    if (health != null && !health.IsDead)
                    {
                        WaveConfig.WaveData waveData =
                            (waveConfig != null && waveIndex >= 0 && waveConfig.waves.Length > waveIndex)
                            ? waveConfig.waves[waveIndex]
                            : null;

                        int damage = 5;
                        float scale = transform.localScale.x;
                        if (waveData != null)
                        {
                            if (scale < 0.8f) damage = waveData.smallSpiderDamage;
                            else if (scale < 1.1f) damage = waveData.mediumSpiderDamage;
                            else damage = waveData.largeSpiderDamage;
                        }

                        health.TakeDamage(damage);
                        nextAttackTime = Time.time + attackCooldown;
                    }
                }
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

    // === ITargetable ===
    public Transform GetTransform() => transform;
    public bool IsAlive() => !isDead;

    public void Die()
    {
        isDead = true;
        if (agent != null) agent.ResetPath();
    }

    private void OnDisable()
    {
        // Подсветка убрана, больше ничего не делаем
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);
    }
}
