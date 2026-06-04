using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Pluminus.Sensors.Extended;

namespace Soulsboss.Combat
{
    public class BossController : MonoBehaviour
    {
        public enum ControlMode
        {
            [Tooltip("Le boss utilise sa boucle interne (guard/attack aleatoire).")]
            Auto,

            [Tooltip("Le boss attend des inputs externes (Pluminus IA ou joueur humain).")]
            ExternalInput
        }

        public Health health;
        public BossShield shield;
        public Transform target;
        public string targetTag = "Player";

        [Header("Pluminus (Optionnel)")]
        [Tooltip("Glissez le PluminusArrayStateSensor ici pour exposer l'etat du boss a l'IA adverse.")]
        public PluminusArrayStateSensor stateSensor;

        [Header("Control")]
        [Tooltip("Auto = IA interne (boucle guard/attack). ExternalInput = attente d'input externe (Pluminus IA ou joueur).")]
        public ControlMode controlMode = ControlMode.Auto;

        [Tooltip("Attacks the boss may use. Drag BossAttack-derived components here.")]
        public List<BossAttack> attacks = new List<BossAttack>();

        [Tooltip("Detection range. Below this the boss engages and may attack.")]
        public float detectionRange = 30f;
        [Tooltip("Idle time spent guarding between attempts to attack.")]
        public float guardDuration = 1.5f;
        [Tooltip("Random jitter added to guardDuration each cycle.")]
        public float guardJitter = 0.5f;
        public float turnSpeed = 720f;

        [Header("Movement")]
        [Tooltip("Move speed toward the player.")]
        public float moveSpeed = 3f;
        [Tooltip("Distance at which the boss stops approaching.")]
        public float preferredDistance = 2.5f;
        [Tooltip("Boss won't move if already within this range.")]
        public float stopDistance = 2f;

        [Header("Dash")]
        [Tooltip("Distance parcourue par le dash.")]
        public float dashDistance = 4f;
        [Tooltip("Duree du dash en secondes.")]
        public float dashDuration = 0.25f;
        [Tooltip("Cooldown entre deux dashs.")]
        public float dashCooldown = 0.5f;

        [Header("Events")]
        public UnityEvent OnAttackBegan;
        public UnityEvent OnAttackEnded;

        public enum State { Idle, Guarding, Attacking, Dead }
        public State Current { get; private set; } = State.Idle;
        public Transform Target => target;
        public BossAttack CurrentAttack { get; private set; }

        /// <summary>Distance actuelle vers la cible. Mise a jour chaque frame.</summary>
        public float DistanceToTarget { get; private set; }

        /// <summary>Nombre d'attaques dans la liste.</summary>
        public int AttackCount => attacks.Count;

        Coroutine loop;
        CharacterController cc;
        Vector3 aiMoveDirection;
        bool isDashing;
        float nextDashTime;

        void Start()
        {
            if (health == null) health = GetComponent<Health>();
            if (shield == null) shield = GetComponentInChildren<BossShield>();
            cc = GetComponent<CharacterController>();
            ResolveTarget();

            if (controlMode == ControlMode.Auto)
                loop = StartCoroutine(AutoLoop());
            else
                loop = StartCoroutine(ManualLoop());
        }

        public bool canRotate = true;

        void Update()
        {
            if (canRotate) FaceTarget();

            if (Current == State.Guarding || Current == State.Idle)
            {
                if (controlMode == ControlMode.Auto)
                    MoveTowardTarget();
                else
                    ApplyAIMove();
            }

            if (target != null)
            {
                Vector3 diff = target.position - transform.position;
                diff.y = 0f;
                DistanceToTarget = diff.magnitude;
            }

            if (Current != State.Dead && health != null && !health.IsAlive)
            {
                Current = State.Dead;
                if (shield != null) shield.Lower();
                if (loop != null) StopCoroutine(loop);
                StopAllCoroutines();
            }
        }

        // ──────────────────────────────────────
        //  Methodes publiques appelables par event
        // ──────────────────────────────────────

        /// <summary>
        /// Lance l'attaque a l'index donne. Void pour etre visible dans les UnityEvent.
        /// Dans l'Inspector, utiliser le champ "Static Parameters" pour choisir l'index.
        /// </summary>
        public void DoAttack(int index) => TryAttack(index);

        public void DoAttack0() => TryAttack(0);
        public void DoAttack1() => TryAttack(1);
        public void DoAttack2() => TryAttack(2);
        public void DoAttack3() => TryAttack(3);
        public void DoAttack4() => TryAttack(4);

        /// <summary>Action : Ne rien faire.</summary>
        public void DoIdle() { }

        /// <summary>Action : Dash droit vers la cible.</summary>
        public void DoDash()
        {
            if (target == null) return;
            if (Current == State.Attacking || Current == State.Dead) return;
            if (isDashing) return;
            if (Time.time < nextDashTime) return;
            StartCoroutine(DashRoutine());
        }

        // ──────────────────────────────────────
        //  API publique pour agent / joueur
        // ──────────────────────────────────────

        /// <summary>
        /// Tente de lancer l'attaque a l'index donne.
        /// Retourne false si : index invalide, hors portee, boss occupe ou mort.
        /// </summary>
        public bool TryAttack(int attackIndex)
        {
            if (Current == State.Dead || Current == State.Attacking) return false;
            if (attackIndex < 0 || attackIndex >= attacks.Count) return false;

            BossAttack attack = attacks[attackIndex];
            if (attack == null) return false;
            if (!attack.IsInRange(DistanceToTarget)) return false;

            ExecuteAttack(attack);
            return true;
        }

        /// <summary>
        /// Retourne true si l'attaque a cet index est utilisable maintenant.
        /// </summary>
        public bool IsAttackAvailable(int attackIndex)
        {
            if (Current == State.Dead || Current == State.Attacking) return false;
            if (attackIndex < 0 || attackIndex >= attacks.Count) return false;
            BossAttack attack = attacks[attackIndex];
            if (attack == null) return false;
            return attack.IsInRange(DistanceToTarget);
        }

        /// <summary>
        /// Remplit le buffer avec true/false pour chaque attaque (disponible ou non).
        /// </summary>
        public void GetAvailableAttacks(bool[] buffer)
        {
            for (int i = 0; i < attacks.Count; i++)
            {
                if (Current == State.Dead || Current == State.Attacking)
                {
                    buffer[i] = false;
                    continue;
                }
                BossAttack a = attacks[i];
                buffer[i] = a != null && a.IsInRange(DistanceToTarget);
            }
        }

        // ──────────────────────────────────────
        //  Boucles internes
        // ──────────────────────────────────────

        IEnumerator AutoLoop()
        {
            while (health == null || health.IsAlive)
            {
                Current = State.Guarding;
                if (shield != null) shield.Raise();
                float wait = guardDuration + Random.Range(-guardJitter, guardJitter);
                yield return new WaitForSeconds(Mathf.Max(0.1f, wait));

                if (target == null) { ResolveTarget(); continue; }
                if (DistanceToTarget > detectionRange) continue;

                BossAttack pick = PickAttack(DistanceToTarget);
                if (pick == null) continue;

                yield return ExecuteAttackRoutine(pick);
            }
            Current = State.Dead;
        }

        IEnumerator ManualLoop()
        {
            while (health == null || health.IsAlive)
            {
                if (Current != State.Attacking)
                {
                    Current = State.Guarding;
                    if (shield != null) shield.Raise();
                }
                yield return null;
            }
            Current = State.Dead;
        }

        // ──────────────────────────────────────
        //  Execution d'attaque
        // ──────────────────────────────────────

        void ExecuteAttack(BossAttack attack)
        {
            if (loop != null) StopCoroutine(loop);
            StopAllCoroutines();
            loop = StartCoroutine(ExecuteThenResume(attack));
        }

        IEnumerator ExecuteThenResume(BossAttack attack)
        {
            yield return ExecuteAttackRoutine(attack);

            Current = State.Guarding;
            if (shield != null) shield.Raise();
            if (stateSensor != null) stateSensor.ResetAll();

            if (controlMode == ControlMode.Auto)
                loop = StartCoroutine(AutoLoop());
            else
                loop = StartCoroutine(ManualLoop());
        }

        IEnumerator ExecuteAttackRoutine(BossAttack attack)
        {
            Current = State.Attacking;
            CurrentAttack = attack;
            if (shield != null) shield.Lower();

            // Pluminus : signale le type d'attaque (index+1) en phase Prep (1)
            int attackType = attacks.IndexOf(attack) + 1;
            if (stateSensor != null)
            {
                stateSensor.SetAxis(0, attackType);  // Axe 0 = Type Action
                stateSensor.SetAxis(1, 1);            // Axe 1 = Phase Prep
            }

            OnAttackBegan?.Invoke();

            // Pluminus : phase Active (2) au moment de l'execution
            if (stateSensor != null) stateSensor.SetAxis(1, 2);

            yield return attack.Execute(this);
            OnAttackEnded?.Invoke();
            CurrentAttack = null;

            // Pluminus : retour au repos
            if (stateSensor != null) stateSensor.ResetAll();
        }

        // ──────────────────────────────────────
        //  Legacy / debug
        // ──────────────────────────────────────

        /// <summary>Force une attaque sans verifier la portee. Pour debug uniquement.</summary>
        public void ForceAttack(BossAttack attack)
        {
            if (Current == State.Dead) return;
            ExecuteAttack(attack);
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

            if (controlMode == ControlMode.Auto)
                loop = StartCoroutine(AutoLoop());
            else
                loop = StartCoroutine(ManualLoop());
        }

        // ──────────────────────────────────────
        //  Utilitaires prives
        // ──────────────────────────────────────

        IEnumerator DashRoutine()
        {
            isDashing = true;
            nextDashTime = Time.time + dashCooldown;

            Vector3 dir = (target.position - transform.position);
            dir.y = 0f;
            dir.Normalize();

            float speed = dashDistance / dashDuration;
            float t = 0f;

            while (t < dashDuration)
            {
                Vector3 move = dir * speed * Time.deltaTime;
                if (cc != null)
                    cc.Move(move + Vector3.down * 9.81f * Time.deltaTime);
                else
                    transform.position += move;

                t += Time.deltaTime;
                yield return null;
            }

            isDashing = false;
        }

        void ApplyAIMove()
        {
            if (aiMoveDirection.sqrMagnitude < 0.001f) return;

            Vector3 move = aiMoveDirection * moveSpeed * Time.deltaTime;

            if (cc != null)
                cc.Move(move + Vector3.down * 9.81f * Time.deltaTime);
            else
                transform.position += move;
        }

        void LateUpdate()
        {
            if (controlMode == ControlMode.ExternalInput)
                aiMoveDirection = Vector3.zero;
        }

        void MoveTowardTarget()
        {
            if (target == null) return;
            Vector3 diff = target.position - transform.position;
            diff.y = 0f;
            float dist = diff.magnitude;
            if (dist <= stopDistance) return;

            Vector3 dir = diff / dist;
            Vector3 move = dir * moveSpeed * Time.deltaTime;

            if (cc != null)
                cc.Move(move + Vector3.down * 9.81f * Time.deltaTime);
            else
                transform.position += move;
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
