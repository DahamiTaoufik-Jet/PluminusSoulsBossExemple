using System.Collections;
using UnityEngine;

namespace Soulsboss.Combat
{
    public class BossThrust : BossAttack
    {
        public DamageHitbox hitbox;
        public Transform swordPivot;

        [Header("Timing")]
        public float telegraph = 0.5f;
        public float thrustDuration = 0.25f;
        public float endingLag = 0.9f;

        [Header("Positions (local)")]
        public Vector3 windupPosition = new Vector3(-0.8f, 0.5f, 0f);
        public Vector3 windupRotation = Vector3.zero;
        public Vector3 thrustPosition = new Vector3(1.2f, 0.5f, 0f);
        public Vector3 thrustRotation = Vector3.zero;

        Vector3 originalPos;
        Vector3 originalRot;

        public override IEnumerator Execute(BossController boss)
        {
            if (swordPivot != null)
            {
                originalPos = swordPivot.localPosition;
                originalRot = swordPivot.localEulerAngles;
            }

            if (swordPivot != null)
                yield return MoveTo(windupPosition, windupRotation, telegraph);
            else
                yield return new WaitForSeconds(telegraph);

            if (hitbox != null) hitbox.Begin();
            if (swordPivot != null)
                yield return MoveTo(thrustPosition, thrustRotation, thrustDuration);
            else
                yield return new WaitForSeconds(thrustDuration);
            if (hitbox != null) hitbox.End();

            yield return new WaitForSeconds(endingLag);

            if (swordPivot != null)
                yield return MoveTo(originalPos, originalRot, 0.3f);
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
