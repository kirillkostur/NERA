using UnityEngine;

public class ObjectLevelSwitch : MonoBehaviour
{
    [Header("Уникальный ID для апгрейда (должен совпадать с конфигом)")]
    public string upgradeID;

    [Header("Дочерние уровни турели (каждый со своей логикой)")]
    public GameObject[] turretLevels;

    [Header("Эффект апгрейда")]
    public GameObject upgradeEffectPrefab;

    [Tooltip("Текущий активный уровень (0 = базовый)")]
    public int currentLevel = 0;

    private void Start()
    {
        ApplyLevel(currentLevel, false);
    }

    public void UpgradeToNext()
    {
        int nextLevel = Mathf.Min(currentLevel + 1, turretLevels.Length - 1);
        if (nextLevel != currentLevel)
        {
            ApplyLevel(nextLevel, true);
        }
    }

    public void ApplyLevel(int level, bool playEffect = true)
    {
        if (turretLevels == null || turretLevels.Length == 0) return;

        level = Mathf.Clamp(level, 0, turretLevels.Length - 1);

        for (int i = 0; i < turretLevels.Length; i++)
        {
            if (turretLevels[i] != null)
                turretLevels[i].SetActive(i == level);
        }

        if (playEffect && upgradeEffectPrefab != null)
        {
            GameObject fx = Instantiate(upgradeEffectPrefab, transform.position, Quaternion.identity);
            float duration = 2f;
            ParticleSystem ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                duration = ps.main.duration + ps.main.startLifetime.constantMax;
            }
            Destroy(fx, duration);
        }

        currentLevel = level;
        Debug.Log($"🔧 Турель {upgradeID} переключена на уровень {currentLevel + 1}");
    }

    public void ResetToLevel(int level = 0)
    {
        ApplyLevel(level, false);
    }
}
