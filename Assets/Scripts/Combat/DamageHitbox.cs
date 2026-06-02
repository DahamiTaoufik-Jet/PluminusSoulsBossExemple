using System.Collections.Generic;
using UnityEngine;

namespace Soulsboss.Combat
{
    public class DamageHitbox : MonoBehaviour
    {
        public Team team;
        public float damage = 20f;

        [Header("Detection zone")]
        public float radius = 1f;
        public float length = 2f;
        public Vector3 offset = Vector3.zero;

        readonly HashSet<IDamageable> hitThisSwing = new HashSet<IDamageable>();
        bool active;
        readonly Collider[] hitBuffer = new Collider[16];
        Vector3 prevTip;
        Vector3 prevBase;
        bool hasPrev;

        public bool IsActive => active;

        public void Begin()
        {
            hitThisSwing.Clear();
            active = true;
            hasPrev = false;
        }

        public void End()
        {
            active = false;
            hasPrev = false;
        }

        void Update()
        {
            if (!active) return;

            GetCapsulePoints(out Vector3 basePoint, out Vector3 tipPoint);

            // Check current position
            CheckOverlap(basePoint, tipPoint);

            // Check midpoint between previous and current to catch fast movements
            if (hasPrev)
            {
                Vector3 midBase = (prevBase + basePoint) * 0.5f;
                Vector3 midTip = (prevTip + tipPoint) * 0.5f;
                CheckOverlap(midBase, midTip);
            }

            prevBase = basePoint;
            prevTip = tipPoint;
            hasPrev = true;
        }

        void GetCapsulePoints(out Vector3 basePoint, out Vector3 tipPoint)
        {
            Vector3 center = transform.TransformPoint(offset);
            Vector3 dir = transform.right;
            float half = length * 0.5f;
            basePoint = center - dir * half;
            tipPoint = center + dir * half;
        }

        void CheckOverlap(Vector3 p0, Vector3 p1)
        {
            int count = Physics.OverlapCapsuleNonAlloc(p0, p1, radius, hitBuffer, ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                var target = hitBuffer[i].GetComponentInParent<IDamageable>();
                if (target == null) continue;
                if (target.Team == team) continue;
                if (!target.IsAlive) continue;
                if (hitThisSwing.Contains(target)) continue;

                hitThisSwing.Add(target);
                target.TakeDamage(damage, transform.position);
            }
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = active ? Color.red : new Color(1f, 0.5f, 0f, 0.4f);
            GetCapsulePoints(out Vector3 basePoint, out Vector3 tipPoint);
            Gizmos.DrawWireSphere(basePoint, radius);
            Gizmos.DrawWireSphere(tipPoint, radius);
            Gizmos.DrawLine(basePoint, tipPoint);
        }
#endif
    }
}
