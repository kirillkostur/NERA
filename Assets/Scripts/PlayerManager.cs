using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [Header("Ссылки на компоненты игрока")]
    public PlayerController playerController;
    public PlayerTargeting playerTargeting;

    private void Awake()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (playerTargeting == null) playerTargeting = GetComponent<PlayerTargeting>();
    }

    private void Update()
    {
        // Здесь можно будет добавлять общую логику игрока
    }
}
