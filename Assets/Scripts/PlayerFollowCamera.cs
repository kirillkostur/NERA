using UnityEngine;

public class PlayerFollowCamera : MonoBehaviour
{
    [Header("Основные настройки")]
    public Transform target;          // Персонаж, за которым следим
    public Vector3 offset = new Vector3(0, 10f, -10f); // Смещение относительно персонажа
    public float followSpeed = 5f;    // Скорость следования камеры
    public float rotationSpeed = 5f;  // Скорость вращения камеры

    [Header("Дополнительные параметры")]
    public bool lockRotation = true;  // Фиксировать угол обзора
    public Vector3 lockedRotation = new Vector3(45f, 45f, 0); // Угол камеры при фиксации

    void LateUpdate()
    {
        if (target == null) return;

        // Желаемая позиция камеры с учётом смещения
        Vector3 desiredPosition = target.position + offset;

        // Плавное перемещение камеры
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

        if (lockRotation)
        {
            // Фиксированный угол камеры
            Quaternion desiredRotation = Quaternion.Euler(lockedRotation);
            transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
        }
        else
        {
            // Камера всегда "смотрит" на персонажа
            Quaternion desiredRotation = Quaternion.LookRotation(target.position - transform.position);
            transform.rotation = Quaternion.Lerp(transform.rotation, desiredRotation, rotationSpeed * Time.deltaTime);
        }
    }
}
