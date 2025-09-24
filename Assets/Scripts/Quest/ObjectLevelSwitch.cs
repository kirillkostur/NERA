using UnityEngine;

[RequireComponent(typeof(Identifiable))]
public class ObjectLevelSwitch : MonoBehaviour
{
    [Header("Уникальный ID для связывания с UpgradeConfig")]
    public string upgradeID;

    [Header("Список уровней апгрейда (0 = базовый, далее повышенные)")]
    public GameObject[] levels;

    [Header("Эффект апгрейда")]
    public GameObject upgradeEffectPrefab;

    [Tooltip("Текущий активный уровень (0 = базовый)")]
    public int currentLevel = 0;

    /// <summary> true, если достигнут максимальный уровень </summary>
    public bool IsMaxLevel => currentLevel >= levels.Length - 1;

    private void Start()
    {
        ApplyLevel(currentLevel, false);
    }

    /// <summary>Применяет уровень, включает нужный объект, выключает остальные</summary>
    public void ApplyLevel(int level, bool playEffect = true)
    {
        if (levels == null || levels.Length == 0) return;
        level = Mathf.Clamp(level, 0, levels.Length - 1);

        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] != null)
                levels[i].SetActive(i == level);
        }

        if (playEffect && upgradeEffectPrefab != null)
        {
            GameObject fx = Instantiate(upgradeEffectPrefab, transform.position, Quaternion.identity);
            float duration = 2f;
            ParticleSystem ps = fx.GetComponent<ParticleSystem>();
            if (ps != null)
                duration = ps.main.duration + ps.main.startLifetime.constantMax;
            Destroy(fx, duration);
        }

        currentLevel = level;
        Debug.Log($"🔧 {upgradeID} теперь уровень {currentLevel + 1}");
    }

    /// <summary>Повышает уровень на 1, если не достигнут максимум</summary>
    public void UpgradeToNext()
    {
        if (IsMaxLevel) return;
        ApplyLevel(currentLevel + 1, true);

        // currentLevel уже обновлён -> отдаём новый уровень
        var ident = GetComponent<Identifiable>();
        GameEvents.RaiseQuestEvent(new QuestEventData(
            QuestEventType.UpgradeObj,
            ident.Id,
            currentLevel
        ));
    }

    public void ResetToLevel(int level = 0)
    {
        ApplyLevel(level, false);
    }
}
