using UnityEngine;

namespace Soulsboss.Combat
{
    /// <summary>
    /// Force l'objet a toujours regarder la cible.
    /// A placer sur le GameObject racine (ex: BossObject) pour que
    /// toute la hierarchie enfant suive la rotation.
    /// Respecte BossController.canRotate si present dans les enfants.
    /// </summary>
    public class LockOnTarget : MonoBehaviour
    {
        [Tooltip("Glissez le GameObject du joueur ici.")]
        public Transform target;

        [Tooltip("Vitesse de rotation (degres/sec). 0 = snap instantane.")]
        public float rotationSpeed = 720f;

        [Tooltip("Offset Y pour aligner l'axe visuel du modele. -90 si le forward du modele est X+.")]
        public float yAxisOffset = -90f;

        BossController bossController;

        void Awake()
        {
            bossController = GetComponentInChildren<BossController>();
        }

        void LateUpdate()
        {
            if (bossController != null && !bossController.canRotate) return;
            if (target == null) return;
            if (!target.gameObject.activeInHierarchy) return;

            Vector3 dir = target.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) return;

            Quaternion want = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, yAxisOffset, 0f);

            if (rotationSpeed <= 0f)
                transform.rotation = want;
            else
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, want, rotationSpeed * Time.deltaTime);
        }
    }
}
