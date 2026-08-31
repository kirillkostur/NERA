using System.Collections;
using UnityEngine;

namespace NERA.Enemies
{
    [DisallowMultipleComponent]
    public sealed class IORegenerationPulseAbility : IOEnemyAbility
    {
        [SerializeField, Min(0.1f)] private float pulseInterval = 7f;
        [SerializeField, Min(0f)] private float telegraphDuration = 0.65f;
        [SerializeField, Min(0f)] private float healAmount = 18f;
        [SerializeField, Min(0.1f)] private float healRadius = 6f;
        [SerializeField] private Transform pulseVisual;

        private float nextPulseAt;
        private Coroutine pulseRoutine;
        private Vector3 pulseVisualScale = Vector3.one;

        protected override void OnBound()
        {
            nextPulseAt = Time.time + pulseInterval;
            if (pulseVisual == null)
                return;

            pulseVisualScale = pulseVisual.localScale;
            pulseVisual.gameObject.SetActive(false);
        }

        protected override void OnTick(float deltaTime)
        {
            if (pulseRoutine != null || Time.time < nextPulseAt)
                return;

            nextPulseAt = Time.time + pulseInterval;
            pulseRoutine = StartCoroutine(Pulse());
        }

        private IEnumerator Pulse()
        {
            if (pulseVisual != null)
            {
                pulseVisual.gameObject.SetActive(true);
                float elapsed = 0f;
                while (elapsed < telegraphDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = telegraphDuration > 0f
                        ? Mathf.Clamp01(elapsed / telegraphDuration)
                        : 1f;
                    pulseVisual.localScale =
                        pulseVisualScale * Mathf.Lerp(0.25f, 1.5f, t);
                    yield return null;
                }
            }
            else if (telegraphDuration > 0f)
            {
                yield return new WaitForSeconds(telegraphDuration);
            }

            HealNearbyEnemies();

            if (pulseVisual != null)
            {
                pulseVisual.localScale = pulseVisualScale;
                pulseVisual.gameObject.SetActive(false);
            }

            pulseRoutine = null;
        }

        private void HealNearbyEnemies()
        {
            if (Enemy == null)
                return;

            float radiusSquared = healRadius * healRadius;
            foreach (IOEnemyController candidate in IOEnemyController.ActiveEnemies)
            {
                if (candidate == null || !candidate.IsAlive)
                    continue;

                if ((candidate.transform.position - Enemy.transform.position)
                        .sqrMagnitude > radiusSquared)
                {
                    continue;
                }

                candidate.Heal(healAmount);
            }
        }

        protected override void OnEnemyDied()
        {
            ResetPulseVisual();
        }

        private void OnDisable()
        {
            ResetPulseVisual();
        }

        private void ResetPulseVisual()
        {
            if (pulseRoutine != null)
            {
                StopCoroutine(pulseRoutine);
                pulseRoutine = null;
            }

            if (pulseVisual == null)
                return;

            pulseVisual.localScale = pulseVisualScale;
            pulseVisual.gameObject.SetActive(false);
        }
    }
}