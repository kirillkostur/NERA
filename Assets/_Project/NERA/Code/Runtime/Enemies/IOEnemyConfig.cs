using UnityEngine;
using NERA.Localization;

namespace NERA.Enemies
{
    [CreateAssetMenu(fileName = "IOEnemyConfig", menuName = "NERA/IO/Enemy Config")]
    public sealed class IOEnemyConfig : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string enemyId = "io_blue";
        [SerializeField] private string displayName = "Blue IO";

        [Header("Movement")]
        [SerializeField, Min(1f)] private float maxHealth = 30f;
        [SerializeField, Min(1f)] private float detectionRadius = 10f;
        [SerializeField, Min(0.5f)] private float attackRange = 6f;
        [SerializeField, Min(0f)] private float moveSpeed = 2f;
        [SerializeField, Min(0.1f)] private float hoverHeight = 1.6f;
        [SerializeField, Min(0f)] private float hoverAmplitude = 0.15f;
        [SerializeField, Min(0.1f)] private float hoverFrequency = 2f;

        [Header("Energy Attack")]
        [SerializeField, Min(0.1f)] private float attackCooldown = 2f;
        [SerializeField, Min(0.1f)] private float projectileSpeed = 8f;
        [SerializeField, Min(0.1f)] private float projectileLifetime = 4f;
        [SerializeField, Min(0f)] private float projectileDamage = 10f;
        [SerializeField, Min(0.01f)] private float projectileScale = 0.22f;
        [SerializeField] private GameObject projectilePrefab;
        [SerializeField] private Color energyColor = new Color(0.1f, 0.65f, 1f);
        [SerializeField, Min(0f)] private float emissionIntensity = 2.5f;
        [SerializeField, Min(0f)] private float projectileEmissionIntensity = 4f;

        [Header("Death")]
        [SerializeField] private GameObject deathDropPrefab;
        [SerializeField] private Vector3 deathDropOffset = Vector3.zero;

        public string EnemyId => enemyId;
        public string DisplayName => NERALocalization.Content(
            "enemy", enemyId, "name", displayName);
        public float MaxHealth => Mathf.Max(1f, maxHealth);
        public float DetectionRadius => Mathf.Max(1f, detectionRadius);
        public float AttackRange => Mathf.Clamp(attackRange, 0.5f, DetectionRadius);
        public float MoveSpeed => Mathf.Max(0f, moveSpeed);
        public float HoverHeight => Mathf.Max(0.1f, hoverHeight);
        public float HoverAmplitude => Mathf.Max(0f, hoverAmplitude);
        public float HoverFrequency => Mathf.Max(0.1f, hoverFrequency);
        public float AttackCooldown => Mathf.Max(0.1f, attackCooldown);
        public float ProjectileSpeed => Mathf.Max(0.1f, projectileSpeed);
        public float ProjectileLifetime => Mathf.Max(0.1f, projectileLifetime);
        public float ProjectileDamage => Mathf.Max(0f, projectileDamage);
        public float ProjectileScale => Mathf.Max(0.01f, projectileScale);
        public GameObject ProjectilePrefab => projectilePrefab;
        public Color EnergyColor => energyColor;
        public float EmissionIntensity => Mathf.Max(0f, emissionIntensity);
        public float ProjectileEmissionIntensity => Mathf.Max(0f, projectileEmissionIntensity);
        public GameObject DeathDropPrefab => deathDropPrefab;
        public Vector3 DeathDropOffset => deathDropOffset;

        private void OnValidate()
        {
            enemyId = enemyId?.Trim();
            displayName = displayName?.Trim();
        }
    }
}
