using UnityEngine;

public class GeneratorSystem : MonoBehaviour
{
    public bool isRepaired = false;     // Текущее состояние генератора
    public bool gameStarted = false;    // Флаг: уже ли запущен цикл дней и спавн

    void Start()
    {
        RepairableObject repair = GetComponent<RepairableObject>();
        if (repair != null)
        {
            repair.OnRepaired += OnGeneratorFixed;
        }
    }

    private void OnGeneratorFixed(RepairableObject obj)
    {
        isRepaired = true;

        // Первый ремонт → запуск игры
        if (!gameStarted)
        {
            gameStarted = true;
            Debug.Log("⚡ Генератор впервые починен — игра началась!");
        }
        else
        {
            Debug.Log("🔧 Генератор снова починен (для геймплея).");
        }
    }

    // В будущем можно добавить метод для "поломки"
    public void BreakGenerator()
    {
        isRepaired = false;
        Debug.Log("💥 Генератор снова сломался!");
    }
}
