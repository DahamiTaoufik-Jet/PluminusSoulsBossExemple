using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Soulsboss.Combat
{
    public class BossController : MonoBehaviour
    {
        public Health health;
        public BossShield shield;
        public Transform target;
        public string targetTag = "Player";

        [Tooltip("Attacks the boss may use. Drag BossAttack-derived components here.")]
        public List<BossAttack> attacks = new List<BossAttack>();

        [Tooltip("Detection range. Below this the boss engages and may attack.")]
        public float detectionRange = 30f;
        [Tooltip("Idle time spent guarding between attempts to attack.")]
        public float guardDuration = 1.5f;
        [Tooltip("Random jitter added to guardDuration each cycle.")]
        public float guardJitter = 0.5f;
        public float turnSpeed = 720f;

        public UnityEvent OnAttackBegan;
        public UnityEvent OnAttackEnded;

        public enum State { Idle, Guarding, Attacking, Dead }
        public State Current { get; private set; } = State.Idle;
        public Transform Target => target;
        public BossAttack CurrentAttack { get; private set; }

        Coroutine loop;

        void Start()
        {
            if (health == null) health = GetComponent<Health>();
            if (shield == null) shield = GetComponentInChildren<BossShield>();
            ResolveTarget();
            loop = StartCoroutine(Loop());
        }

        public bool canRotate = true;

        void Update()
        {
            if (canRotate && Current != State.Attacking) FaceTarget();
            if (Current != State.Dead && health != null && !health.IsAlive)
            {
                Current = State.Dead;
                if (shield != null) shield.Lower();
                if (loop != null) StopCoroutine(loop);
                StopAllCoroutines();
            }
        }

        void ResolveTarget()
        {
            if (target != null) return;
            var go = GameObject.FindGameObjectWithTag(targetTag);
            if (go != null) target = go.transform;
        }

        void FaceTarget()
        {
            if (target == null) { ResolveTarget(); if (target == null) return; }
            Vector3 d = target.position - transform.position; d.y = 0f;
            if (d.sqrMagnitude < 0.01f) return;
            Quaternion want = Quaternion.LookRotation(d) * Quaternion.Euler(0f, -90f, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
        }

        IEnumerator Loop()
        {
            while (health == null || health.IsAlive)
            {
                Current = State.Guarding;
                if (shield != null) shield.Raise();
                float wait = guardDuration + Random.Range(-guardJitter, guardJitter);
                yield return new WaitForSeconds(Mathf.Max(0.1f, wait));

                if (target == null) { ResolveTarget(); continue; }
                float dist = Vector3.Distance(transform.position, target.position);
                if (dist > detectionRange) continue;

                BossAttack pick = PickAttack(dist);
                if (pick == null) continue;

                Current = State.Attacking;
                CurrentAttack = pick;
                if (shield != null) shield.Lower();
                OnAttackBegan?.Invoke();
                yield return pick.Execute(this);
                OnAttackEnded?.Invoke();
                CurrentAttack = null;
            }
            Current = State.Dead;
        }

        public void ForceAttack(BossAttack attack)
        {
            if (Current == State.Dead) return;
            if (loop != null) StopCoroutine(loop);
            StopAllCoroutines();
            loop = StartCoroutine(ForceAttackRoutine(attack));
        }

        public void InterruptForCounter(float duration)
        {
            if (Current == State.Dead) return;
            if (loop != null) StopCoroutine(loop);
            StopAllCoroutines();
            StartCoroutine(CounterRoutine(duration));
        }

        IEnumerator CounterRoutine(float duration)
        {
            Current = State.Attacking;
            if (shield != null) shield.Raise();
            yield return new WaitForSeconds(duration);
            Current = State.Guarding;
            loop = StartCoroutine(Loop());
        }

        IEnumerator ForceAttackRoutine(BossAttack attack)
        {
            Current = State.Attacking;
            CurrentAttack = attack;
            if (shield != null) shield.Lower();
            OnAttackBegan?.Invoke();
            yield return attack.Execute(this);
            OnAttackEnded?.Invoke();
            CurrentAttack = null;

            Current = State.Guarding;
            if (shield != null) shield.Raise();
            loop = StartCoroutine(Loop());
        }

        BossAttack PickAttack(float distance)
        {
            float total = 0f;
            for (int i = 0; i < attacks.Count; i++)
            {
                var a = attacks[i];
                if (a == null) continue;
                if (!a.IsInRange(distance)) continue;
                total += Mathf.Max(0f, a.weight);
            }
            if (total <= 0f) return null;
            float roll = Random.value * total;
            float acc = 0f;
            for (int i = 0; i < attacks.Count; i++)
            {
                var a = attacks[i];
                if (a == null) continue;
                if (!a.IsInRange(distance)) continue;
                acc += Mathf.Max(0f, a.weight);
                if (roll <= acc) return a;
            }
            return null;
        }
    }
}
