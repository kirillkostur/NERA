using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerHealth : MonoBehaviour
{
    [Header("Здоровье и броня")]
    public int maxArmor = 50;
    public int maxHealth = 100;

    public bool IsDead { get; private set; }

    [HideInInspector] public int currentArmor;
    [HideInInspector] public int currentHealth;
    private Animator animator;

    void Awake()
    {
        currentArmor = maxArmor;
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        IsDead = false;
    }

    public void TakeDamage(int amount)
    {
        if (IsDead) return;

        if (currentArmor > 0)
        {
            int absorbed = Mathf.Min(currentArmor, amount);
            currentArmor -= absorbed;
            amount -= absorbed;
        }
        if (amount > 0) currentHealth -= amount;

        if (currentHealth <= 0) Die();
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        // Сообщаем менеджеру, чтобы отключил управление и таргет
        var manager = GetComponent<PlayerManager>();
        if (manager != null) manager.OnPlayerDeath();

        // Выключаем CharacterController сейчас и на следующем кадре (на случай гонки)
        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        StartCoroutine(EnsureCCDisabledNextFrame());

        if (animator != null) animator.enabled = false;

        // Включаем физику на костях
        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = true;
    }

    private IEnumerator EnsureCCDisabledNextFrame()
    {
        yield return null; // дождались конца кадра
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
    }
}
