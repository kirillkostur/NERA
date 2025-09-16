using UnityEngine;
using UnityEngine.AI;

public class SpiderSpawner_NavMesh : MonoBehaviour
{
    [Header("Ссылки")]
    public DayNightCycle dayNight;
    public WaveConfig waveConfig;
    public GeneratorSystem generator;

    [Header("Зона спавна")]
    public float spawnRadius = 15f;
    public Transform[] spawnPoints;
    public float navMeshCheckRadius = 30f;
    public float raycastHeightCheck = 5f;
    public LayerMask groundMask;

    private float nextSpawnTime;
    private int currentSpiders;
    private int spawnEventsDone;
    private WaveConfig.WaveData activeWave;

    private void OnEnable()
    {
        if (dayNight != null)
            dayNight.OnNewDay += ResetSpawner;
    }

    private void OnDisable()
    {
        if (dayNight != null)
            dayNight.OnNewDay -= ResetSpawner;
    }

    /// <summary>
    /// При наступлении нового дня теперь мы не удаляем пауков.
    /// Сбрасываются только счётчики спавна, чтобы новая ночь начиналась заново.
    /// </summary>
    private void ResetSpawner()
    {
        spawnEventsDone = 0;
        currentSpiders = 0;
        nextSpawnTime = 0f;
        activeWave = null;
        Debug.Log("🌅 Новый день — сброшены только счётчики (пауки остались на сцене)");
    }

    void Update()
    {
        if (dayNight == null || waveConfig == null || generator == null) return;

        if (dayNight.isNight && generator.gameStarted)
        {
            activeWave = waveConfig.GetWaveForDay(dayNight.currentDay, generator.gameStarted);
            if (activeWave == null) return;

            if (Time.time >= nextSpawnTime &&
                currentSpiders < activeWave.maxSpidersOnScene &&
                spawnEventsDone < activeWave.spawnEvents)
            {
                int count = Random.Range(activeWave.minSpidersPerSpawn, activeWave.maxSpidersPerSpawn + 1);

                for (int i = 0; i < count && currentSpiders < activeWave.maxSpidersOnScene; i++)
                    SpawnSpider();

                spawnEventsDone++;
                nextSpawnTime = Time.time + activeWave.spawnInterval;
            }
        }
    }

    private void SpawnSpider()
    {
        if (activeWave == null || activeWave.spiderTypes == null || activeWave.spiderTypes.Length == 0) return;

        Vector3 spawnPos = transform.position;

        if (activeWave.useSpawnPoints && (spawnPoints != null && spawnPoints.Length > 0))
        {
            Transform p = spawnPoints[Random.Range(0, spawnPoints.Length)];
            spawnPos = p.position;
        }
        else
        {
            if (NavMesh.SamplePosition(transform.position + Random.insideUnitSphere * spawnRadius,
                                       out NavMeshHit hit, navMeshCheckRadius, NavMesh.AllAreas))
                spawnPos = hit.position;
        }

        Vector3 rayOrigin = spawnPos + Vector3.up * raycastHeightCheck;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit rayHit, raycastHeightCheck * 2f, groundMask))
            spawnPos = rayHit.point;

        GameObject prefab = activeWave.spiderTypes[Random.Range(0, activeWave.spiderTypes.Length)];
        GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);

        // === Размер ===
        float scale = Random.Range(activeWave.minScale, activeWave.maxScale);
        go.transform.localScale = new Vector3(scale, scale, scale);

        // === Скорость от размера ===
        float t = Mathf.InverseLerp(activeWave.minScale, activeWave.maxScale, scale);
        float multiplier = Mathf.Lerp(1f + activeWave.sizeSpeedMultiplier,
                                      1f - activeWave.sizeSpeedMultiplier,
                                      t);
        multiplier = Mathf.Clamp(multiplier, 0.5f, 2f);

        float finalSpeed = activeWave.baseSpeed * multiplier;

        var ai = go.GetComponent<SpiderAI_NavMesh>();
        if (ai != null)
        {
            ai.moveSpeed = finalSpeed;
            ai.ApplySettings();
        }

        currentSpiders++;
        Debug.Log($"🕷 Spawned at {spawnPos} | Day={dayNight.currentDay} | Scale={scale:F2} | Mult={multiplier:F2} | Speed={finalSpeed:F2}");
    }

    public void SpiderDied()
    {
        currentSpiders = Mathf.Max(0, currentSpiders - 1);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);
    }
}
