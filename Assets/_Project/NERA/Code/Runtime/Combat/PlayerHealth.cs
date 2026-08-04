using System;
using System.Collections.Generic;
using NERA.Player;
using UnityEngine;

namespace NERA.Combat
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        [Header("Ragdoll")]
        [Tooltip("Animator controlling the parkour character model.")]
        [SerializeField] private Animator animator;
        [Tooltip("Root containing only skeleton ragdoll bodies.")]
        [SerializeField] private Transform ragdollRoot;
        [Tooltip("Root Rigidbody used by parkour locomotion; never treated as ragdoll.")]
        [SerializeField] private Rigidbody locomotionBody;
        [SerializeField] private Collider[] locomotionColliders;
        [SerializeField, Min(0f)] private float deathImpulse = 2.5f;
        [Tooltip("Rigidbody that receives the death impulse. Hips are recommended.")]
        [SerializeField] private Rigidbody impulseBody;

        private ParkourPlayerBridge playerBridge;
        private Rigidbody[] ragdollBodies = Array.Empty<Rigidbody>();
        private Collider[] ragdollColliders = Array.Empty<Collider>();
        private bool deathProcessed;

        public event Action<float, float> HealthChanged;
        public event Action Died;

        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsAlive => !deathProcessed && CurrentHealth > 0f;
        public bool IsRagdollActive => deathProcessed;
        public IReadOnlyList<Rigidbody> RagdollBodies => ragdollBodies;

        private void Awake()
        {
            playerBridge = GetComponent<ParkourPlayerBridge>();
            animator ??= GetComponent<Animator>();
            animator ??= GetComponentInChildren<Animator>();
            locomotionBody ??= GetComponent<Rigidbody>();

            if (locomotionColliders == null || locomotionColliders.Length == 0)
                locomotionColliders = GetComponents<CapsuleCollider>();

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

        public void Revive()
        {
            if (!deathProcessed)
            {
                RestoreFullHealth();
                return;
            }

            SetRagdollActive(false);
            deathProcessed = false;
            playerBridge?.Revive();
            if (animator != null)
            {
                animator.Rebind();
                animator.Update(0f);
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
            Vector3 inheritedVelocity = locomotionBody != null
                ? locomotionBody.linearVelocity
                : Vector3.zero;
            Vector3 inheritedAngularVelocity = locomotionBody != null
                ? locomotionBody.angularVelocity
                : Vector3.zero;
            playerBridge?.HandleDeath(
                impulseBody != null ? impulseBody.transform : ragdollRoot);

            if (playerBridge == null)
                DisableLocomotionBody();

            Vector3 impulseDirection = GetDeathImpulseDirection(source);
            SetRagdollActive(true);
            ApplyInheritedVelocity(
                inheritedVelocity,
                inheritedAngularVelocity);

            if (deathImpulse > 0f && impulseBody != null)
            {
                impulseBody.AddForce(
                    impulseDirection * deathImpulse,
                    ForceMode.Impulse);
            }

            Died?.Invoke();
            Debug.LogWarning("Player died and ragdoll was enabled.", this);
        }

        private void ApplyInheritedVelocity(
            Vector3 linearVelocity,
            Vector3 angularVelocity)
        {
            foreach (Rigidbody body in ragdollBodies)
            {
                if (body == null)
                    continue;

                body.linearVelocity = linearVelocity;
                body.angularVelocity = angularVelocity;
            }
        }

        private void CacheRagdollParts()
        {
            Transform searchRoot = ragdollRoot != null
                ? ragdollRoot
                : transform;
            Rigidbody[] foundBodies =
                searchRoot.GetComponentsInChildren<Rigidbody>(true);
            var validBodies = new List<Rigidbody>();
            var foundColliders = new List<Collider>();

            foreach (Rigidbody body in foundBodies)
            {
                if (body == null || body == locomotionBody)
                    continue;

                validBodies.Add(body);
                foreach (Collider bodyCollider in body.GetComponents<Collider>())
                {
                    if (bodyCollider == null ||
                        Array.IndexOf(locomotionColliders, bodyCollider) >= 0)
                    {
                        continue;
                    }

                    if (!foundColliders.Contains(bodyCollider))
                        foundColliders.Add(bodyCollider);
                }
            }

            ragdollBodies = validBodies.ToArray();
            ragdollColliders = foundColliders.ToArray();

            if (impulseBody == null && animator != null && animator.isHuman)
            {
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                if (hips != null)
                    impulseBody = hips.GetComponent<Rigidbody>();
            }

            if (impulseBody == null && ragdollBodies.Length > 0)
                impulseBody = ragdollBodies[0];

            if (ragdollBodies.Length == 0)
            {
                Debug.LogError(
                    "PlayerHealth: no skeleton ragdoll bodies were found. " +
                    "The locomotion Rigidbody was intentionally excluded.",
                    this);
            }
        }

        private void SetRagdollActive(bool active)
        {
            if (animator != null)
                animator.enabled = !active;

            foreach (Collider ragdollCollider in ragdollColliders)
            {
                if (ragdollCollider != null)
                    ragdollCollider.enabled = active;
            }

            foreach (Rigidbody body in ragdollBodies)
            {
                if (body == null)
                    continue;

                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.useGravity = active;
                body.detectCollisions = active;
                body.isKinematic = !active;
                if (active)
                    body.WakeUp();
                else
                    body.Sleep();
            }

            if (active)
                Physics.SyncTransforms();
        }

        private void DisableLocomotionBody()
        {
            if (locomotionBody != null)
            {
                locomotionBody.linearVelocity = Vector3.zero;
                locomotionBody.angularVelocity = Vector3.zero;
                locomotionBody.useGravity = false;
                locomotionBody.detectCollisions = false;
                locomotionBody.isKinematic = true;
            }

            foreach (Collider collider in locomotionColliders)
            {
                if (collider != null)
                    collider.enabled = false;
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
