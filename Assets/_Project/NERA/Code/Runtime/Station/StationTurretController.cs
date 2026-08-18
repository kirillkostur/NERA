using NERA.Combat;
using NERA.Energy;
using NERA.Enemies;
using NERA.Maintenance;
using NERA.Items;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.Station
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MaintainableObject))]
    public sealed class StationTurretController : MonoBehaviour, IDamageable
    {
        [SerializeField] private Transform yawPivot;
        [SerializeField] private Transform muzzle;
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        private MaintainableObject maintenance;
        private StationObjectIdentity identity;
        private IOEnemyController target;
        private string consumerId;
        private float nextTargetSearchAt;
        private float nextShotAt;

        private static readonly Dictionary<string, StationTurretController>
            TurretsById = new Dictionary<string, StationTurretController>(
                StringComparer.OrdinalIgnoreCase);

        public string TurretId
        {
            get
            {
                CacheIdentity();
                return identity != null ? identity.ObjectId : string.Empty;
            }
        }
        public bool InitiallyActive
        {
            get
            {
                StationSystemsConfig config =
                    StationSystemsController.Instance?.Config ??
                    StationSystemsConfig.LoadDefault();
                return config.Find(
                    StationSystemType.Turret,
                    TurretId)?.InitiallyActive ?? true;
            }
        }
        public float Condition => maintenance != null
            ? maintenance.Condition
            : 1f;
        public bool IsAlive => maintenance != null && maintenance.IsOperational;
        public bool HasTarget => target != null && target.IsAlive;
        public bool IsInstalled => StationSystemsController.Instance?
            .GetDefinition(StationSystemType.Turret, TurretId) != null;
        public bool IsOperational => IsInstalled && IsAlive &&
            (StationSystemsController.Instance == null ||
             StationSystemsController.Instance.IsRequestedActive(
                 StationSystemType.Turret,
                 TurretId)) &&
            EnergySystemController.Instance != null &&
            EnergySystemController.Instance.IsConsumerPowered(consumerId);
        public float EffectiveDamage => Mathf.Max(
            0f,
            GetConfiguredStat(StationObjectStat.Damage, 12f));
        public float EffectiveDetectionRange => Mathf.Max(
            1f,
            GetConfiguredStat(
                StationObjectStat.DetectionRange,
                18f));
        public float EffectiveRotationSpeed => Mathf.Max(
            1f,
            GetConfiguredStat(
                StationObjectStat.RotationSpeed,
                180f));
        public float EffectiveFireInterval => Mathf.Max(
            0.02f,
            GetConfiguredStat(
                StationObjectStat.FireInterval,
                0.45f));
        public float EffectiveAimTolerance => Mathf.Clamp(
            GetConfiguredStat(
                StationObjectStat.AimTolerance,
                5f),
            0.1f,
            45f);
        public float EffectiveEnergyPerShot => Mathf.Max(
            0f,
            GetConfiguredStat(
                StationObjectStat.FiringEnergyPerShot,
                5f));

        private void Awake()
        {
            CacheIdentity();
            maintenance = GetComponent<MaintainableObject>();
            if (yawPivot == null)
                yawPivot = transform;
            if (muzzle == null)
                muzzle = yawPivot;
            consumerId = $"turret:{TurretId}";
            RegisterTurret();
            StationSystemsController.Instance?.RegisterObject(
                StationSystemType.Turret,
                TurretId);
        }

        private void Start()
        {
            RefreshEnergy();
        }

        private void Update()
        {
            bool available = IsInstalled && IsAlive &&
                (StationSystemsController.Instance == null ||
                 StationSystemsController.Instance.IsRequestedActive(
                     StationSystemType.Turret,
                     TurretId));

            if (!available)
            {
                target = null;
                RefreshEnergy();
                return;
            }

            if (Time.time >= nextTargetSearchAt)
            {
                nextTargetSearchAt = Time.time + 0.25f;
                target = FindNearestTarget();
            }

            RefreshEnergy();
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
                    EffectiveRotationSpeed * Time.deltaTime);
            }

            if (Time.time < nextShotAt ||
                !IsMuzzleAimedAt(target.transform.position) ||
                !HasLineOfSight(origin, direction))
                return;

            if (!TrySpendFiringEnergy())
                return;

            nextShotAt = Time.time + EffectiveFireInterval;
            target.TakeDamage(EffectiveDamage, gameObject);
        }

        public void TakeDamage(float amount, GameObject _)
        {
            if (maintenance == null || amount <= 0f)
                return;

            float received = Mathf.Max(
                0f,
                amount * GetConfiguredStat(
                    StationObjectStat.DamageTaken,
                    1f));
            maintenance.SetCondition(
                maintenance.Condition - received / 100f);
        }

        public bool IsMuzzleAimedAt(Vector3 targetPosition)
        {
            Transform aimTransform = muzzle != null
                ? muzzle
                : yawPivot != null
                    ? yawPivot
                    : transform;
            Vector3 targetDirection = Vector3.ProjectOnPlane(
                targetPosition - aimTransform.position,
                Vector3.up);
            Vector3 muzzleDirection = Vector3.ProjectOnPlane(
                aimTransform.forward,
                Vector3.up);
            if (targetDirection.sqrMagnitude <= 0.001f ||
                muzzleDirection.sqrMagnitude <= 0.001f)
            {
                return false;
            }

            return Vector3.Angle(muzzleDirection, targetDirection) <=
                EffectiveAimTolerance;
        }

        private IOEnemyController FindNearestTarget()
        {
            IOEnemyController nearest = null;
            float range = EffectiveDetectionRange;
            float nearestSqr = range * range;

            foreach (IOEnemyController enemy in IOEnemyController.ActiveEnemies)
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

        public bool TrySpendFiringEnergy()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            return energy != null &&
                energy.TrySpendConsumerEnergy(
                    consumerId,
                    EffectiveEnergyPerShot);
        }

        private void RefreshEnergy()
        {
            EnergySystemController energy = EnergySystemController.Instance;
            if (energy == null || string.IsNullOrWhiteSpace(consumerId))
                return;

            bool requested = IsInstalled && IsAlive &&
                (StationSystemsController.Instance == null ||
                 StationSystemsController.Instance.IsRequestedActive(
                     StationSystemType.Turret,
                     TurretId));
            float rate = Mathf.Max(
                0f,
                GetConfiguredStat(
                    StationObjectStat.IdleEnergyConsumption,
                    2f));
            energy.RegisterConsumer(
                consumerId,
                rate,
                energy.Config.GetMinimumCharge01(
                    StationSystemType.Turret,
                    TurretId),
                StationSystemType.Turret,
                TurretId);
            energy.SetConsumerActive(consumerId, requested);
        }

        private void OnDestroy()
        {
            EnergySystemController.Instance?.UnregisterConsumer(consumerId);
            string stableId = TurretId;
            if (!string.IsNullOrWhiteSpace(stableId) &&
                TurretsById.TryGetValue(
                    stableId,
                    out StationTurretController registered) &&
                registered == this)
            {
                TurretsById.Remove(stableId);
            }
        }

        public static StationTurretController FindById(string objectId)
        {
            if (string.IsNullOrWhiteSpace(objectId))
                return null;

            TurretsById.TryGetValue(
                objectId.Trim(),
                out StationTurretController turret);
            return turret;
        }

        private void RegisterTurret()
        {
            string stableId = TurretId;
            if (!string.IsNullOrWhiteSpace(stableId))
                TurretsById[stableId] = this;
        }

        private void CacheIdentity()
        {
            if (identity == null)
                identity = GetComponentInParent<StationObjectIdentity>(true);
        }

        private float GetConfiguredStat(
            StationObjectStat stat,
            float fallback)
        {
            return StationSystemsController.Instance?.GetStat(
                StationSystemType.Turret,
                TurretId,
                stat,
                fallback) ?? fallback;
        }

        private void OnValidate()
        {
            CacheIdentity();
        }
    }
}
