using System.Collections.Generic;
using UnityEngine;

namespace Soulsboss.Combat
{
    [RequireComponent(typeof(Collider))]
    public class DamageHitbox : MonoBehaviour
    {
        public Team team;
        public float damage = 20f;

        Collider col;
        readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();

        void Awake()
        {
            col = GetComponent<Collider>();
            col.isTrigger = true;
            col.enabled = false;
        }

        public void Begin()
        {
            hitThisSwing.Clear();
            col.enabled = true;
        }

        public void End()
        {
            col.enabled = false;
        }

        void OnTriggerEnter(Collider other)
        {
            var target = other.GetComponentInParent<IDamageable>();
            if (target == null) return;
            if (target.Team == team) return;
            if (!target.IsAlive) return;
            if (hitThisSwing.Contains(target)) return;

            hitThisSwing.Add(target);
            target.TakeDamage(damage, transform.position);
        }
    }
}
