using UnityEngine;

namespace Soulsboss.Combat
{
    public class CameraController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Laisse vide = auto-find par tag 'Player'.")]
        public Transform player;
        [Tooltip("Laisse vide = auto-find par tag 'Boss'.")]
        public Transform boss;

        [Header("Position")]
        public float distance = 7f;
        public float height = 3.5f;
        public float positionDamping = 6f;

        [Header("Look")]
        public float rotationDamping = 8f;
        [Range(0f, 1f)]
        [Tooltip("0 = regarde le joueur, 1 = regarde le boss")]
        public float lookBias = 0.5f;
        public float lookHeightOffset = 1f;

        [Header("Side offset")]
        [Tooltip("Decale la camera sur le cote pour mieux voir les deux combattants.")]
        public float sideOffset = 1.5f;

        void Start()
        {
            if (player == null)
            {
                GameObject go = GameObject.FindGameObjectWithTag("Player");
                if (go != null) player = go.transform;
            }
            if (boss == null)
            {
                GameObject go = GameObject.FindGameObjectWithTag("Boss");
                if (go != null) boss = go.transform;
            }

            // Snap immediat a la position correcte
            if (player != null) SnapToDesired();
        }

        void LateUpdate()
        {
            if (player == null) return;

            Vector3 desiredPos;
            Quaternion desiredRot;

            if (boss != null && boss.gameObject.activeInHierarchy)
            {
                ComputeArenaCamera(out desiredPos, out desiredRot);
            }
            else
            {
                ComputeFollowCamera(out desiredPos, out desiredRot);
            }

            transform.position = Vector3.Lerp(transform.position, desiredPos, positionDamping * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotationDamping * Time.deltaTime);
        }

        void ComputeArenaCamera(out Vector3 pos, out Quaternion rot)
        {
            Vector3 midpoint = Vector3.Lerp(player.position, boss.position, 0.5f);

            // Axe player->boss
            Vector3 axis = player.position - boss.position;
            axis.y = 0f;
            float axisMag = axis.magnitude;
            if (axisMag < 0.01f) axis = Vector3.forward;
            else axis /= axisMag;

            // Perpendiculaire (cote)
            Vector3 side = new Vector3(axis.z, 0f, -axis.x);

            // Camera : derriere le joueur, decalee sur le cote, en hauteur
            pos = player.position + axis * distance + side * sideOffset + Vector3.up * height;

            // Look at : entre joueur et boss
            Vector3 lookTarget = Vector3.Lerp(
                player.position + Vector3.up * lookHeightOffset,
                boss.position + Vector3.up * lookHeightOffset,
                lookBias);

            Vector3 lookDir = lookTarget - pos;
            if (lookDir.sqrMagnitude > 0.001f)
                rot = Quaternion.LookRotation(lookDir);
            else
                rot = transform.rotation;
        }

        void ComputeFollowCamera(out Vector3 pos, out Quaternion rot)
        {
            pos = player.position - player.forward * distance + Vector3.up * height;

            Vector3 lookTarget = player.position + Vector3.up * lookHeightOffset;
            Vector3 lookDir = lookTarget - pos;
            if (lookDir.sqrMagnitude > 0.001f)
                rot = Quaternion.LookRotation(lookDir);
            else
                rot = transform.rotation;
        }

        void SnapToDesired()
        {
            Vector3 desiredPos;
            Quaternion desiredRot;
            if (boss != null && boss.gameObject.activeInHierarchy)
                ComputeArenaCamera(out desiredPos, out desiredRot);
            else
                ComputeFollowCamera(out desiredPos, out desiredRot);

            transform.position = desiredPos;
            transform.rotation = desiredRot;
        }
    }
}
