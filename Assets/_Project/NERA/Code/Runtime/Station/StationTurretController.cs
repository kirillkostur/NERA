using NERA.Combat;
using NERA.Energy;
using NERA.Enemies;
using NERA.Maintenance;
using UnityEngine;

namespace NERA.Station
{
    [RequireComponent(typeof(MaintainableObject))]
    public sealed class StationTurretController : MonoBehaviour, IDamageable
    {
        [SerializeField] private string turretId = "station_turret_01";
        [SerializeField] private Transform yawPivot;
        [SerializeField] private Transform muzzle;
        [SerializeField, Min(1f)] private float detectionRange = 18f;
        [SerializeField, Min(1f)] private float rotationSpeed = 180f;
        [SerializeField, Min(0.05f)] private float fireInterval = 0.45f;
        [SerializeField, Min(0.1f)] private float damage = 12f;
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        private MaintainableObject maintenance;
        private IOEnemyController target;
        private string consumerId;
        private float nextTargetSearchAt;
        private float nextShotAt;

        public static StationTurretController Instance { get; private set; }
        public bool IsAlive => maintenance != null && maintenance.IsOperational;
        public bool HasTarget => target != null && target.IsAlive;
        public bool IsInstalled => StationSystemsController.Instance != null &&
            StationSystemsController.Instance.IsUnlocked(StationSystemType.Turret);
        public bool IsOperational => IsInstalled && IsAlive &&
            (StationSystemsController.Instance == null ||
             StationSystemsController.Instance.IsRequestedActive(StationSystemType.Turret)) &&
            EnergySystemController.Instance != null &&
            EnergySystemController.Instance.IsConsumerPowered(consumerId);

        private void Awake()
        {
            Instance = this;
            maintenance = GetComponent<MaintainableObject>();
            if (yawPivot == null)
                yawPivot = transform;
            if (muzzle == null)
                muzzle = yawPivot;
            consumerId = $"turret:{turretId}";
        }

        private void Start()
        {
            RefreshEnergy(false);
        }

        private void Update()
        {
            bool available = IsInstalled && IsAlive &&
                (StationSystemsController.Instance == null ||
                 StationSystemsController.Instance.IsRequestedActive(StationSystemType.Turret));

            if (!available)
            {
                target = null;
                RefreshEnergy(false);
                return;
            }

            if (Time.time >= nextTargetSearchAt)
            {
                nextTargetSearchAt = Time.time + 0.25f;
                target = FindNearestTarget();
            }

            RefreshEnergy(false);
            if (!IsOperational || target == null || !target.IsAlive)
                return;

            Vector3 origin = muzzle.position;
            Vector3 direction = target.transform.position - origin;
            Vector3 flatDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (flatDirection.sqrMagnitude > 0.001f)
            {
                Quaternion desired = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
                yawPivot.rotation = Quaternion.RotateTowards(
                    yawPivot.rotation,
                    desired,
                    rotationSpeed * Time.deltaTime);
            }

            if (Time.time < nextShotAt || !HasLineOfSight(origin, direction))
                return;

            nextShotAt = Time.time + fireInterval;
            RefreshEnergy(true);
            target.TakeDamage(damage, gameObject);
        }

        public void TakeDamage(float amount, GameObject _)
        {
            if (maintenance == null || amount <= 0f)
                return;

            maintenance.SetCondition(maintenance.Condition - amount / 100f);
        }

        private IOEnemyController FindNearestTarget()
        {
            IOEnemyController[] enemies = FindObjectsByType<IOEnemyController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            IOEnemyController nearest = null;
            float nearestSqr = detectionRange * detectionRange;

            foreach (IOEnemyController enemy in enemies)
            {
                if (enemy == null || !enemy.IsAlive)
                    continue;

                float sqr = (enemy.transform.position - transform.position).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = enemy;
                }
            }
            return nearest;
        }

        private bool HasLineOfSight(Vector3 origin, Vector3 direction)
        {
            float distance = direction.magnitude;
            if (distance <= 0.001f)
                return false;

            if (!Physics.Raycast(
                    origin,
                    direction / distance,
                    out RaycastHit hit,
                    distance,
                    lineOfSightMask,
                    QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return hit.collider.GetComponentInParent<IOEnemyController>() == target;
        }

        private void RefreshEnergy(bool firing)
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null || string.IsNullOrWhiteSpace(consumerId))
                return;

            bool requested = IsInstalled && IsAlive &&
                (StationSystemsController.Instance == null ||
                 StationSystemsController.Instance.IsRequestedActive(StationSystemType.Turret));
            float rate = firing
                ? energy.Config.TurretFiringConsumption
                : energy.Config.TurretIdleConsumption;
            energy.RegisterConsumer(consumerId, rate, true);
            energy.SetConsumerActive(consumerId, requested);
        }

        private void OnDestroy()
        {
            EnergySystemController.Instance?.UnregisterConsumer(consumerId);
            if (Instance == this)
                Instance = null;
        }
    }
}
