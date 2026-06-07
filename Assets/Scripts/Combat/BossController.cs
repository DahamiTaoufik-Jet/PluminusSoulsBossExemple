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

        [Header("Force Attack (Anti-Idle)")]
        [Tooltip("Si > 0 et en mode ExternalInput, force une attaque aleatoire si le boss n'attaque pas pendant N secondes.")]
        public float forceAttackAfterIdle = 3f;

        [Header("Sword Reset")]
        [Tooltip("Glissez le pivot de l'epee ici pour reset sa pose entre les episodes.")]
        public Transform swordPivot;

        [Header("Events")]
        public UnityEvent OnAttackBegan;
        public UnityEvent OnAttackEnded;
        [Tooltip("Invoque quand une attaque se termine sans toucher ni etre bloquee par la cible.")]
        public UnityEvent OnAttackWhiffed;

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
        bool attackLandedFlag;
        float idleTimer;
        Vector3 savedSwordLocalPos;
        Quaternion savedSwordLocalRot;

        /// <summary>Transform racine deplace par le mouvement (parent pour eviter le desync de rotation).</summary>
        public Transform MoveTransform { get; private set; }

        void Start()
        {
            if (health == null) health = GetComponent<Health>();
            if (shield == null) shield = GetComponentInChildren<BossShield>();
            cc = GetComponent<CharacterController>();
            MoveTransform = transform.parent != null ? transform.parent : transform;
            if (swordPivot != null)
            {
                savedSwordLocalPos = swordPivot.localPosition;
                savedSwordLocalRot = swordPivot.localRotation;
            }
            SyncTargetFromLockOn();
            StartLoop();
        }

        public bool canRotate = true;

        /// <summary>
        /// Reinitialise l'etat interne du boss apres un reset d'episode.
        /// A wirer dans PluminusTrainingManager.OnReset ou Health.ResetHealth.
        /// </summary>
        public void ResetState()
        {
            StopAllCoroutines();
            Current = State.Idle;
            CurrentAttack = null;
            canRotate = true;
            isDashing = false;
            nextDashTime = 0f;
            attackLandedFlag = false;
            aiMoveDirection = Vector3.zero;
            idleTimer = 0f;
            if (swordPivot != null)
            {
                swordPivot.localPosition = savedSwordLocalPos;
                swordPivot.localRotation = savedSwordLocalRot;
            }
            SyncTargetFromLockOn();
            StartLoop();
        }

        void StartLoop()
        {
            if (controlMode == ControlMode.Auto)
                loop = StartCoroutine(AutoLoop());
            else
                loop = StartCoroutine(ManualLoop());
        }

        void Update()
        {

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

            // Force une attaque aleatoire si idle trop longtemps
            if (controlMode == ControlMode.ExternalInput && forceAttackAfterIdle > 0f && Current != State.Dead)
            {
                if (Current == State.Attacking)
                {
                    idleTimer = 0f;
                }
                else
                {
                    idleTimer += Time.deltaTime;
                    if (idleTimer >= forceAttackAfterIdle)
                    {
                        idleTimer = 0f;
                        ForceRandomAttack();
                    }
                }
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

        /// <summary>
        /// Force une attaque au hasard parmi celles a portee. Ignore l'etat actuel (sauf Dead).
        /// </summary>
        public void ForceRandomAttack()
        {
            if (Current == State.Dead) return;
            if (attacks.Count == 0) return;

            // Cherche les attaques a portee
            List<int> available = new List<int>();
            for (int i = 0; i < attacks.Count; i++)
            {
                if (attacks[i] != null && attacks[i].IsInRange(DistanceToTarget))
                    available.Add(i);
            }

            if (available.Count == 0)
            {
                // Rien a portee -> dash vers la cible
                DoDash();
                return;
            }

            int pick = available[Random.Range(0, available.Count)];
            ExecuteAttack(attacks[pick]);
        }

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
                if (stateSensor != null) { stateSensor.SetAxis(0, 0); stateSensor.SetAxis(1, 1); }
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
                    if (stateSensor != null) { stateSensor.SetAxis(0, 0); stateSensor.SetAxis(1, 1); }
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

            canRotate = true; // Securite : si l'attaque (ex: Leap) a ete interrompue
            Current = State.Guarding;
            if (shield != null) shield.Raise();
            if (stateSensor != null) { stateSensor.SetAxis(0, 0); stateSensor.SetAxis(1, 1); }

            if (controlMode == ControlMode.Auto)
                loop = StartCoroutine(AutoLoop());
            else
                loop = StartCoroutine(ManualLoop());
        }

        IEnumerator ExecuteAttackRoutine(BossAttack attack)
        {
            Current = State.Attacking;
            CurrentAttack = attack;
            canRotate = false;
            if (shield != null) shield.Lower();

            // Whiff detection : ecoute temporaire des events de la cible
            attackLandedFlag = false;
            Health targetHealth = (target != null) ? target.GetComponent<Health>() : null;
            if (targetHealth != null)
            {
                targetHealth.OnDamaged.AddListener(MarkAttackLanded);
                targetHealth.OnBlocked.AddListener(MarkAttackLanded);
            }

            // Pluminus : signale le type d'attaque (index+1) en phase Prep (2)
            int attackType = attacks.IndexOf(attack) + 1;
            if (stateSensor != null)
            {
                stateSensor.SetAxis(0, attackType);  // Axe 0 = Type Action
                stateSensor.SetAxis(1, 2);            // Axe 1 = Phase Prep
            }

            OnAttackBegan?.Invoke();

            // Pluminus : phase Active (3) au moment de l'execution
            if (stateSensor != null) stateSensor.SetAxis(1, 3);

            yield return attack.Execute(this);
            OnAttackEnded?.Invoke();
            CurrentAttack = null;

            // Nettoyage des listeners + detection whiff
            if (targetHealth != null)
            {
                targetHealth.OnDamaged.RemoveListener(MarkAttackLanded);
                targetHealth.OnBlocked.RemoveListener(MarkAttackLanded);
            }
            if (!attackLandedFlag) OnAttackWhiffed?.Invoke();

            // Pluminus : retour au repos
            if (stateSensor != null) stateSensor.ResetAll();
        }

        void MarkAttackLanded() { attackLandedFlag = true; }

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
                    MoveTransform.position += move;

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
                MoveTransform.position += move;
        }

        void LateUpdate()
        {
            // Rotation geree par LockOnTarget sur le parent (BossObject)

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
                MoveTransform.position += move;
        }

        /// <summary>Synchronise target avec LockOnTarget du parent (source unique de verite).</summary>
        void SyncTargetFromLockOn()
        {
            LockOnTarget lockOn = GetComponentInParent<LockOnTarget>();
            if (lockOn != null && lockOn.target != null)
                target = lockOn.target;
            else
                ResolveTarget();
        }

        void ResolveTarget()
        {
            if (target != null) return;
            // Recherche par tag
            var go = GameObject.FindGameObjectWithTag(targetTag);
            if (go != null) { target = go.transform; return; }
            // Fallback : cherche un Health d'equipe opposee
            var healths = FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < healths.Length; i++)
            {
                if (healths[i] == health) continue;
                if (healths[i].team == Team.Boss) continue;
                target = healths[i].transform;
                Debug.LogWarning($"[BossController] Cible trouvee par fallback Health: {target.name}. Pensez a assigner le tag '{targetTag}'.");
                return;
            }
            Debug.LogError($"[BossController] Aucune cible trouvee ! Verifiez le tag '{targetTag}' ou assignez 'target' dans l'Inspector.");
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
