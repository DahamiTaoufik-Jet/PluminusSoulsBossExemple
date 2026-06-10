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
        [Tooltip("Fired after death when the entity is fully disabled.")]
        public UnityEvent OnDisabled;

        public Team Team => team;
        public bool IsAlive => current > 0f;

        [Tooltip("Delay before disabling the GameObject after death.")]
        public float disableDelay = 1.5f;

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
                if (!IsAlive) StartCoroutine(DisableAfterDelay());
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
                if (!IsAlive) StartCoroutine(DisableAfterDelay());
            }
            else
            {
                OnDamaged?.Invoke();
            }
        }

        System.Collections.IEnumerator DisableAfterDelay()
        {
            yield return new WaitForSeconds(disableDelay);
            OnDisabled?.Invoke();
            gameObject.SetActive(false);
        }

        public void ResetHealth()
        {
            StopAllCoroutines();
            gameObject.SetActive(true);
            current = maxHp;
            invincible = false;
            guarding = false;
        }
    }
}
