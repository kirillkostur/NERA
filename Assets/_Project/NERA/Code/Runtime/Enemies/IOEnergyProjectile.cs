using NERA.Combat;
using UnityEngine;

namespace NERA.Enemies
{
    [DisallowMultipleComponent]
    public sealed class IOEnergyProjectile : MonoBehaviour
    {
        private Vector3 direction;
        private float speed;
        private float damage;
        private float remainingLifetime;
        private GameObject source;

        public void Initialize(
            Vector3 travelDirection,
            float travelSpeed,
            float damageAmount,
            float lifetime,
            GameObject damageSource)
        {
            direction = travelDirection.normalized;
            speed = travelSpeed;
            damage = damageAmount;
            remainingLifetime = lifetime;
            source = damageSource;
        }

        private void Update()
        {
            float distance = speed * Time.deltaTime;

            if (Physics.SphereCast(
                    transform.position,
                    0.12f,
                    direction,
                    out RaycastHit hit,
                    distance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                TryDamage(hit.collider);
                Destroy(gameObject);
                return;
            }

            transform.position += direction * distance;
            remainingLifetime -= Time.deltaTime;

            if (remainingLifetime <= 0f)
                Destroy(gameObject);
        }

        private void TryDamage(Collider hitCollider)
        {
            if (hitCollider == null || hitCollider.gameObject == source)
                return;

            IDamageable damageable =
                hitCollider.GetComponentInParent<IDamageable>();

            damageable?.TakeDamage(damage, source);
        }
    }
}
