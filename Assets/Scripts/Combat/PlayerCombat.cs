using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using TMPro;

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

        [Header("Counter Window")]
        [Tooltip("Duree en secondes apres une esquive reussie pendant laquelle une attaque compte comme un counter-hit.")]
        public float counterWindowDuration = 1.5f;

        [Header("UI (Optionnel)")]
        [Tooltip("Text UI qui affiche l'etat du dodge cooldown.")]
        public TextMeshProUGUI dodgeStatusText;

        [Header("Events")]
        public UnityEvent OnAttackStarted;
        [Tooltip("Invoque quand l'attaque du joueur se termine sans toucher personne.")]
        public UnityEvent OnAttackWhiffed;
        public UnityEvent OnDodged;
        [Tooltip("Invoque quand le joueur esquive alors que le boss n'etait pas en train d'attaquer.")]
        public UnityEvent OnDodgeEmpty;
        [Tooltip("Invoque quand le joueur essaye d'esquiver alors que le cooldown n'est pas pret.")]
        public UnityEvent OnDodgeOnCooldown;
        [Tooltip("Invoque quand le joueur touche le boss dans la fenetre de contre (apres une esquive reussie).")]
        public UnityEvent OnCounterHit;
        [Tooltip("Invoque quand le joueur touche le boss SANS avoir esquive avant (attaque brute).")]
        public UnityEvent OnRawHit;

        CharacterController cc;
        float nextAttackTime;
        float nextDodgeTime;
        bool dodging;
        bool attacking;
        float counterWindowEnd;
        float nextPerfectDodgeTime;
        Vector3 originalSwordRotation;
        ParticleSystem dodgeParticles;
        Vector3 savedSwordLocalPos;
        Quaternion savedSwordLocalRot;

        public bool CanAttack => Time.time >= nextAttackTime && !dodging && !attacking;
        public bool IsDodging => dodging;
        public bool IsAttacking => attacking;

        /// <summary>Ratio 0-1 du cooldown dodge restant. 0 = pret, 1 = vient juste d'etre utilise.</summary>
        public float DodgeCooldownRatio => dodgeCooldown > 0f
            ? Mathf.Clamp01((nextDodgeTime - Time.time) / dodgeCooldown)
            : 0f;

        /// <summary>Ratio 0-1 du cooldown attaque restant. 0 = pret, 1 = vient juste d'etre utilise.</summary>
        public float AttackCooldownRatio => attackCooldown > 0f
            ? Mathf.Clamp01((nextAttackTime - Time.time) / attackCooldown)
            : 0f;

        void Update()
        {
            if (dodgeStatusText != null)
            {
                bool ready = Time.time >= nextDodgeTime && !dodging;
                dodgeStatusText.text = ready ? "DODGE READY" : "COOLDOWN";
                dodgeStatusText.color = ready ? Color.green : Color.red;
            }
        }

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (controller == null) controller = GetComponent<PlayerController>();
            if (health == null) health = GetComponent<Health>();
            if (swordPivot != null)
            {
                savedSwordLocalPos = swordPivot.localPosition;
                savedSwordLocalRot = swordPivot.localRotation;
            }
            BuildDodgeParticles();
        }

        /// <summary>
        /// Reinitialise l'etat de combat apres un reset d'episode.
        /// A wirer dans PluminusTrainingManager.OnReset.
        /// </summary>
        public void ResetState()
        {
            StopAllCoroutines();
            attacking = false;
            dodging = false;
            nextAttackTime = 0f;
            nextDodgeTime = 0f;
            counterWindowEnd = 0f;
            if (health != null) health.invincible = false;
            if (controller != null) controller.SetInputLocked(false);
            if (swordPivot != null)
            {
                swordPivot.localPosition = savedSwordLocalPos;
                swordPivot.localRotation = savedSwordLocalRot;
            }
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
            if (Time.time < nextDodgeTime)
            {
                OnDodgeOnCooldown?.Invoke();
                return;
            }
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
            if (swordHitbox != null)
            {
                swordHitbox.End();
                if (swordHitbox.HitCount == 0)
                {
                    OnAttackWhiffed?.Invoke();
                }
                else
                {
                    // L'attaque a touche : counter-hit ou attaque brute ?
                    if (Time.time <= counterWindowEnd)
                        OnCounterHit?.Invoke();
                    else
                        OnRawHit?.Invoke();
                }
            }

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
            controller.SetInputLocked(true);
            if (health != null) health.invincible = true;
            if (dodgeParticles != null) dodgeParticles.Play();

            Transform boss = controller.boss;
            float iframeEnd = Time.time + iframeDuration;
            float t = 0f;

            // Whiff dodge : on traque si le boss etait en frappe active (hitbox) pendant le dodge
            bool bossWasStriking = false;
            BossController bossCtrl = (boss != null) ? boss.GetComponent<BossController>() : null;
            if (bossCtrl == null && boss != null) bossCtrl = boss.GetComponentInParent<BossController>();

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
                    if (bossCtrl != null && bossCtrl.IsInActiveStrike)
                        bossWasStriking = true;

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

            // Dodge dans le vide : le boss n'etait pas en frappe active
            if (!bossWasStriking)
            {
                OnDodgeEmpty?.Invoke();
                // Cooldown normal : tu as esquive dans le vide, assume le cooldown
            }
            else if (Time.time >= nextPerfectDodgeTime)
            {
                // Esquive parfaite : ouvre la fenetre de contre + reset cooldown attaque
                OnDodged?.Invoke();
                nextPerfectDodgeTime = Time.time + 1.4f;
                nextDodgeTime = Time.time + 1.4f;
                counterWindowEnd = Time.time + counterWindowDuration;
                nextAttackTime = 0f;
                SpawnFloatingText("PERFECT DODGE!", Color.cyan);
            }
        }

        /// <summary>A wirer sur Health.OnDamaged du joueur.</summary>
        public void OnPlayerTookDamage() => SpawnFloatingText("HIT", Color.red);

        /// <summary>A wirer sur Health.OnDamaged du boss.</summary>
        public void OnBossTookDamage() => SpawnFloatingText("HIT", Color.green);

        /// <summary>A wirer sur Health.OnBlocked du boss.</summary>
        public void OnBossBlocked() => SpawnFloatingText("BLOCKED", new Color(1f, 0.3f, 0.3f));

        void SpawnFloatingText(string text, Color color)
        {
            var go = new GameObject("FloatingText");
            go.transform.position = transform.position + Vector3.up * 2.5f;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.color = color;
            tmp.fontSize = 8f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.sortingOrder = 100;
            StartCoroutine(FloatAndFade(go, 1.2f));
        }

        IEnumerator FloatAndFade(GameObject go, float duration)
        {
            var tmp = go.GetComponent<TextMeshPro>();
            Vector3 start = go.transform.position;
            Color startColor = tmp.color;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float ratio = t / duration;
                go.transform.position = start + Vector3.up * ratio * 1.5f;
                tmp.color = new Color(startColor.r, startColor.g, startColor.b, 1f - ratio);
                if (Camera.main != null)
                    go.transform.forward = Camera.main.transform.forward;
                yield return null;
            }
            Destroy(go);
        }

        void BuildDodgeParticles()
        {
            var go = new GameObject("Dodge_VFX");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            dodgeParticles = go.AddComponent<ParticleSystem>();
            dodgeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = dodgeParticles.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = 0.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startColor = new Color(0.6f, 0.85f, 1f, 0.8f);
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = dodgeParticles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20) });

            var shape = dodgeParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            var sol = dodgeParticles.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var renderer = dodgeParticles.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material.color = new Color(0.6f, 0.85f, 1f, 0.8f);
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
