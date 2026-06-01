using UnityEngine;

namespace Soulsboss.Combat
{
    public enum Team { Player, Boss }

    public interface IDamageable
    {
        Team Team { get; }
        bool IsAlive { get; }
        void TakeDamage(float amount, Vector3 hitOrigin);
        void TakeDamageUnblockable(float amount, Vector3 hitOrigin);
    }
}
