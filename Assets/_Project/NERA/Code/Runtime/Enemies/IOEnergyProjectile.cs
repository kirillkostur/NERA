using NERA.Combat;
using System;
using UnityEngine;

namespace NERA.Enemies
{
    [DisallowMultipleComponent]
    public sealed class IOEnergyProjectile : MonoBehaviour
    {
        [SerializeField] private LayerMask hitMask =
            (1 << 0) | (1 << 3) | (1 << 6) | (1 << 7) |
            (1 << 9) | (1 << 10) | (1 << 11) | (1 << 14) | (1 << 15);

        private Vector3 direction;
        private float speed;
        private float damage;
        private float remainingLifetime;
        private GameObject source;
        private float explosionRadius;
        private Action<IOEnergyProjectile> releaseAction;
        private Material runtimeMaterial;
        private Renderer projectileRenderer;

        private void Awake()
        {
            projectileRenderer = GetComponent<Renderer>();
        }

        public void SetReleaseAction(Action<IOEnergyProjectile> release)
        {
            releaseAction = release;
        }

        public void ConfigureVisual(Color color, float emissionIntensity)
        {
            projectileRenderer ??= GetComponent<Renderer>();
            if (projectileRenderer == null)
                return;

            if (runtimeMaterial == null)
                runtimeMaterial = projectileRenderer.material;
            runtimeMaterial.color = color;
            runtimeMaterial.EnableKeyword("_EMISSION");
            runtimeMaterial.SetColor(
                "_EmissionColor",
                color * Mathf.Max(0f, emissionIntensity));
        }

public void Initialize(
            Vector3 travelDirection,
            float travelSpeed,
            float damageAmount,
            float lifetime,
            GameObject damageSource,
            float areaRadius = 0f)
        {
            direction = travelDirection.normalized;
            speed = travelSpeed;
            damage = damageAmount;
            remainingLifetime = lifetime;
            source = damageSource;
            explosionRadius = Mathf.Max(0f, areaRadius);
            gameObject.SetActive(true);
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
                    hitMask,
                    QueryTriggerInteraction.Ignore))
            {
                if (explosionRadius > 0f)
                    ApplyExplosion(hit.point);
                else
                    TryDamage(hit.collider);
                Release();
                return;
            }

            transform.position += direction * distance;
            remainingLifetime -= Time.deltaTime;

            if (remainingLifetime <= 0f)
                Release();
        }

private void TryDamage(Collider hitCollider)
        {
            if (hitCollider == null ||
                source != null &&
                hitCollider.transform.IsChildOf(source.transform))
            {
                return;
            }

            IDamageable damageable =
                hitCollider.GetComponentInParent<IDamageable>();

            damageable?.TakeDamage(damage, source);
        }

private void ApplyExplosion(Vector3 impactPoint)
        {
            Collider[] hits = Physics.OverlapSphere(
                impactPoint,
                explosionRadius,
                hitMask,
                QueryTriggerInteraction.Ignore);
            var damaged =
                new System.Collections.Generic.HashSet<IDamageable>();

            foreach (Collider hitCollider in hits)
            {
                if (hitCollider == null ||
                    source != null &&
                    hitCollider.transform.IsChildOf(source.transform))
                {
                    continue;
                }

                if (hitCollider.GetComponentInParent<IOEnemyController>() != null)
                    continue;

                IDamageable damageable =
                    hitCollider.GetComponentInParent<IDamageable>();
                if (damageable != null && damaged.Add(damageable))
                    damageable.TakeDamage(damage, source);
            }
        }


        private void Release()
        {
            source = null;
            Action<IOEnergyProjectile> release = releaseAction;
            releaseAction = null;
            if (release != null)
                release(this);
            else
                Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
                Destroy(runtimeMaterial);
        }
    }
}
