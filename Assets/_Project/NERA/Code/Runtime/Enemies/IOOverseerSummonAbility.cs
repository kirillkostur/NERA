using UnityEngine;

namespace NERA.Enemies
{
    [DisallowMultipleComponent]
    public sealed class IOOverseerSummonAbility : IOEnemyAbility
    {
        [SerializeField] private GameObject[] reinforcementPrefabs;
        [SerializeField] private float[] healthThresholds = { 0.66f, 0.33f };
        [SerializeField, Min(0.5f)] private float spawnRadius = 2.5f;

        private bool[] triggeredThresholds;

        protected override void OnBound()
        {
            int count = healthThresholds != null
                ? healthThresholds.Length
                : 0;
            triggeredThresholds = new bool[count];
        }

        protected override void OnHealthChanged(
            float previousNormalized,
            float currentNormalized)
        {
            if (currentNormalized <= 0f ||
                healthThresholds == null ||
                triggeredThresholds == null)
            {
                return;
            }

            for (int index = 0; index < healthThresholds.Length; index++)
            {
                float threshold = Mathf.Clamp01(healthThresholds[index]);
                if (triggeredThresholds[index] ||
                    previousNormalized <= threshold ||
                    currentNormalized > threshold)
                {
                    continue;
                }

                triggeredThresholds[index] = true;
                SpawnWave(index);
            }
        }

        private void SpawnWave(int waveIndex)
        {
            if (Enemy == null || reinforcementPrefabs == null)
                return;

            int count = reinforcementPrefabs.Length;
            for (int index = 0; index < count; index++)
            {
                GameObject prefab = reinforcementPrefabs[index];
                if (prefab == null)
                    continue;

                float angle =
                    (index + waveIndex * 0.5f) *
                    Mathf.PI * 2f /
                    Mathf.Max(1, count);
                Vector3 offset = new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle)) * spawnRadius;
                GameObject spawned = Instantiate(
                    prefab,
                    Enemy.transform.position + offset,
                    Enemy.transform.rotation);
                spawned.name = prefab.name + "_Summoned";

                IOEnemyController controller =
                    spawned.GetComponent<IOEnemyController>();
                controller?.ConfigureAsSummonedInstance();
            }
        }
    }
}