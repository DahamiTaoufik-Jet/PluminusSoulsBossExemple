using System.Collections;
using UnityEngine;

namespace Soulsboss.Combat
{
    public class BossDiagonalSlash : BossAttack
    {
        public DamageHitbox hitbox;
        public Transform swordPivot;

        [Header("Timing")]
        public float telegraph = 0.5f;
        public float slashDuration = 0.18f;
        public float endingLag = 1.0f;

        [Header("Angles")]
        public Vector3 windupRotation = new Vector3(0f, 0f, 60f);
        public Vector3 endRotation = new Vector3(0f, 0f, -50f);

        Vector3 originalRotation;

        public override IEnumerator Execute(BossController boss)
        {
            if (swordPivot != null) originalRotation = swordPivot.localEulerAngles;

            if (swordPivot != null)
                yield return RotateTo(windupRotation, telegraph);
            else
                yield return new WaitForSeconds(telegraph);

            boss.IsInActiveStrike = true;
            if (hitbox != null) hitbox.Begin();
            if (swordPivot != null)
                yield return RotateTo(endRotation, slashDuration);
            else
                yield return new WaitForSeconds(slashDuration);
            if (hitbox != null) hitbox.End();
            boss.IsInActiveStrike = false;

            yield return new WaitForSeconds(endingLag);

            if (swordPivot != null)
                yield return RotateTo(originalRotation, 0.3f);
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
