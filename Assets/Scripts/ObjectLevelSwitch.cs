using UnityEngine;

public class ObjectLevelSwitch : MonoBehaviour
{
    [Header("Дочерние уровни турели (каждый со своей логикой)")]
    public GameObject[] turretLevels; // Lvl1, Lvl2, Lvl3 — готовые под-объекты со скриптами

    [Header("Эффект апгрейда")]
    public GameObject upgradeEffectPrefab;

    [Tooltip("Текущий активный уровень (0 = базовый)")]
    public int currentLevel = 0;

    private void Start()
    {
        ApplyLevel(currentLevel, false);
    }

    /// <summary>
    /// Повышает уровень турели на один.
    /// </summary>
    public void UpgradeToNext()
    {
        int nextLevel = Mathf.Min(currentLevel + 1, turretLevels.Length - 1);
        if (nextLevel != currentLevel)
        {
            ApplyLevel(nextLevel, true);
        }
    }

    /// <summary>
    /// Принудительное переключение на заданный уровень.
    /// </summary>
    public void ApplyLevel(int level, bool playEffect = true)
    {
        if (turretLevels == null || turretLevels.Length == 0) return;

        level = Mathf.Clamp(level, 0, turretLevels.Length - 1);

        // Выключаем все уровни
        for (int i = 0; i < turretLevels.Length; i++)
        {
            if (turretLevels[i] != null)
                turretLevels[i].SetActive(i == level);
        }

        // Проигрываем эффект апгрейда и удаляем его после проигрывания
        if (playEffect && upgradeEffectPrefab != null)
        {
            GameObject fx = Instantiate(upgradeEffectPrefab, transform.position, Quaternion.identity);
            // Попробуем получить длительность из ParticleSystem
            float duration = 2f;
            ParticleSystem ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                duration = ps.main.duration + ps.main.startLifetime.constantMax;
            }
            Destroy(fx, duration);
        }

        currentLevel = level;
        Debug.Log($"🔧 Турель переключена на уровень {currentLevel + 1}");
    }

    /// <summary>
    /// Сбрасывает на первый уровень.
    /// </summary>
    public void ResetToLevel(int level = 0)
    {
        ApplyLevel(level, false);
    }
}
