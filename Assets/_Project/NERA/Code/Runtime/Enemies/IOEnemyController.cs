using NERA.Combat;
using NERA.Expeditions;
using UnityEngine;

namespace NERA.Enemies
{
    [DisallowMultipleComponent]
    public sealed class IOEnemyController : MonoBehaviour, IDamageable
    {
        private enum State
        {
            Idle,
            Pursuing,
            Attacking,
            Dead
        }

        [Header("Blue IO")]
        [SerializeField, Min(1f)] private float maxHealth = 30f;
        [SerializeField, Min(1f)] private float detectionRadius = 10f;
        [SerializeField, Min(0.5f)] private float attackRange = 6f;
        [SerializeField, Min(0f)] private float moveSpeed = 2f;
        [SerializeField, Min(0.1f)] private float hoverHeight = 1.6f;

        [Header("Energy Attack")]
        [SerializeField, Min(0.1f)] private float attackCooldown = 2f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 8f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 4f;
        [SerializeField, Min(0f)] private float projectileDamage = 10f;
        [SerializeField] private Color energyColor = new Color(0.1f, 0.65f, 1f);

        private Transform target;
        private State state;
        private float currentHealth;
        private float nextAttackTime;
        private float baseY;
        private Material runtimeMaterial;

        public bool IsAlive => state != State.Dead;

        private void Awake()
        {
            currentHealth = maxHealth;
            baseY = transform.position.y;

            Renderer targetRenderer = GetComponentInChildren<Renderer>();
            if (targetRenderer != null)
            {
                runtimeMaterial = targetRenderer.material;
                runtimeMaterial.color = energyColor;
                runtimeMaterial.EnableKeyword("_EMISSION");
                runtimeMaterial.SetColor("_EmissionColor", energyColor * 2.5f);
            }
        }

        private void Update()
        {
            if (!IsAlive)
                return;

            Hover();
            AcquireTarget();

            if (target == null)
            {
                state = State.Idle;
                return;
            }

            float distance = Vector3.Distance(transform.position, target.position);
            if (distance > detectionRadius)
            {
                target = null;
                state = State.Idle;
                return;
            }

            MarkEncountered();
            FaceTarget();

            if (distance > attackRange)
            {
                state = State.Pursuing;
                PursueTarget();
                return;
            }

            state = State.Attacking;
            TryAttack();
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (!IsAlive || amount <= 0f)
                return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            if (currentHealth > 0f)
                return;

            state = State.Dead;
            SpawnResearchDrop();
            Destroy(gameObject);
        }

        private void AcquireTarget()
        {
            if (target != null)
                return;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null &&
                Vector3.Distance(transform.position, player.transform.position) <= detectionRadius)
            {
                target = player.transform;
            }
        }

        private void PursueTarget()
        {
            Vector3 destination = target.position;
            destination.y = baseY;

            transform.position = Vector3.MoveTowards(
                transform.position,
                destination,
                moveSpeed * Time.deltaTime);
        }

        private void TryAttack()
        {
            if (Time.time < nextAttackTime)
                return;

            nextAttackTime = Time.time + attackCooldown;

            Vector3 origin = transform.position + transform.forward * 0.8f;
            Vector3 direction = (target.position + Vector3.up - origin).normalized;

            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "IO_Energy_Projectile";
            projectile.transform.SetPositionAndRotation(
                origin,
                Quaternion.LookRotation(direction));
            projectile.transform.localScale = Vector3.one * 0.22f;

            Collider projectileCollider = projectile.GetComponent<Collider>();
            if (projectileCollider != null)
                Destroy(projectileCollider);

            Renderer projectileRenderer = projectile.GetComponent<Renderer>();
            if (projectileRenderer != null)
            {
                Material projectileMaterial = projectileRenderer.material;
                projectileMaterial.color = energyColor;
                projectileMaterial.EnableKeyword("_EMISSION");
                projectileMaterial.SetColor("_EmissionColor", energyColor * 4f);
            }

            IOEnergyProjectile energyProjectile =
                projectile.AddComponent<IOEnergyProjectile>();
            energyProjectile.Initialize(
                direction,
                projectileSpeed,
                projectileDamage,
                projectileLifetime,
                gameObject);
        }

        private void FaceTarget()
        {
            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(direction);
        }

        private void Hover()
        {
            Vector3 position = transform.position;
            position.y = baseY + Mathf.Sin(Time.time * 2f) * 0.15f;
            transform.position = position;
        }

        private void MarkEncountered()
        {
            ExpeditionProgressController progress =
                ExpeditionProgressController.Instance;
            progress?.MarkIOTraceSeen();
        }

        private void SpawnResearchDrop()
        {
            GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            drop.name = "IO_Blue_Energy_Stone";
            drop.transform.position = transform.position;
            drop.transform.localScale = Vector3.one * 0.3f;

            Renderer dropRenderer = drop.GetComponent<Renderer>();
            if (dropRenderer != null)
                dropRenderer.material.color = energyColor;
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            detectionRadius = Mathf.Max(1f, detectionRadius);
            attackRange = Mathf.Clamp(attackRange, 0.5f, detectionRadius);
            hoverHeight = Mathf.Max(0.1f, hoverHeight);
        }
    }
}
