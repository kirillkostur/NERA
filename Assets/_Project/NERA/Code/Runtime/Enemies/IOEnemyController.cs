using NERA.Combat;
using NERA.Quests;
using NERA.Items;
using NERA.Save;
using System.Collections.Generic;
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

        [SerializeField] private IOEnemyConfig config;
        [Tooltip("Stable ID required for every authored enemy instance.")]
        [SerializeField] private string persistentId;

        private static readonly HashSet<IOEnemyController> ActiveEnemySet =
            new HashSet<IOEnemyController>();
        private const float TargetScanInterval = 0.15f;
        private const float SharedPlayerRetryInterval = 0.5f;
        private static Transform sharedPlayerTransform;
        private static PlayerHealth sharedPlayerHealth;
        private static float nextSharedPlayerSearchAt;

        private Transform target;
        private PlayerHealth targetHealth;
        private State state;
        private float currentHealth;
        private float nextAttackTime;
        private float baseY;
        private Material runtimeMaterial;
        private bool encounterReported;
        private string persistentKey;
        private float nextTargetScanAt;

        public static IReadOnlyCollection<IOEnemyController> ActiveEnemies =>
            ActiveEnemySet;
        public string AuthoredPersistentId => persistentId?.Trim();
        public bool IsAlive => state != State.Dead;
        public string PersistentKey => persistentKey;

        private void OnValidate()
        {
            persistentId = persistentId?.Trim();
        }

        private void Awake()
        {
            persistentKey = PersistentSceneIdentity.CreateKey(
                transform,
                persistentId);
            currentHealth = MaxHealth;
            baseY = transform.position.y;

            Renderer targetRenderer = GetComponentInChildren<Renderer>();
            if (targetRenderer != null)
            {
                runtimeMaterial = targetRenderer.material;
                runtimeMaterial.color = EnergyColor;
                runtimeMaterial.EnableKeyword("_EMISSION");
                runtimeMaterial.SetColor(
                    "_EmissionColor",
                    EnergyColor * EmissionIntensity
                );
            }
        }

        private void Start()
        {
            if (!HasPersistentIdentity)
                return;

            WorldStateController worldState = WorldStateController.Instance;
            if (worldState == null ||
                !worldState.IsEnemyDefeated(persistentKey))
            {
                return;
            }

            state = State.Dead;
            ActiveEnemySet.Remove(this);
            SpawnResearchDrop();
            Destroy(gameObject);
        }

        private void OnEnable()
        {
            if (IsAlive)
                ActiveEnemySet.Add(this);

            nextTargetScanAt = Time.time +
                Mathf.Abs(GetInstanceID() % 10) *
                (TargetScanInterval / 10f);
        }

        private void Update()
        {
            if (!IsAlive)
                return;

            Hover();
            AcquireTarget();

            if (!HasLivingTarget())
            {
                ClearTarget();
                state = State.Idle;
                return;
            }

            float sqrDistance =
                (transform.position - target.position).sqrMagnitude;
            if (sqrDistance > DetectionRadius * DetectionRadius)
            {
                ClearTarget();
                state = State.Idle;
                return;
            }

            MarkEncountered();
            FaceTarget();

            if (sqrDistance > AttackRange * AttackRange)
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
            ActiveEnemySet.Remove(this);
            if (HasPersistentIdentity)
            {
                WorldStateController.Instance?.MarkEnemyDefeated(
                    persistentKey);
            }
            QuestController.Instance?.Report(
                QuestSignalType.EnemyKilled,
                config != null ? config.EnemyId : name,
                config != null ? config.DisplayName : name);
            SpawnResearchDrop();
            Destroy(gameObject);
        }

        private void AcquireTarget()
        {
            if (HasLivingTarget())
                return;

            if (Time.time < nextTargetScanAt)
                return;

            nextTargetScanAt = Time.time + TargetScanInterval;

            ClearTarget();

            if (!TryResolveSharedPlayer())
                return;

            if ((transform.position - sharedPlayerTransform.position)
                    .sqrMagnitude > DetectionRadius * DetectionRadius)
                return;

            target = sharedPlayerTransform;
            targetHealth = sharedPlayerHealth;
            targetHealth.Died += HandleTargetDied;
        }

        private static bool TryResolveSharedPlayer()
        {
            if (sharedPlayerTransform != null &&
                sharedPlayerHealth != null &&
                sharedPlayerHealth.IsAlive)
            {
                return true;
            }

            sharedPlayerTransform = null;
            sharedPlayerHealth = null;
            if (Time.time < nextSharedPlayerSearchAt)
                return false;

            nextSharedPlayerSearchAt = Time.time + SharedPlayerRetryInterval;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
                return false;

            PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            if (playerHealth == null)
                playerHealth = player.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null || !playerHealth.IsAlive)
                return false;

            sharedPlayerTransform = player.transform;
            sharedPlayerHealth = playerHealth;
            return true;
        }

        private bool HasLivingTarget()
        {
            return target != null &&
                   targetHealth != null &&
                   targetHealth.IsAlive;
        }

        private void HandleTargetDied()
        {
            ClearTarget();
            state = State.Idle;
        }

        private void ClearTarget()
        {
            if (targetHealth != null)
                targetHealth.Died -= HandleTargetDied;

            target = null;
            targetHealth = null;
        }

        private void PursueTarget()
        {
            Vector3 destination = target.position;
            destination.y = baseY;

            transform.position = Vector3.MoveTowards(
                transform.position,
                destination,
                MoveSpeed * Time.deltaTime);
        }

        private void TryAttack()
        {
            if (!HasLivingTarget())
            {
                ClearTarget();
                state = State.Idle;
                return;
            }

            if (Time.time < nextAttackTime)
                return;

            nextAttackTime = Time.time + AttackCooldown;

            Vector3 origin = transform.position + transform.forward * 0.8f;
            Vector3 direction = (target.position + Vector3.up - origin).normalized;

            IOEnergyProjectile projectile = IOProjectilePool.Spawn(
                ProjectilePrefab,
                origin,
                Quaternion.LookRotation(direction),
                ProjectileScale,
                EnergyColor,
                ProjectileEmissionIntensity);
            projectile.Initialize(
                direction,
                ProjectileSpeed,
                ProjectileDamage,
                ProjectileLifetime,
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
            position.y = baseY +
                Mathf.Sin(Time.time * HoverFrequency) * HoverAmplitude;
            transform.position = position;
        }

        private void MarkEncountered()
        {
            if (encounterReported)
                return;

            encounterReported = true;
            QuestController.Instance?.Report(
                QuestSignalType.EnemyEncountered,
                config != null ? config.EnemyId : name,
                config != null ? config.DisplayName : name);
        }

        private void SpawnResearchDrop()
        {
            if (DeathDropPrefab == null)
                return;

            GameObject drop = Instantiate(
                DeathDropPrefab,
                transform.position + DeathDropOffset,
                Quaternion.identity
            );
            WorldItem worldItem = drop != null
                ? drop.GetComponentInChildren<WorldItem>(true)
                : null;
            if (worldItem == null)
                return;

            if (HasPersistentIdentity)
            {
                worldItem.SetPersistentWorldId(persistentKey + "/drop");
            }
            else
            {
                worldItem.Initialize(worldItem.ItemData);
            }
        }

        private void OnDisable()
        {
            ActiveEnemySet.Remove(this);
            ClearTarget();
        }

        private void OnDestroy()
        {
            ActiveEnemySet.Remove(this);
            ClearTarget();

            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }

        private float MaxHealth => config != null ? config.MaxHealth : 30f;
        private float DetectionRadius => config != null ? config.DetectionRadius : 10f;
        private float AttackRange => config != null ? config.AttackRange : 6f;
        private float MoveSpeed => config != null ? config.MoveSpeed : 2f;
        private float HoverAmplitude => config != null ? config.HoverAmplitude : 0.15f;
        private float HoverFrequency => config != null ? config.HoverFrequency : 2f;
        private float AttackCooldown => config != null ? config.AttackCooldown : 2f;
        private float ProjectileSpeed => config != null ? config.ProjectileSpeed : 8f;
        private float ProjectileLifetime => config != null ? config.ProjectileLifetime : 4f;
        private float ProjectileDamage => config != null ? config.ProjectileDamage : 10f;
        private float ProjectileScale => config != null ? config.ProjectileScale : 0.22f;
        private GameObject ProjectilePrefab => config != null ? config.ProjectilePrefab : null;
        private Color EnergyColor => config != null
            ? config.EnergyColor
            : new Color(0.1f, 0.65f, 1f);
        private float EmissionIntensity => config != null ? config.EmissionIntensity : 2.5f;
        private float ProjectileEmissionIntensity => config != null
            ? config.ProjectileEmissionIntensity
            : 4f;
        private GameObject DeathDropPrefab => config != null ? config.DeathDropPrefab : null;
        private Vector3 DeathDropOffset => config != null ? config.DeathDropOffset : Vector3.zero;
        private bool HasPersistentIdentity =>
            !string.IsNullOrEmpty(persistentKey);
    }
}
