using UnityEngine;

namespace NERA.Combat
{
    public interface IDamageable
    {
        bool IsAlive { get; }

        void TakeDamage(float amount, GameObject source);
    }
}
