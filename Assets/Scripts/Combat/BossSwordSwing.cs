using System.Collections;
using UnityEngine;

namespace Soulsboss.Combat
{
    public class BossSwordSwing : BossAttack
    {
        public DamageHitbox hitbox;
        public Transform swordPivot;

        [Header("Timing")]
        public float telegraph = 0.6f;
        public float swingDuration = 0.2f;
        public float endingLag = 1.1f;

        [Header("Swing arc (local euler angles)")]
        [Tooltip("Sword raised position before swing.")]
        public Vector3 raisedRotation = new Vector3(0f, 0f, 90f);
        [Tooltip("Sword rest / end position after swing.")]
        public Vector3 restRotation = new Vector3(0f, 0f, -45f);

        Vector3 originalRotation;

        void Awake()
        {
            if (swordPivot != null) originalRotation = swordPivot.localEulerAngles;
        }

        public override IEnumerator Execute(BossController boss)
        {
            if (swordPivot != null) originalRotation = swordPivot.localEulerAngles;

            // Telegraph: raise sword above head
            if (swordPivot != null)
            {
                yield return RotateTo(raisedRotation, telegraph);
            }
            else
            {
                yield return new WaitForSeconds(telegraph);
            }

            // Active window: swing down
            if (hitbox != null) hitbox.Begin();
            if (swordPivot != null)
            {
                yield return RotateTo(restRotation, swingDuration);
            }
            else
            {
                yield return new WaitForSeconds(swingDuration);
            }
            if (hitbox != null) hitbox.End();

            // Ending lag: sword stays down, shield stays down = punish window
            yield return new WaitForSeconds(endingLag);

            // Return to original pose
            if (swordPivot != null)
            {
                yield return RotateTo(originalRotation, 0.3f);
            }
        }

        IEnumerator RotateTo(Vector3 targetEuler, float duration)
        {
            Quaternion from = swordPivot.localRotation;
            Quaternion to = Quaternion.Euler(targetEuler);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                swordPivot.localRotation = Quaternion.Slerp(from, to, t / duration);
                yield return null;
            }
            swordPivot.localRotation = to;
        }
    }
}
