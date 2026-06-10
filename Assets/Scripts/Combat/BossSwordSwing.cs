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

        [Header("Raised pose (windup)")]
        public Vector3 raisedPosition = new Vector3(0f, 1f, 0f);
        public Vector3 raisedRotation = new Vector3(0f, 0f, 90f);

        [Header("Strike pose (end of swing)")]
        public Vector3 strikePosition = new Vector3(0f, 0f, 0.5f);
        public Vector3 strikeRotation = new Vector3(0f, 0f, -45f);

        Vector3 originalPosition;
        Vector3 originalRotation;

        public override IEnumerator Execute(BossController boss)
        {
            if (swordPivot != null)
            {
                originalPosition = swordPivot.localPosition;
                originalRotation = swordPivot.localEulerAngles;
            }

            // Telegraph: raise sword
            if (swordPivot != null)
                yield return MoveTo(raisedPosition, raisedRotation, telegraph);
            else
                yield return new WaitForSeconds(telegraph);

            // Active window: swing down
            boss.IsInActiveStrike = true;
            if (hitbox != null) hitbox.Begin();
            if (swordPivot != null)
                yield return MoveTo(strikePosition, strikeRotation, swingDuration);
            else
                yield return new WaitForSeconds(swingDuration);
            if (hitbox != null) hitbox.End();
            boss.IsInActiveStrike = false;

            // Ending lag
            yield return new WaitForSeconds(endingLag);

            // Return to original pose
            if (swordPivot != null)
                yield return MoveTo(originalPosition, originalRotation, 0.3f);
        }

        IEnumerator MoveTo(Vector3 targetPos, Vector3 targetEuler, float duration)
        {
            Vector3 fromPos = swordPivot.localPosition;
            Quaternion fromRot = swordPivot.localRotation;
            Quaternion toRot = Quaternion.Euler(targetEuler);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float ratio = t / duration;
                swordPivot.localPosition = Vector3.Lerp(fromPos, targetPos, ratio);
                swordPivot.localRotation = Quaternion.Slerp(fromRot, toRot, ratio);
                yield return null;
            }
            swordPivot.localPosition = targetPos;
            swordPivot.localRotation = toRot;
        }
    }
}
