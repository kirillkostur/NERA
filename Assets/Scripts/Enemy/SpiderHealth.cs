using UnityEngine;

[RequireComponent(typeof(SpiderAI_NavMesh))]
public class SpiderHealth : MonoBehaviour
{
    [Header("Эффекты")]
    public GameObject deathEffect;         // Эффект при смерти
    public GameObject hitEffectPrefab;     // 👈 Эффект при попадании
    public Transform hitPoint;             // 👈 Точка появления эффекта попадания

    [HideInInspector] public WaveConfig waveConfig; // Назначается спавнером
    [HideInInspector] public int waveIndex;         // Назначается спавнером

    private int currentHP;
    private SpiderAI_NavMesh spiderAI;

    private void Awake()
    {
        spiderAI = GetComponent<SpiderAI_NavMesh>();
    }

    private void Start()
    {
        if (waveConfig == null || waveConfig.waves == null || waveIndex < 0 || waveIndex >= waveConfig.waves.Length)
        {
            Debug.LogWarning($"[SpiderHealth] WaveConfig не передан корректно для {name}, использую запасное HP=20.");
            currentHP = 20;
            return;
        }

        var waveData = waveConfig.waves[waveIndex];
        currentHP = waveConfig.GetHealthByScale(transform.localScale.x, waveData);
    }

    public void TakeDamage(int amount)
    {
        if (currentHP <= 0) return;

        currentHP -= amount;

        // 👇 Создаём эффект попадания
        if (hitEffectPrefab != null)
        {
            Vector3 spawnPos = hitPoint != null ? hitPoint.position : transform.position;
            GameObject fx = Instantiate(hitEffectPrefab, spawnPos, Quaternion.identity);
            Destroy(fx, 1.5f); // Уничтожаем эффект попадания
        }

        if (currentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (deathEffect != null)
        {
            GameObject fx = Instantiate(deathEffect, transform.position, Quaternion.identity);
            Destroy(fx, 3f); // удаляем эффект смерти
        }

        spiderAI.Die();

        // 👇 ДОБАВЛЕНО: уведомим систему квестов
        GameEvents.RaiseSpiderKilled(this);

        Destroy(gameObject, 0.2f);
    }
}
