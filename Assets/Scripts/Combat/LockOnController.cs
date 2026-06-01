using UnityEngine;
using UnityEngine.InputSystem;

namespace Soulsboss.Combat
{
    public class LockOnController : MonoBehaviour
    {
        public Transform referenceTransform;
        public Camera referenceCamera;
        public float maxRange = 25f;
        [Range(0f, 180f)] public float maxAngle = 80f;

        public LockOnTarget Current { get; private set; }
        public bool IsLocked => Current != null && Current.isActiveAndEnabled;

        void Awake()
        {
            if (referenceTransform == null) referenceTransform = transform;
            if (referenceCamera == null) referenceCamera = Camera.main;
        }

        void LateUpdate()
        {
            if (Current == null) return;
            if (!Current.isActiveAndEnabled) { Current = null; return; }
            var d = Current.Pivot.position - referenceTransform.position;
            if (d.sqrMagnitude > maxRange * maxRange * 1.5f) Current = null;
        }

        public void Toggle()
        {
            if (Current != null) { Current = null; return; }
            Current = PickBestTarget();
        }

        public void Clear()
        {
            Current = null;
        }

        void OnLockOn(InputValue v)
        {
            if (v.isPressed) Toggle();
        }

        LockOnTarget PickBestTarget()
        {
            LockOnTarget best = null;
            float bestScore = float.PositiveInfinity;
            Vector3 origin = referenceTransform.position;
            Vector3 forward = referenceCamera != null ? referenceCamera.transform.forward : referenceTransform.forward;
            float cosLimit = Mathf.Cos(maxAngle * Mathf.Deg2Rad);
            float rangeSqr = maxRange * maxRange;

            for (int i = 0; i < LockOnTarget.All.Count; i++)
            {
                var t = LockOnTarget.All[i];
                if (t == null || !t.isActiveAndEnabled) continue;
                Vector3 to = t.Pivot.position - origin;
                float distSqr = to.sqrMagnitude;
                if (distSqr > rangeSqr) continue;
                float dist = Mathf.Sqrt(distSqr);
                if (dist < 0.0001f) continue;
                float dot = Vector3.Dot(forward, to / dist);
                if (dot < cosLimit) continue;

                // score: closer to screen center wins, distance is secondary
                float score = (1f - dot) * 10f + dist;
                if (score < bestScore) { bestScore = score; best = t; }
            }
            return best;
        }
    }
}
