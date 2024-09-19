using UnityEngine;

namespace BlackMesa.Components
{
    internal class Tripmine : MonoBehaviour
    {
        public LineRenderer laserRenderer;
        public BoxCollider laserCollider;
        public LayerMask playerLayer;

        public float killRadius;
        public float hurtRadius;

        private void Start()
        {
            SetupLaserAndCollider();
        }

        private void Update()
        {
            SetupLaserAndCollider();
        }

        // Method to handle the laser and collider adjustments
        public void SetupLaserAndCollider()
        {
            /*if (PrefabUtility.IsPartOfPrefabAsset(gameObject))
            {
                return;
            }*/

            if (!Physics.Raycast(transform.position, -transform.up, out var hit, float.PositiveInfinity, LayerMask.GetMask(new string[] { "Room", "Default" })))
            {
                gameObject.SetActive(false);
                return;
            }

            Debug.Log($"tripmine: {transform.position}");
            Debug.Log($"raycast hit: {hit.point}");
            Debug.Log($"Raycast Distance: {hit.distance / transform.lossyScale.y}");

            float distanceToWall = hit.distance / transform.lossyScale.y;
            float halfDistance = distanceToWall / 2f;

            Vector3[] laserPoints = new Vector3[2];
            laserPoints[0] = transform.position;
            laserPoints[1] = hit.point;

            var laserRendererLocal = laserRenderer.transform.worldToLocalMatrix;

            for (var i = 0; i < laserPoints.Length; i++)
            {
                laserPoints[i] = laserRendererLocal.MultiplyPoint3x4(laserPoints[i]);
            }

            laserRenderer.SetPositions(laserPoints);

            // Adjust the BoxCollider size and center
            laserCollider.size = new Vector3(laserCollider.size.x, distanceToWall, laserCollider.size.z);
            laserCollider.center = new Vector3(0f, -halfDistance, 0f); 
        }

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & playerLayer) != 0)
            {
                TriggerExplosion();
            }
        }

        public void TriggerExplosion()
        {
            BetterExplosion.SpawnExplosion(transform.position, killRadius, hurtRadius, 90);

            Destroy(gameObject);

            // destroy it on the server?
            /*if (Unity.Netcode.NetworkBehaviour.IsServer)
                Destroy(gameObject);*/
            
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, killRadius); // Example explosion radius
        }
    }
}
