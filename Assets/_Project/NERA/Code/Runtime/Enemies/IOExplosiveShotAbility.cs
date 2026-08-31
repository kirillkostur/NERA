using UnityEngine;

namespace NERA.Enemies
{
    [DisallowMultipleComponent]
    public sealed class IOExplosiveShotAbility : IOEnemyAttackAbility
    {
        [SerializeField, Min(0.1f)] private float explosionRadius = 3.5f;
        [SerializeField, Min(0.01f)] private float projectileScaleMultiplier = 1.5f;

        private float nextShotAt;

        public override void TickAttack(Transform target)
        {
            if (Enemy == null || target == null ||
                Time.time < nextShotAt)
            {
                return;
            }

            nextShotAt = Time.time + Enemy.AttackCooldownValue;
            Enemy.FireProjectileAt(
                target,
                damageMultiplier: 1f,
                scaleMultiplier: projectileScaleMultiplier,
                explosionRadius: explosionRadius);
        }
    }
}