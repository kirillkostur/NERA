using System;
using UnityEngine;

namespace NERA.Combat
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsAlive => CurrentHealth > 0f;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (!IsAlive || amount <= 0f)
                return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            HealthChanged?.Invoke(CurrentHealth, maxHealth);

            Debug.Log(
                $"Player received {amount:0.#} energy damage from " +
                $"{(source != null ? source.name : "unknown source")}. " +
                $"Health: {CurrentHealth:0.#}/{maxHealth:0.#}",
                this);

            if (IsAlive)
                return;

            PlayerController controller = GetComponent<PlayerController>();
            if (controller != null)
                controller.SetInputEnabled(false);

            Died?.Invoke();
            Debug.LogWarning("Player was defeated by IO energy.", this);
        }

        public void RestoreFullHealth()
        {
            CurrentHealth = maxHealth;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
        }
    }
}
