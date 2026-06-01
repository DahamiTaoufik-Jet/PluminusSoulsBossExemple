using System.Collections;
using UnityEngine;

namespace Soulsboss.Combat
{
    public class BossShieldCounter : MonoBehaviour
    {
        public BossController boss;
        public Health bossHealth;

        [Header("Aura")]
        public float auraRadius = 5f;
        public float auraDamage = 15f;
        public float knockbackDistance = 4f;
        public float knockbackDuration = 0.3f;

        [Header("Timing")]
        public float chargeTime = 0.4f;
        public float endingLag = 0.8f;

        [Header("Particles")]
        public Color auraColor = new Color(0.8f, 0.2f, 0.2f, 1f);
        public Color burstColor = new Color(1f, 0.5f, 0.1f, 1f);

        ParticleSystem chargeParticles;
        ParticleSystem burstParticles;
        bool countering;

        void Awake()
        {
            if (boss == null) boss = GetComponent<BossController>();
            if (bossHealth == null) bossHealth = GetComponent<Health>();
            BuildParticles();
        }

        void OnEnable()
        {
            if (bossHealth != null) bossHealth.OnBlocked.AddListener(OnShieldHit);
        }

        void OnDisable()
        {
            if (bossHealth != null) bossHealth.OnBlocked.RemoveListener(OnShieldHit);
        }

        void OnShieldHit()
        {
            if (countering) return;
            if (boss != null && boss.Current == BossController.State.Attacking) return;
            StartCoroutine(CounterRoutine());
        }

        IEnumerator CounterRoutine()
        {
            countering = true;

            if (boss != null) boss.InterruptForCounter(chargeTime + endingLag + 0.1f);

            if (chargeParticles != null) chargeParticles.Play();
            yield return new WaitForSeconds(chargeTime);

            if (burstParticles != null) burstParticles.Play();
            HitAllInRadius();

            yield return new WaitForSeconds(endingLag);
            countering = false;
        }

        void HitAllInRadius()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, auraRadius);
            for (int i = 0; i < hits.Length; i++)
            {
                var target = hits[i].GetComponentInParent<IDamageable>();
                if (target == null) continue;
                if (target.Team == Team.Boss) continue;
                if (!target.IsAlive) continue;

                target.TakeDamageUnblockable(auraDamage, transform.position);

                var cc = hits[i].GetComponentInParent<CharacterController>();
                if (cc != null) StartCoroutine(Knockback(cc));
            }
        }

        IEnumerator Knockback(CharacterController cc)
        {
            Vector3 dir = cc.transform.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = -transform.forward;
            dir.Normalize();

            float speed = knockbackDistance / Mathf.Max(0.05f, knockbackDuration);
            float t = 0f;
            while (t < knockbackDuration)
            {
                cc.Move(dir * speed * Time.deltaTime);
                t += Time.deltaTime;
                yield return null;
            }
        }

        void BuildParticles()
        {
            chargeParticles = CreateSystem("ShieldCounter_Charge");
            var cm = chargeParticles.main;
            cm.duration = chargeTime;
            cm.loop = false;
            cm.startLifetime = chargeTime * 0.8f;
            cm.startSpeed = 0f;
            cm.startSize = 0.15f;
            cm.startColor = auraColor;
            cm.maxParticles = 60;
            cm.simulationSpace = ParticleSystemSimulationSpace.World;

            var ce = chargeParticles.emission;
            ce.rateOverTime = 0f;
            ce.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 40) });

            var cs = chargeParticles.shape;
            cs.shapeType = ParticleSystemShapeType.Sphere;
            cs.radius = auraRadius * 0.8f;

            var cv = chargeParticles.velocityOverLifetime;
            cv.enabled = true;
            cv.radial = -auraRadius;

            var cr = chargeParticles.GetComponent<ParticleSystemRenderer>();
            cr.material = new Material(Shader.Find("Particles/Standard Unlit"));
            cr.material.color = auraColor;

            burstParticles = CreateSystem("ShieldCounter_Burst");
            var bm = burstParticles.main;
            bm.duration = 0.3f;
            bm.loop = false;
            bm.startLifetime = 0.6f;
            bm.startSpeed = auraRadius * 3f;
            bm.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            bm.startColor = burstColor;
            bm.maxParticles = 80;
            bm.simulationSpace = ParticleSystemSimulationSpace.World;

            var be = burstParticles.emission;
            be.rateOverTime = 0f;
            be.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 60) });

            var bs = burstParticles.shape;
            bs.shapeType = ParticleSystemShapeType.Sphere;
            bs.radius = 0.3f;

            var bsl = burstParticles.sizeOverLifetime;
            bsl.enabled = true;
            bsl.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var brr = burstParticles.GetComponent<ParticleSystemRenderer>();
            brr.material = new Material(Shader.Find("Particles/Standard Unlit"));
            brr.material.color = burstColor;
        }

        ParticleSystem CreateSystem(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.8f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }
    }
}
