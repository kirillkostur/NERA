using UnityEngine;

public class GeneratorSystem : MonoBehaviour
{
    [Header("Состояние генератора")]
    [Tooltip("Починен ли генератор сейчас")]
    public bool isRepaired = false;
    [Tooltip("Игра запущена после первой починки")]
    public bool gameStarted = false;

    private RepairableObject repairable;

    private void Awake()
    {
        repairable = GetComponent<RepairableObject>();
        if (repairable != null)
            repairable.OnRepaired += OnGeneratorFixed;
    }

    private void OnDestroy()
    {
        if (repairable != null)
            repairable.OnRepaired -= OnGeneratorFixed;
    }

    private void OnGeneratorFixed(RepairableObject obj)
    {
        isRepaired = true;

        if (!gameStarted)
        {
            gameStarted = true;
            Debug.Log("⚡ Генератор впервые починен — игра началась!");
        }
        else
        {
            Debug.Log("🔧 Генератор снова починен.");
        }
    }

    /// <summary>
    /// Сломать генератор снова (для геймплея).
    /// Сбрасывает свои флаги и передаёт поломку RepairableObject.
    /// </summary>
    public void BreakGenerator()
    {
        isRepaired = false;

        if (repairable != null)
        {
            repairable.BreakObject();
        }

        Debug.Log("💥 Генератор снова сломался!");
    }
}
