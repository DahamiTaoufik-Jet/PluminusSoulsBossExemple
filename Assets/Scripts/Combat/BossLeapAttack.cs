using System.Collections;
using UnityEngine;

namespace Soulsboss.Combat
{
    /// <summary>
    /// Boss leaps into the air toward the player, then slams down dealing AOE damage.
    /// Designed for long range — use minRange to trigger only when the player is far.
    /// </summary>
    public class BossLeapAttack : BossAttack
    {
        public DamageHitbox hitbox;
        public Transform swordPivot;

        [Header("Leap")]
        [Tooltip("Peak height of the jump arc.")]
        public float leapHeight = 5f;
        [Tooltip("Time to reach the peak.")]
        public float ascentDuration = 0.4f;
        [Tooltip("Time from peak to landing.")]
        public float descentDuration = 0.3f;
        [Tooltip("How close to the player the boss lands.")]
        public float landingOffset = 1.5f;

        [Header("Slam")]
        public float slamRadius = 3f;
        public float slamDamage = 25f;
        public float endingLag = 1.2f;

        [Header("Sword poses (local)")]
        public Vector3 raisedPosition = new Vector3(0f, 1.2f, 0f);
        public Vector3 raisedRotation = new Vector3(0f, 0f, 90f);
        public Vector3 slamPosition = new Vector3(0.8f, -0.2f, 0f);
        public Vector3 slamRotation = new Vector3(0f, 0f, -60f);

        [Header("Particles")]
        public Color impactColor = new Color(1f, 0.6f, 0.1f, 1f);

        Vector3 originalPos;
        Vector3 originalRot;
        ParticleSystem impactParticles;

        void Awake()
        {
            BuildParticles();
        }

        public override IEnumerator Execute(BossController boss)
        {
            Transform bossTransform = boss.transform;
            Transform target = boss.Target;
            if (target == null) yield break;

            // Save sword pose
            if (swordPivot != null)
            {
                originalPos = swordPivot.localPosition;
                originalRot = swordPivot.localEulerAngles;
            }

            // Disable rotation during leap
            boss.canRotate = false;
            CharacterController cc = bossTransform.GetComponent<CharacterController>();

            // Calculate landing position
            Vector3 toTarget = target.position - bossTransform.position;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            Vector3 dir = dist > 0.1f ? toTarget / dist : bossTransform.right;
            Vector3 landPos = target.position - dir * landingOffset;
            landPos.y = bossTransform.position.y;

            Vector3 startPos = bossTransform.position;

            // Raise sword during ascent
            if (swordPivot != null)
                StartCoroutine(MoveSword(raisedPosition, raisedRotation, ascentDuration));

            // Ascent — arc upward toward midpoint
            float t = 0f;
            while (t < ascentDuration)
            {
                t += Time.deltaTime;
                float ratio = Mathf.Clamp01(t / ascentDuration);
                float smooth = Mathf.SmoothStep(0f, 1f, ratio);

                Vector3 horizontal = Vector3.Lerp(startPos, landPos, smooth * 0.5f);
                float height = Mathf.Sin(smooth * Mathf.PI * 0.5f) * leapHeight;
                Vector3 pos = new Vector3(horizontal.x, startPos.y + height, horizontal.z);

                MoveToPosition(bossTransform, cc, pos);
                yield return null;
            }

            // Descent — slam down, tracking player
            Vector3 peakPos = bossTransform.position;
            float peakHeight = peakPos.y - startPos.y;

            // Swing sword down during descent
            if (swordPivot != null)
                StartCoroutine(MoveSword(slamPosition, slamRotation, descentDuration));

            t = 0f;
            while (t < descentDuration)
            {
                t += Time.deltaTime;
                float ratio = Mathf.Clamp01(t / descentDuration);
                float smooth = ratio * ratio; // accelerating fall

                // Recalculate landing each frame to track player
                Vector3 toTarget2 = target.position - peakPos;
                toTarget2.y = 0f;
                float d2 = toTarget2.magnitude;
                Vector3 dir2 = d2 > 0.1f ? toTarget2 / d2 : dir;
                landPos = target.position - dir2 * landingOffset;
                landPos.y = startPos.y;

                Vector3 horizontal = Vector3.Lerp(peakPos, landPos, smooth);
                float height = Mathf.Lerp(peakHeight, 0f, smooth);
                Vector3 pos = new Vector3(horizontal.x, startPos.y + height, horizontal.z);

                MoveToPosition(bossTransform, cc, pos);
                yield return null;
            }

            // Snap to ground
            MoveToPosition(bossTransform, cc, landPos);

            // Impact — AOE damage
            if (hitbox != null) hitbox.Begin();
            SlamAOE(bossTransform);
            if (hitbox != null) hitbox.End();

            if (impactParticles != null) impactParticles.Play();

            // Re-enable rotation
            boss.canRotate = true;

            // Ending lag
            yield return new WaitForSeconds(endingLag);

            // Return sword
            if (swordPivot != null)
                yield return MoveSword(originalPos, originalRot, 0.3f);
        }

        void SlamAOE(Transform center)
        {
            Collider[] hits = Physics.OverlapSphere(center.position, slamRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                var target = hits[i].GetComponentInParent<IDamageable>();
                if (target == null) continue;
                if (target.Team == Team.Boss) continue;
                if (!target.IsAlive) continue;

                target.TakeDamage(slamDamage, center.position);
            }
        }

        void MoveToPosition(Transform bossTransform, CharacterController cc, Vector3 targetPos)
        {
            if (cc != null)
            {
                Vector3 delta = targetPos - bossTransform.position;
                cc.Move(delta);
            }
            else
            {
                bossTransform.position = targetPos;
            }
        }

        IEnumerator MoveSword(Vector3 targetPos, Vector3 targetEuler, float duration)
        {
            if (swordPivot == null) yield break;
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

        void BuildParticles()
        {
            var go = new GameObject("LeapSlam_Impact");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            impactParticles = go.AddComponent<ParticleSystem>();
            impactParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = impactParticles.main;
            main.duration = 0.3f;
            main.loop = false;
            main.startLifetime = 0.8f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, slamRadius * 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startColor = impactColor;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = impactParticles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 50) });

            var shape = impactParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.5f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var sol = impactParticles.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = impactParticles.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material.color = impactColor;
        }
    }
}
