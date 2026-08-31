using System.Collections.Generic;
using NERA.Enemies;
using NERA.Inventory;
using NERA.Items;
using UnityEngine;
using UnityEngine.Serialization;

namespace NERA.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerEquipmentController))]
    public sealed class PlayerEnergyWeaponController : MonoBehaviour
    {
        [FormerlySerializedAs("aimOrigin")]
        [SerializeField] private Transform fireOrigin;
        [SerializeField] private bool useMainCameraWhenAvailable = true;

        private PlayerEquipmentController equipment;
        private float nextFireTime;
        private Camera cachedMainCamera;

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
            return TryActivateIntegration(definition);
        }

public bool TryActivateIntegration(
            AnomalyIntegrationDefinition definition)
        {
            if (definition == null)
                return false;

            Vector3 center = transform.position;
            switch (definition.Effect)
            {
                case AnomalyIntegrationEffect.EnableElectronics:
                    ApplyElectronicPulse(center, definition);
                    break;

                case AnomalyIntegrationEffect.DamageAnomalies:
                    DamageAnomalies(center, definition);
                    break;

                case AnomalyIntegrationEffect.RestoreFullHealth:
                    PlayerHealth health = GetComponent<PlayerHealth>();
                    if (health == null)
                        health = GetComponentInParent<PlayerHealth>();
                    if (health == null || !health.IsAlive)
                        return false;

                    health.RestoreFullHealth();
                    break;

                case AnomalyIntegrationEffect.RevealThroughWalls:
                    AnomalyScanRevealController scanner =
                        GetComponent<AnomalyScanRevealController>();
                    if (scanner == null)
                    {
                        scanner =
                            gameObject.AddComponent<AnomalyScanRevealController>();
                    }

                    scanner.Reveal(
                        center,
                        definition.Radius,
                        definition.EffectDuration,
                        definition.AffectedLayers,
                        definition.DisplayColor);
                    break;

                case AnomalyIntegrationEffect
                    .DisableElectronicsPermanently:
                    AnomalyPowerPulse.DisablePermanently(
                        center,
                        definition.Radius,
                        definition.AffectedLayers,
                        gameObject,
                        "Violet IO integration");
                    DamageAnomalies(center, definition);
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
            AnomalyIntegrationDefinition definition)
        {
            AnomalyPowerPulse.ApplyTemporaryState(
                center,
                definition.Radius,
                definition.AffectedLayers,
                true,
                definition.ElectronicDuration,
                gameObject);
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

            Transform origin = ResolveFireOrigin();
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

        private Transform ResolveFireOrigin()
        {
            if (useMainCameraWhenAvailable)
            {
                if (cachedMainCamera == null)
                    cachedMainCamera = Camera.main;
                if (cachedMainCamera != null)
                    return cachedMainCamera.transform;
            }

            if (fireOrigin != null)
                return fireOrigin;

            return transform;
        }
    }
}
