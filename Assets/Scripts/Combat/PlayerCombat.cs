using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Soulsboss.Combat
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCombat : MonoBehaviour
    {
        public Health health;
        public PlayerController controller;
        public DamageHitbox swordHitbox;
        public Transform swordPivot;

        [Header("Attack")]
        public float attackCooldown = 2f;
        public float attackWindup = 0.25f;
        public float attackActiveWindow = 0.25f;
        public float attackRecovery = 0.4f;

        [Header("Sword swing (local euler angles)")]
        public Vector3 raisedRotation = new Vector3(0f, 0f, 90f);
        public Vector3 strikeRotation = new Vector3(0f, 0f, -45f);

        [Header("Dodge")]
        public float dodgeDistance = 3f;
        public float dodgeDuration = 0.35f;
        public float dodgeSpeed = 10f;
        public float iframeDuration = 0.25f;
        public float dodgeCooldown = 1f;

        public UnityEvent OnAttackStarted;
        public UnityEvent OnDodged;

        CharacterController cc;
        float nextAttackTime;
        float nextDodgeTime;
        bool dodging;
        bool attacking;
        Vector3 originalSwordRotation;

        public bool CanAttack => Time.time >= nextAttackTime && !dodging && !attacking;
        public bool IsDodging => dodging;
        public bool IsAttacking => attacking;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (controller == null) controller = GetComponent<PlayerController>();
            if (health == null) health = GetComponent<Health>();
        }

        public void RequestAttack()
        {
            if (!CanAttack) return;
            nextAttackTime = Time.time + attackCooldown;
            StartCoroutine(AttackRoutine());
        }

        public void RequestDodge(int direction)
        {
            if (dodging || attacking) return;
            if (direction == 0) return;
            if (Time.time < nextDodgeTime) return;
            nextDodgeTime = Time.time + dodgeCooldown;
            StartCoroutine(DodgeRoutine(direction > 0 ? 1 : -1));
        }

        IEnumerator AttackRoutine()
        {
            attacking = true;
            OnAttackStarted?.Invoke();
            controller.SetInputLocked(true);
            if (swordPivot != null) originalSwordRotation = swordPivot.localEulerAngles;

            // Windup: raise sword
            if (swordPivot != null)
                yield return RotateSword(raisedRotation, attackWindup);
            else
                yield return new WaitForSeconds(attackWindup);

            // Active: swing down
            if (swordHitbox != null) swordHitbox.Begin();
            if (swordPivot != null)
                yield return RotateSword(strikeRotation, attackActiveWindow);
            else
                yield return new WaitForSeconds(attackActiveWindow);
            if (swordHitbox != null) swordHitbox.End();

            // Recovery: return to rest
            if (swordPivot != null)
                yield return RotateSword(originalSwordRotation, attackRecovery);
            else
                yield return new WaitForSeconds(attackRecovery);

            controller.SetInputLocked(false);
            attacking = false;
        }

        IEnumerator RotateSword(Vector3 targetEuler, float duration)
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

        IEnumerator DodgeRoutine(int dir)
        {
            dodging = true;
            OnDodged?.Invoke();
            controller.SetInputLocked(true);
            if (health != null) health.invincible = true;

            Transform boss = controller.boss;
            float iframeEnd = Time.time + iframeDuration;
            float t = 0f;

            if (boss != null && boss.gameObject.activeInHierarchy)
            {
                // Arc de cercle autour du boss
                Vector3 fromBoss = transform.position - boss.position;
                fromBoss.y = 0f;
                float radius = fromBoss.magnitude;
                if (radius < 0.1f) radius = 1f;

                float startAngle = Mathf.Atan2(fromBoss.x, fromBoss.z);
                float arcLength = dodgeDistance;
                float angularSpeed = (dodgeSpeed / radius) * dir;

                float currentAngle = startAngle;
                float baseY = transform.position.y;

                while (t < dodgeDuration)
                {
                    currentAngle += angularSpeed * Time.deltaTime;
                    Vector3 desired = boss.position + new Vector3(
                        Mathf.Sin(currentAngle) * radius,
                        baseY - boss.position.y,
                        Mathf.Cos(currentAngle) * radius);

                    Vector3 delta = desired - transform.position;
                    cc.Move(delta);

                    if (health != null && Time.time > iframeEnd) health.invincible = false;
                    t += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                // Fallback ligne droite si pas de boss
                Vector3 dirVec = controller.StrafeRight * dir;
                float speed = dodgeDistance / Mathf.Max(0.05f, dodgeDuration);
                while (t < dodgeDuration)
                {
                    cc.Move(dirVec * speed * Time.deltaTime);
                    if (health != null && Time.time > iframeEnd) health.invincible = false;
                    t += Time.deltaTime;
                    yield return null;
                }
            }

            if (health != null) health.invincible = false;
            controller.SetInputLocked(false);
            dodging = false;
        }

        void OnAttack(InputValue v) { if (v.isPressed) RequestAttack(); }

        void OnDodge(InputValue v)
        {
            if (!v.isPressed) return;
            float x = controller != null ? controller.MoveInput.x : 0f;
            if (x > 0.3f) RequestDodge(-1);
            else if (x < -0.3f) RequestDodge(1);
        }
    }
}
