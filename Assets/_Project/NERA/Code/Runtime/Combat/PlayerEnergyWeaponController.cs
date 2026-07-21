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
        }

        private void OnDisable()
        {
            if (equipment != null)
                equipment.EquipmentUseRequested -= HandleEquipmentUseRequested;
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
