using UnityEngine;
using Unity.Netcode;
using BlackMesa.Utilities;

namespace BlackMesa.Components
{
    internal class Tripmine : NetworkBehaviour
    {
        public LineRenderer laserRenderer;
        public BoxCollider laserCollider;

        public float killRadius;
        public float hurtRadius;

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

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
                TriggerExplosionServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        public void TriggerExplosionServerRpc()
        {
            BlackMesaInterior.Logger.LogInfo("TriggerExplosionServerRpc");
            TriggerExplosionClientRpc();
        }

        [ClientRpc]
        public void TriggerExplosionClientRpc()
        {
            BlackMesaInterior.Logger.LogInfo("TriggerExplosionClientRpc");
            Explode();
        }

        private void Explode()
        {
            BlackMesaInterior.Logger.LogInfo("Explode");
            BetterExplosion.SpawnExplosion(transform.position, killRadius, hurtRadius, 90);

            gameObject.SetActive(false);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, killRadius); // Example explosion radius
        }
    }
}
