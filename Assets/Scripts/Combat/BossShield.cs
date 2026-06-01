using System.Collections;
using UnityEngine;

namespace Soulsboss.Combat
{
    public class BossShield : MonoBehaviour
    {
        public Health health;
        public Transform shieldTransform;

        [Header("Guard position (front)")]
        public Vector3 guardLocalPos = new Vector3(0f, 0.5f, 0.5f);
        public Vector3 guardLocalRot = Vector3.zero;

        [Header("Lowered position (side)")]
        public Vector3 loweredLocalPos = new Vector3(-0.6f, 0.2f, 0f);
        public Vector3 loweredLocalRot = new Vector3(0f, 0f, -45f);

        public float transitionSpeed = 8f;

        public bool IsRaised { get; private set; }

        Vector3 targetPos;
        Quaternion targetRot;

        void Awake()
        {
            if (health == null) health = GetComponentInParent<Health>();
            Raise();
        }

        void Update()
        {
            if (shieldTransform == null) return;
            shieldTransform.localPosition = Vector3.Lerp(shieldTransform.localPosition, targetPos, transitionSpeed * Time.deltaTime);
            shieldTransform.localRotation = Quaternion.Slerp(shieldTransform.localRotation, targetRot, transitionSpeed * Time.deltaTime);
        }

        public void Raise()
        {
            IsRaised = true;
            if (health != null) health.guarding = true;
            targetPos = guardLocalPos;
            targetRot = Quaternion.Euler(guardLocalRot);
        }

        public void Lower()
        {
            IsRaised = false;
            if (health != null) health.guarding = false;
            targetPos = loweredLocalPos;
            targetRot = Quaternion.Euler(loweredLocalRot);
        }
    }
}
