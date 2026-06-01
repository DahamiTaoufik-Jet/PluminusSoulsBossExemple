using System.Collections;
using UnityEngine;

namespace Soulsboss.Combat
{
    /// <summary>
    /// Base for any boss attack. Override Execute() with a coroutine that handles
    /// windup, active hitbox window, and recovery (ending lag).
    /// The boss shield is guaranteed to be lowered for the entire duration of Execute().
    /// </summary>
    public abstract class BossAttack : MonoBehaviour
    {
        [Tooltip("Minimum distance to the target required to pick this attack.")]
        public float minRange = 0f;
        [Tooltip("Maximum distance to the target this attack can hit from.")]
        public float maxRange = 3f;
        [Tooltip("Relative weight when the boss picks an attack at random.")]
        public float weight = 1f;

        public bool IsInRange(float distance) => distance >= minRange && distance <= maxRange;

        public abstract IEnumerator Execute(BossController boss);
    }
}
