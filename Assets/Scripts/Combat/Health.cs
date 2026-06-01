using UnityEngine;
using UnityEngine.Events;

namespace Soulsboss.Combat
{
    public class Health : MonoBehaviour, IDamageable
    {
        public Team team;
        public float maxHp = 100f;
        public float current;

        [Tooltip("Active i-frames: incoming damage ignored.")]
        public bool invincible;

        [Tooltip("Shield up: incoming damage triggers OnBlocked instead of OnDamaged.")]
        public bool guarding;

        public UnityEvent OnDamaged;
        public UnityEvent OnBlocked;
        public UnityEvent OnDeath;

        public Team Team => team;
        public bool IsAlive => current > 0f;

        void Awake()
        {
            current = maxHp;
        }

        public void TakeDamage(float amount, Vector3 hitOrigin)
        {
            if (!IsAlive) return;
            if (invincible) return;
            if (guarding) { OnBlocked?.Invoke(); return; }

            current -= amount;
            if (current <= 0f)
            {
                current = 0f;
                OnDeath?.Invoke();
            }
            else
            {
                OnDamaged?.Invoke();
            }
        }

        public void TakeDamageUnblockable(float amount, Vector3 hitOrigin)
        {
            if (!IsAlive) return;
            current -= amount;
            if (current <= 0f)
            {
                current = 0f;
                OnDeath?.Invoke();
            }
            else
            {
                OnDamaged?.Invoke();
            }
        }

        public void ResetHealth()
        {
            current = maxHp;
            invincible = false;
            guarding = false;
        }
    }
}
