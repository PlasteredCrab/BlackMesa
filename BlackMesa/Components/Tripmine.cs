using UnityEngine;
using Unity.Netcode;
using BlackMesa.Utilities;

namespace BlackMesa.Components
{
    internal class Tripmine : NetworkBehaviour
    {
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
        }

        public void SetupLaserAndCollider()
        {
            if (!Physics.Raycast(transform.position, -transform.up, out var hit, float.PositiveInfinity, LayerMask.GetMask(new string[] { "Room", "Default" })))
            {
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

        private void OnTriggerStay(Collider other)
        {
            if (!activated)
                return;
            if (hasExplodedOnClient)
                return;
            if (other.CompareTag("Player"))
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
