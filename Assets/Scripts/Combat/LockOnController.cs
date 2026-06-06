using UnityEngine;
using UnityEngine.InputSystem;

namespace Soulsboss.Combat
{
    /// <summary>
    /// Systeme de lock-on camera du joueur.
    /// Cherche le Health ennemi le plus proche dans le champ de vision.
    /// </summary>
    public class LockOnController : MonoBehaviour
    {
        public Transform referenceTransform;
        public Camera referenceCamera;
        public float maxRange = 25f;
        [Range(0f, 180f)] public float maxAngle = 80f;

        [Tooltip("Equipe a ignorer (generalement l'equipe du joueur).")]
        public Team ignoreTeam = Team.Player;

        public Transform CurrentTarget { get; private set; }
        public bool IsLocked => CurrentTarget != null && CurrentTarget.gameObject.activeInHierarchy;

        void Awake()
        {
            if (referenceTransform == null) referenceTransform = transform;
            if (referenceCamera == null) referenceCamera = Camera.main;
        }

        void LateUpdate()
        {
            if (CurrentTarget == null) return;
            if (!CurrentTarget.gameObject.activeInHierarchy) { CurrentTarget = null; return; }
            var d = CurrentTarget.position - referenceTransform.position;
            if (d.sqrMagnitude > maxRange * maxRange * 1.5f) CurrentTarget = null;
        }

        public void Toggle()
        {
            if (CurrentTarget != null) { CurrentTarget = null; return; }
            CurrentTarget = PickBestTarget();
        }

        public void Clear()
        {
            CurrentTarget = null;
        }

        void OnLockOn(InputValue v)
        {
            if (v.isPressed) Toggle();
        }

        Transform PickBestTarget()
        {
            Transform best = null;
            float bestScore = float.PositiveInfinity;
            Vector3 origin = referenceTransform.position;
            Vector3 forward = referenceCamera != null
                ? referenceCamera.transform.forward
                : referenceTransform.forward;
            float cosLimit = Mathf.Cos(maxAngle * Mathf.Deg2Rad);
            float rangeSqr = maxRange * maxRange;

            var healths = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < healths.Length; i++)
            {
                Health h = healths[i];
                if (h == null || !h.IsAlive) continue;
                if (h.team == ignoreTeam) continue;

                Vector3 to = h.transform.position - origin;
                float distSqr = to.sqrMagnitude;
                if (distSqr > rangeSqr) continue;
                float dist = Mathf.Sqrt(distSqr);
                if (dist < 0.0001f) continue;
                float dot = Vector3.Dot(forward, to / dist);
                if (dot < cosLimit) continue;

                float score = (1f - dot) * 10f + dist;
                if (score < bestScore) { bestScore = score; best = h.transform; }
            }
            return best;
        }
    }
}
