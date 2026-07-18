using System;
using System.Collections.Generic;
using UnityEngine;

namespace NERA.Combat
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        [Header("Ragdoll")]
        [Tooltip("Animator controlling the character model. Found automatically when empty.")]
        [SerializeField] private Animator animator;

        [Tooltip("Optional impulse applied to the ragdoll when the player dies.")]
        [SerializeField, Min(0f)] private float deathImpulse = 2.5f;

        [Tooltip("Rigidbody that receives the death impulse. Hips are recommended.")]
        [SerializeField] private Rigidbody impulseBody;

        private PlayerController playerController;
        private Rigidbody[] ragdollBodies = Array.Empty<Rigidbody>();
        private Collider[] ragdollColliders = Array.Empty<Collider>();
        private bool deathProcessed;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsAlive => !deathProcessed && CurrentHealth > 0f;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            CacheRagdollParts();
            SetRagdollActive(false);

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

            if (CurrentHealth <= 0f)
                Die(source);
        }

        public void Kill(GameObject source = null)
        {
            if (!IsAlive)
                return;

            CurrentHealth = 0f;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
            Die(source);
        }

        public void RestoreFullHealth()
        {
            if (deathProcessed)
            {
                Debug.LogWarning(
                    "RestoreFullHealth cannot revive a ragdolled player. " +
                    "Reload or respawn the player object instead.",
                    this);
                return;
            }

            CurrentHealth = maxHealth;
            HealthChanged?.Invoke(CurrentHealth, maxHealth);
        }

        private void Die(GameObject source)
        {
            if (deathProcessed)
                return;

            deathProcessed = true;
            CurrentHealth = 0f;

            if (playerController != null)
                playerController.HandleDeath();

            Vector3 impulseDirection = GetDeathImpulseDirection(source);
            SetRagdollActive(true);

            if (deathImpulse > 0f && impulseBody != null)
            {
                impulseBody.AddForce(
                    impulseDirection * deathImpulse,
                    ForceMode.Impulse);
            }

            Died?.Invoke();
            Debug.LogWarning("Player died and ragdoll was enabled.", this);
        }

        private void CacheRagdollParts()
        {
            ragdollBodies = GetComponentsInChildren<Rigidbody>(true);

            var foundColliders = new List<Collider>();

            foreach (Rigidbody body in ragdollBodies)
            {
                if (body == null)
                    continue;

                Collider[] bodyColliders = body.GetComponents<Collider>();

                foreach (Collider bodyCollider in bodyColliders)
                {
                    if (bodyCollider == null || bodyCollider is CharacterController)
                        continue;

                    if (!foundColliders.Contains(bodyCollider))
                        foundColliders.Add(bodyCollider);
                }
            }

            ragdollColliders = foundColliders.ToArray();

            if (impulseBody == null && animator != null && animator.isHuman)
            {
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                    impulseBody = hips.GetComponent<Rigidbody>();
            }

            if (impulseBody == null && ragdollBodies.Length > 0)
                impulseBody = ragdollBodies[0];

            Debug.Log(
                $"Ragdoll cached: {ragdollBodies.Length} rigidbodies, " +
                $"{ragdollColliders.Length} colliders.",
                this);
        }

        private void SetRagdollActive(bool active)
        {
            if (animator != null)
                animator.enabled = !active;

            foreach (Collider ragdollCollider in ragdollColliders)
            {
                if (ragdollCollider == null)
                    continue;

                ragdollCollider.enabled = active;
            }

            foreach (Rigidbody body in ragdollBodies)
            {
                if (body == null)
                    continue;

                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = active;
                body.detectCollisions = active;
                body.isKinematic = !active;

                if (active)
                    body.WakeUp();
                else
                    body.Sleep();
            }

            if (active)
            {
                Physics.SyncTransforms();

                Debug.Log(
                    $"Ragdoll enabled: {ragdollBodies.Length} rigidbodies, " +
                    $"{ragdollColliders.Length} colliders.",
                    this);
            }
        }

        private Vector3 GetDeathImpulseDirection(GameObject source)
        {
            if (source == null)
                return -transform.forward + Vector3.up * 0.2f;

            Vector3 direction = transform.position - source.transform.position;
            direction.y = Mathf.Max(direction.y, 0.2f);

            return direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : -transform.forward;
        }

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            deathImpulse = Mathf.Max(0f, deathImpulse);
        }
    }
}