using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Спавнер пауков. Работает только ночью и только если StationBatterySystem.Instance.GameStarted == true.
/// </summary>
public class SpiderSpawner_NavMesh : MonoBehaviour
{
    [Header("Ссылки")]
    public DayNightCycle dayNight;
    public WaveConfig waveConfig;

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
    private int activeWaveIndex = -1;

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

    private void ResetSpawner()
    {
        spawnEventsDone = 0;
        currentSpiders = 0;
        nextSpawnTime = 0f;
        activeWave = null;
        activeWaveIndex = -1;
        Logger.Log("🌅 Новый день — сброшены счётчики спавна пауков");
    }

    private void Update()
    {
        if (dayNight == null || waveConfig == null) return;

        var battery = StationBatterySystem.Instance;
        bool gameStarted = (battery != null && battery.GameStarted);

        if (!gameStarted) return;      // игра ещё не стартовала
        if (!dayNight.isNight) return; // спавним только ночью

        activeWave = waveConfig.GetWaveForDay(dayNight.currentDay, true);
        if (activeWave == null) return;

        // Находим индекс активной волны
        for (int i = 0; i < waveConfig.waves.Length; i++)
        {
            if (waveConfig.waves[i] == activeWave)
            {
                activeWaveIndex = i;
                break;
            }
        }

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

    private void SpawnSpider()
    {
        if (activeWave == null || activeWave.spiderTypes == null || activeWave.spiderTypes.Length == 0) return;

        Vector3 spawnPos = transform.position;

        if (activeWave.useSpawnPoints && spawnPoints != null && spawnPoints.Length > 0)
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

        // Масштаб паука
        float scale = Random.Range(activeWave.minScale, activeWave.maxScale);
        go.transform.localScale = new Vector3(scale, scale, scale);

        // Рассчёт скорости с учётом размера
        float t = Mathf.InverseLerp(activeWave.minScale, activeWave.maxScale, scale);
        float mult = Mathf.Lerp(1f + activeWave.sizeSpeedMultiplier, 1f - activeWave.sizeSpeedMultiplier, t);
        mult = Mathf.Clamp(mult, 0.5f, 2f);

        float finalSpeed = activeWave.baseSpeed * mult;

        // Настройка AI
        var ai = go.GetComponent<SpiderAI_NavMesh>();
        if (ai != null)
        {
            ai.moveSpeed = finalSpeed;
            ai.ApplySettings();
            ai.waveConfig = waveConfig;        // 👈 передаём конфиг
            ai.waveIndex = activeWaveIndex;    // 👈 передаём индекс волны
        }

        // Настройка здоровья
        var health = go.GetComponent<SpiderHealth>();
        if (health != null)
        {
            health.waveConfig = waveConfig;
            health.waveIndex = activeWaveIndex;
        }

        currentSpiders++;
        Logger.Log($"🕷 Spawned | Day={dayNight.currentDay} | Scale={scale:F2} | Speed={finalSpeed:F2}");
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
