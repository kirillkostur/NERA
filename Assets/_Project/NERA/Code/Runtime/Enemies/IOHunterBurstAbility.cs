using System.Collections;
using UnityEngine;

namespace NERA.Enemies
{
    [DisallowMultipleComponent]
    public sealed class IOHunterBurstAbility : IOEnemyAttackAbility
    {
        [SerializeField, Min(1)] private int shotCount = 3;
        [SerializeField, Min(0f)] private float shotInterval = 0.16f;
        [SerializeField, Min(0f)] private float dashDistance = 2.25f;
        [SerializeField, Min(0.05f)] private float dashCollisionRadius = 0.4f;
        [SerializeField] private LayerMask dashObstacleMask = ~0;

        private float nextBurstAt;
        private float dashSide = 1f;
        private Coroutine burstRoutine;

        public override void TickAttack(Transform target)
        {
            if (Enemy == null || target == null ||
                burstRoutine != null || Time.time < nextBurstAt)
            {
                return;
            }

            nextBurstAt = Time.time + Enemy.AttackCooldownValue;
            TrySideDash();
            burstRoutine = StartCoroutine(FireBurst(target));
        }

        private void TrySideDash()
        {
            Vector3 right = Enemy.transform.right;
            Vector3 offset = right * (dashDistance * dashSide);
            if (!Enemy.TryMoveBy(
                    offset,
                    dashCollisionRadius,
                    dashObstacleMask))
            {
                Enemy.TryMoveBy(
                    -offset,
                    dashCollisionRadius,
                    dashObstacleMask);
            }

            dashSide *= -1f;
        }

        private IEnumerator FireBurst(Transform target)
        {
            int count = Mathf.Max(1, shotCount);
            for (int index = 0; index < count; index++)
            {
                if (Enemy == null || !Enemy.IsAlive || target == null)
                    break;

                Enemy.FireProjectileAt(target);
                if (index + 1 < count && shotInterval > 0f)
                    yield return new WaitForSeconds(shotInterval);
            }

            burstRoutine = null;
        }

        protected override void OnEnemyDied()
        {
            StopBurst();
        }

        private void OnDisable()
        {
            StopBurst();
        }

        private void StopBurst()
        {
            if (burstRoutine == null)
                return;

            StopCoroutine(burstRoutine);
            burstRoutine = null;
        }
    }
}