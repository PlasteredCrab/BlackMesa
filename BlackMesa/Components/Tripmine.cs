using UnityEngine;
using Unity.Netcode;
using BlackMesa.Utilities;
using GameNetcodeStuff;
using System;

namespace BlackMesa.Components
{
    internal class Tripmine : NetworkBehaviour
    {
        public static Lazy<int> RaycastLayers = new(() => LayerMask.GetMask("Room"));

        public LineRenderer laserRenderer;
        public BoxCollider laserCollider;

        public TerminalAccessibleObject terminalObject;
        public AudioSource toggleAudio;
        public AudioClip deactivationClip;
        public AudioClip activationClip;

        public float killRadius;
        public float hurtRadius;

        private bool hasExplodedOnClient = false;

        private bool activated = true;

        private void Start()
        {
            SetupLaserAndCollider();
            PlaceTerminalAccessibleObjectOnFloor();
        }

        public void SetupLaserAndCollider()
        {
            if (!Physics.Raycast(transform.position, -transform.up, out var hit, float.PositiveInfinity, RaycastLayers.Value, QueryTriggerInteraction.Ignore))
            {
                BlackMesaInterior.Logger.LogWarning($"{this} at {transform.position} failed its raycast, disabling.");
                gameObject.SetActive(false);
                return;
            }

            float distanceToWall = hit.distance / transform.lossyScale.y;
            float halfDistance = distanceToWall / 2f;

            // Create an array of points for the line renderer to use
            Vector3[] laserPoints = [transform.position, hit.point];

            var laserRendererLocal = laserRenderer.transform.worldToLocalMatrix;
            for (var i = 0; i < laserPoints.Length; i++)
                laserPoints[i] = laserRendererLocal.MultiplyPoint3x4(laserPoints[i]);

            laserRenderer.SetPositions(laserPoints);

            // Adjust the BoxCollider size and center
            laserCollider.size = new Vector3(laserCollider.size.x, distanceToWall, laserCollider.size.z);
            laserCollider.center = new Vector3(0f, -halfDistance, 0f);
        }

        public void PlaceTerminalAccessibleObjectOnFloor()
        {
            // Originate the ray from slightly in front of the tripmine to avoid hitting whatever it may be
            // attached to.
            var origin = transform.position - transform.up * 0.2f;

            if (!Physics.Raycast(origin, -transform.forward, out var hit, 3, RaycastLayers.Value))
            {
                terminalObject.transform.position = origin;
                return;
            }

            terminalObject.transform.position = hit.point;
        }

        private void OnTriggerStay(Collider other)
        {
            if (!activated)
                return;
            if (hasExplodedOnClient)
                return;

            NetworkBehaviour collidedBehaviour = null;
            if (other.CompareTag("Player") && other.TryGetComponent<PlayerControllerB>(out var player) && !player.isPlayerDead)
                collidedBehaviour = player;
            if (other.CompareTag("PlayerBody"))
                collidedBehaviour = other.GetComponentInParent<PlayerControllerB>();
            else if (other.tag.StartsWith("PlayerRagdoll"))
                collidedBehaviour = other.GetComponent<DeadBodyInfo>()?.playerScript;
            else if (other.CompareTag("PhysicsProp"))
                collidedBehaviour = other.GetComponent<GrabbableObject>();

            if (collidedBehaviour == null)
                return;
            if (!collidedBehaviour.IsOwner)
                return;

            Explode();
            TriggerExplosionServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        public void TriggerExplosionServerRpc()
        {
            TriggerExplosionClientRpc();
        }

        [ClientRpc]
        public void TriggerExplosionClientRpc()
        {
            Explode();
        }

        private void Explode()
        {
            if (hasExplodedOnClient)
                return;
            hasExplodedOnClient = true;

            BetterExplosion.SpawnExplosion(transform.position, killRadius, hurtRadius, 90);

            gameObject.SetActive(false);
            Destroy(terminalObject);
        }

        public void SetActivated(bool activated)
        {
            SetActivatedOnLocalClient(activated);
            SetActivatedServerRpc(activated);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetActivatedServerRpc(bool activated)
        {
            SetActivatedClientRpc(activated);
        }

        [ClientRpc]
        private void SetActivatedClientRpc(bool activated)
        {
            SetActivatedOnLocalClient(activated);
        }

        private void SetActivatedOnLocalClient(bool activated)
        {
            if (this.activated == activated)
                return;

            this.activated = activated;

            laserRenderer.enabled = activated;

            PlayToggleAudio();
        }

        private void PlayToggleAudio()
        {
            if (toggleAudio == null)
                return;

            if (activated && activationClip != null)
                toggleAudio.PlayOneShot(activationClip);

            if (!activated && deactivationClip != null)
                toggleAudio.PlayOneShot(deactivationClip);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, killRadius); // Example explosion radius
        }
    }
}
