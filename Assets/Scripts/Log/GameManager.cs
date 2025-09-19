using UnityEngine;

public class GameManager : MonoBehaviour
{
    private void Awake()
    {
#if UNITY_EDITOR
        Logger.Enabled = true;   // ✅ Включаем логи только в редакторе для отладки
#else
        Logger.Enabled = false;  // 🚫 Отключаем логи в билде
#endif
    }
}


