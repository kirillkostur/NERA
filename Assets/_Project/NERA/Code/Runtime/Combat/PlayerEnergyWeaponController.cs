using System.Collections.Generic;
using NERA.Enemies;
using NERA.Inventory;
using NERA.Items;
using UnityEngine;

namespace NERA.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerEquipmentController))]
    public sealed class PlayerEnergyWeaponController : MonoBehaviour
    {
        [SerializeField] private Transform aimOrigin;
        [SerializeField] private bool useMainCameraWhenAvailable = true;

        private PlayerEquipmentController equipment;
        private float nextFireTime;

        private void Awake()
        {
            equipment = GetComponent<PlayerEquipmentController>();
        }

        private void OnEnable()
        {
            if (equipment == null)
                equipment = GetComponent<PlayerEquipmentController>();

            equipment.EquipmentUseRequested += HandleEquipmentUseRequested;
            equipment.AnomalyUseRequested += HandleAnomalyUseRequested;
        }

        private void OnDisable()
        {
            if (equipment != null)
            {
                equipment.EquipmentUseRequested -= HandleEquipmentUseRequested;
                equipment.AnomalyUseRequested -= HandleAnomalyUseRequested;
            }
        }

        private bool HandleEquipmentUseRequested(
            ItemInstance instance,
            QuickAccessAction action
        )
        {
            ItemData item = instance?.ItemData;
            if (action != QuickAccessAction.Fire ||
                item == null ||
                item.WeaponDefinition == null)
            {
                return false;
            }

            return TryFire(item.WeaponDefinition);
        }

        private bool HandleAnomalyUseRequested(
            ItemInstance _,
            AnomalyIntegrationDefinition definition)
        {
            if (definition == null)
                return false;

            Vector3 center = transform.position;
            switch (definition.Effect)
            {
                case AnomalyIntegrationEffect.EnableElectronics:
                    ApplyElectronicPulse(
                        center,
                        definition,
                        true);
                    break;

                case AnomalyIntegrationEffect.DamageAnomalies:
                    DamageAnomalies(center, definition);
                    break;

                case AnomalyIntegrationEffect.DisableElectronics:
                    ApplyElectronicPulse(
                        center,
                        definition,
                        false);
                    break;

                default:
                    return false;
            }

            DrawPulse(center, definition);
            return true;
        }

        private void DamageAnomalies(
            Vector3 center,
            AnomalyIntegrationDefinition definition)
        {
            Collider[] hits = Physics.OverlapSphere(
                center,
                definition.Radius,
                definition.AffectedLayers,
                QueryTriggerInteraction.Collide);
            HashSet<IOEnemyController> affected =
                new HashSet<IOEnemyController>();
            foreach (Collider hit in hits)
            {
                IOEnemyController anomaly =
                    hit.GetComponentInParent<IOEnemyController>();
                if (anomaly != null &&
                    anomaly.IsAlive &&
                    affected.Add(anomaly))
                {
                    anomaly.TakeDamage(
                        definition.AnomalyDamage,
                        gameObject);
                }
            }
        }

        private void ApplyElectronicPulse(
            Vector3 center,
            AnomalyIntegrationDefinition definition,
            bool powered)
        {
            Collider[] hits = Physics.OverlapSphere(
                center,
                definition.Radius,
                definition.AffectedLayers,
                QueryTriggerInteraction.Collide);
            HashSet<IAnomalyElectronic> affected =
                new HashSet<IAnomalyElectronic>();
            foreach (Collider hit in hits)
            {
                MonoBehaviour[] behaviours =
                    hit.GetComponentsInParent<MonoBehaviour>(true);
                foreach (MonoBehaviour behaviour in behaviours)
                {
                    if (behaviour is IAnomalyElectronic electronic &&
                        affected.Add(electronic))
                    {
                        electronic.ApplyAnomalyPowerState(
                            powered,
                            definition.ElectronicDuration,
                            gameObject);
                    }
                }
            }
        }

        private static void DrawPulse(
            Vector3 center,
            AnomalyIntegrationDefinition definition)
        {
            float radius = definition.Radius;
            Color color = definition.DisplayColor;
            const float duration = 0.35f;
            Debug.DrawLine(
                center - Vector3.right * radius,
                center + Vector3.right * radius,
                color,
                duration);
            Debug.DrawLine(
                center - Vector3.forward * radius,
                center + Vector3.forward * radius,
                color,
                duration);
        }

        private bool TryFire(WeaponDefinition weapon)
        {
            if (weapon == null || Time.time < nextFireTime)
                return false;

            nextFireTime = Time.time + weapon.Cooldown;

            Transform origin = ResolveAimOrigin();
            Vector3 start = origin.position;
            Vector3 direction = origin.forward;
            float beamDistance = weapon.Range;

            if (Physics.Raycast(
                    start,
                    direction,
                    out RaycastHit hit,
                    weapon.Range,
                    weapon.HitMask,
                    QueryTriggerInteraction.Ignore
                ))
            {
                beamDistance = hit.distance;
                IDamageable damageable =
                    hit.collider.GetComponentInParent<IDamageable>();

                if (damageable != null && damageable.IsAlive)
                    damageable.TakeDamage(weapon.Damage, gameObject);
            }

            if (weapon.DebugBeamDuration > 0f)
            {
                Debug.DrawRay(
                    start,
                    direction * beamDistance,
                    weapon.BeamColor,
                    weapon.DebugBeamDuration
                );
            }

            return true;
        }

        private Transform ResolveAimOrigin()
        {
            if (useMainCameraWhenAvailable && Camera.main != null)
                return Camera.main.transform;

            if (aimOrigin != null)
                return aimOrigin;

            return transform;
        }
    }
}
