using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Soulsboss.Combat
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public enum ControlMode { Manual, AI }

        [Header("Control")]
        [Tooltip("Manual = input clavier/manette. AI = pilote par Pluminus ActionRouter.")]
        public ControlMode controlMode = ControlMode.Manual;

        [Header("References")]
        [Tooltip("Laisse vide = auto-find par tag 'Boss'.")]
        public Transform boss;

        [Header("Movement")]
        public float moveSpeed = 5f;
        public float rotationSpeed = 1440f;
        public float gravity = -20f;

        [Header("Dash")]
        [Tooltip("Distance parcourue par le dash.")]
        public float dashDistance = 3f;
        [Tooltip("Duree du dash en secondes.")]
        public float dashDuration = 0.25f;
        [Tooltip("Cooldown entre deux dashs.")]
        public float dashCooldown = 0.5f;

        [Header("Engagement Distance")]
        [Tooltip("Distance max avant de recevoir une penalite de fuite.")]
        public float maxEngagementDistance = 8f;
        [Tooltip("Intervalle entre chaque penalite (secondes).")]
        public float penaltyInterval = 0.5f;
        [Tooltip("Invoque periodiquement quand le joueur est trop loin du boss.")]
        public UnityEvent OnTooFar;

        [Header("Ground check")]
        public float groundCheckRadius = 0.3f;
        public float groundCheckOffset = 0.1f;
        public LayerMask groundMask = ~0;

        CharacterController cc;
        Vector2 moveInput;
        Vector2 aiMoveInput;
        float fallSpeed;
        bool inputLocked;
        bool grounded;
        bool isDashing;
        float nextDashTime;
        float penaltyTimer;

        // Directions calculees chaque frame (joueur -> boss)
        Vector3 toBossDir;
        Vector3 strafeDir;

        public Vector2 MoveInput => controlMode == ControlMode.AI ? aiMoveInput : moveInput;
        public bool IsInputLocked => inputLocked;
        public bool IsGrounded => grounded;
        // Expose pour le dodge dans PlayerCombat
        public Vector3 StrafeRight => strafeDir;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
        }

        /// <summary>
        /// Reinitialise l'etat interne du joueur apres un reset d'episode.
        /// A wirer dans PluminusTrainingManager.OnReset.
        /// </summary>
        public void ResetState()
        {
            StopAllCoroutines();
            inputLocked = false;
            isDashing = false;
            nextDashTime = 0f;
            fallSpeed = 0f;
            moveInput = Vector2.zero;
            aiMoveInput = Vector2.zero;
            penaltyTimer = 0f;
        }

        void Start()
        {
            if (boss == null)
            {
                GameObject go = GameObject.FindGameObjectWithTag("Boss");
                if (go != null) boss = go.transform;
            }
        }

        void Update()
        {
            ComputeDirections();
            GroundCheck();
            ApplyMovement();
            ApplyRotation();
            CheckEngagementDistance();
        }

        void ComputeDirections()
        {
            if (boss != null && boss.gameObject.activeInHierarchy)
            {
                Vector3 raw = boss.position - transform.position;
                raw.y = 0f;
                float mag = raw.magnitude;
                if (mag > 0.001f)
                {
                    toBossDir = raw / mag;
                    strafeDir = new Vector3(toBossDir.z, 0f, -toBossDir.x);
                    return;
                }
            }
            // Fallback : X+ = forward du modele
            toBossDir = transform.right;
            strafeDir = -transform.forward;
        }

        void CheckEngagementDistance()
        {
            if (boss == null || !boss.gameObject.activeInHierarchy) return;
            if (controlMode != ControlMode.AI) return;

            Vector3 diff = boss.position - transform.position;
            diff.y = 0f;
            if (diff.magnitude <= maxEngagementDistance) return;

            penaltyTimer += Time.deltaTime;
            if (penaltyTimer >= penaltyInterval)
            {
                penaltyTimer = 0f;
                OnTooFar?.Invoke();
            }
        }

        void GroundCheck()
        {
            Vector3 origin = transform.position + Vector3.up * groundCheckOffset;
            grounded = Physics.CheckSphere(origin, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

            if (grounded && fallSpeed < 0f) fallSpeed = -2f;
            else fallSpeed += gravity * Time.deltaTime;
        }

        void ApplyMovement()
        {
            Vector2 currentInput = (controlMode == ControlMode.AI) ? aiMoveInput : moveInput;
            Vector3 wish = inputLocked ? Vector3.zero : (toBossDir * currentInput.y + strafeDir * currentInput.x);
            if (wish.sqrMagnitude > 1f) wish.Normalize();

            Vector3 move = wish * moveSpeed;
            move.y = fallSpeed;

            cc.Move(move * Time.deltaTime);
        }

        void ApplyRotation()
        {
            if (boss == null || !boss.gameObject.activeInHierarchy) return;

            Vector3 raw = boss.position - transform.position;
            raw.y = 0f;
            if (raw.sqrMagnitude < 0.001f) return;

            // LookRotation pointe Z+ vers la cible
            // On ajoute -90 en Y pour que X+ pointe vers la cible
            Quaternion desired = Quaternion.LookRotation(raw.normalized) * Quaternion.Euler(0f, 90f, 0f);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, desired, rotationSpeed * Time.deltaTime);
        }

        public void SetInputLocked(bool locked) => inputLocked = locked;

        // ──────────────────────────────────────
        //  Actions publiques pour ActionRouter
        // ──────────────────────────────────────

        /// <summary>Action 0 : Ne rien faire (Idle).</summary>
        public void DoIdle() { }

        /// <summary>Action 1 : Dash vers le boss.</summary>
        public void DoDashForward() { TryDash(toBossDir); }

        /// <summary>Action 2 : Attaquer. Delegue a PlayerCombat.</summary>
        public void DoAttack()
        {
            PlayerCombat combat = GetComponent<PlayerCombat>();
            if (combat != null) combat.RequestAttack();
        }

        /// <summary>Action 3 : Esquiver a gauche.</summary>
        public void DoDodgeLeft()
        {
            PlayerCombat combat = GetComponent<PlayerCombat>();
            if (combat != null) combat.RequestDodge(1);
        }

        /// <summary>Action 4 : Esquiver a droite.</summary>
        public void DoDodgeRight()
        {
            PlayerCombat combat = GetComponent<PlayerCombat>();
            if (combat != null) combat.RequestDodge(-1);
        }

        // ──────────────────────────────────────
        //  Dash
        // ──────────────────────────────────────

        private void TryDash(Vector3 direction)
        {
            if (isDashing || inputLocked) return;
            if (Time.time < nextDashTime) return;
            if (direction.sqrMagnitude < 0.001f) return;
            StartCoroutine(DashRoutine(direction.normalized));
        }

        System.Collections.IEnumerator DashRoutine(Vector3 dir)
        {
            isDashing = true;
            inputLocked = true;
            nextDashTime = Time.time + dashCooldown;

            float speed = dashDistance / dashDuration;
            float t = 0f;

            while (t < dashDuration)
            {
                Vector3 move = dir * speed * Time.deltaTime;
                move.y = fallSpeed * Time.deltaTime;
                cc.Move(move);
                t += Time.deltaTime;
                yield return null;
            }

            isDashing = false;
            inputLocked = false;
        }

        // ──────────────────────────────────────
        //  Input manuel (inchange)
        // ──────────────────────────────────────

        void OnMove(InputValue v)
        {
            if (controlMode == ControlMode.Manual) moveInput = v.Get<Vector2>();
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = grounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * groundCheckOffset, groundCheckRadius);

            if (boss == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up, toBossDir * 2f);
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position + Vector3.up, strafeDir * 2f);
        }
#endif
    }
}
