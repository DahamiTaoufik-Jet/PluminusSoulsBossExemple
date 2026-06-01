using UnityEngine;
using UnityEngine.InputSystem;

namespace Soulsboss.Combat
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Laisse vide = auto-find par tag 'Boss'.")]
        public Transform boss;

        [Header("Movement")]
        public float moveSpeed = 5f;
        public float rotationSpeed = 1440f;
        public float gravity = -20f;

        [Header("Ground check")]
        public float groundCheckRadius = 0.3f;
        public float groundCheckOffset = 0.1f;
        public LayerMask groundMask = ~0;

        CharacterController cc;
        Vector2 moveInput;
        float fallSpeed;
        bool inputLocked;
        bool grounded;

        // Directions calculees chaque frame (joueur -> boss)
        Vector3 toBossDir;
        Vector3 strafeDir;

        public Vector2 MoveInput => moveInput;
        public bool IsInputLocked => inputLocked;
        public bool IsGrounded => grounded;
        // Expose pour le dodge dans PlayerCombat
        public Vector3 StrafeRight => strafeDir;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
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

        void GroundCheck()
        {
            Vector3 origin = transform.position + Vector3.up * groundCheckOffset;
            grounded = Physics.CheckSphere(origin, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

            if (grounded && fallSpeed < 0f) fallSpeed = -2f;
            else fallSpeed += gravity * Time.deltaTime;
        }

        void ApplyMovement()
        {
            Vector3 wish = inputLocked ? Vector3.zero : (toBossDir * moveInput.y + strafeDir * moveInput.x);
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

        void OnMove(InputValue v) => moveInput = v.Get<Vector2>();

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
