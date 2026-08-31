using NERA.Combat;
using UnityEngine;

namespace NERA.Enemies
{
    [DisallowMultipleComponent]
    public sealed class IOPowerDisruptionAbility : IOEnemyAbility
    {
        [SerializeField, Min(0f)] private float initialDelay = 4f;
        [SerializeField, Min(0.5f)] private float cooldown = 12f;
        [SerializeField, Min(0.1f)] private float radius = 8f;
        [SerializeField] private LayerMask affectedLayers = ~0;
        [SerializeField] private Color pulseColor =
            new Color(0.72f, 0.22f, 1f);

        private float nextCastAt;

        public float Radius => Mathf.Max(0.1f, radius);
        public float Cooldown => Mathf.Max(0.5f, cooldown);
        public int CastCount { get; private set; }
        public int LastAffectedCount { get; private set; }

        protected override void OnBound()
        {
            nextCastAt = Time.time + Mathf.Max(0f, initialDelay);
        }

        protected override void OnTick(float _)
        {
            if (Enemy == null || Time.time < nextCastAt)
                return;

            nextCastAt = Time.time + Cooldown;
            CastPowerDisruption();
        }

        public void CastPowerDisruption()
        {
            if (Enemy == null || !Enemy.IsAlive)
                return;

            LastAffectedCount = AnomalyPowerPulse.DisablePermanently(
                Enemy.transform.position,
                Radius,
                affectedLayers,
                Enemy.gameObject,
                "IO enemy power disruption");
            CastCount++;
            DrawPulse();
        }

        private void DrawPulse()
        {
            Vector3 center = Enemy.transform.position;
            Debug.DrawLine(
                center - Vector3.right * Radius,
                center + Vector3.right * Radius,
                pulseColor,
                0.6f);
            Debug.DrawLine(
                center - Vector3.forward * Radius,
                center + Vector3.forward * Radius,
                pulseColor,
                0.6f);
        }

        private void OnValidate()
        {
            initialDelay = Mathf.Max(0f, initialDelay);
            cooldown = Mathf.Max(0.5f, cooldown);
            radius = Mathf.Max(0.1f, radius);
        }
    }
}
