using UnityEngine;

namespace NERA.Enemies
{
    /// <summary>
    /// Base component for opt-in IO behavior modules.
    /// The enemy controller owns lifecycle dispatch so ability components do not
    /// duplicate targeting, health, persistence, quest, or drop logic.
    /// </summary>
    public abstract class IOEnemyAbility : MonoBehaviour
    {
        protected IOEnemyController Enemy { get; private set; }

        internal void Bind(IOEnemyController enemy)
        {
            Enemy = enemy;
            OnBound();
        }

        internal void TickAbility(float deltaTime)
        {
            if (Enemy != null && Enemy.IsAlive)
                OnTick(deltaTime);
        }

        internal void NotifyHealthChanged(
            float previousNormalized,
            float currentNormalized)
        {
            OnHealthChanged(previousNormalized, currentNormalized);
        }

        internal void NotifyDied()
        {
            OnEnemyDied();
        }

        protected virtual void OnBound()
        {
        }

        protected virtual void OnTick(float deltaTime)
        {
        }

        protected virtual void OnHealthChanged(
            float previousNormalized,
            float currentNormalized)
        {
        }

        protected virtual void OnEnemyDied()
        {
        }
    }

    /// <summary>
    /// Optional replacement for the controller's basic single-shot attack.
    /// Only one attack ability should be placed on an IO prefab.
    /// </summary>
    public abstract class IOEnemyAttackAbility : IOEnemyAbility
    {
        public abstract void TickAttack(Transform target);
    }
}
