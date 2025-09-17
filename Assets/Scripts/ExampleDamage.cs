using UnityEngine;

public class ExampleDamage : MonoBehaviour
{
    public GeneratorSystem generator; // Перетащи сюда объект генератора в инспекторе

    void Update()
    {
        // Тест: при нажатии R ломаем генератор
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (generator != null)
            {
                generator.BreakGenerator();
            }
        }
    }
}
