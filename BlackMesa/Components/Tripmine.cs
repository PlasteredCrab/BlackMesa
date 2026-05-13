using System;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using BlackMesa.Utilities;
using GameNetcodeStuff;

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

        public float explosionOriginOffset;
        public float killRadius;
        public float hurtRadius;
        public int nonLethalDamage;
        public float sphereCastRange;
        public float sphereCastRadius;

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

        private bool TryGetPlayerForCollider(Collider collider, out PlayerControllerB player)
        {
            player = null;

            if (collider.CompareTag("Player") && collider.TryGetComponent(out player))
                return true;

            if (collider.tag.StartsWith("PlayerBody"))
            {
                player = collider.GetComponentInParent<PlayerControllerB>();
                return player != null;
            }

            return false;
        }

        private void OnTriggerStay(Collider other)
        {
            if (!activated)
                return;
            if (hasExplodedOnClient)
                return;

            NetworkBehaviour collidedBehaviour = null;
            if (TryGetPlayerForCollider(other, out var player) && !player.isPlayerDead)
                collidedBehaviour = player;
            else if (other.tag.StartsWith("PlayerRagdoll"))
                collidedBehaviour = other.GetComponent<DeadBodyInfo>()?.grabBodyObject;
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

        private struct ExplosionOrigin(Vector3 position, Vector3 direction)
        {
            public Vector3 position = position;
            public Vector3 direction = direction;
        }

        private ExplosionOrigin GetExplosionOrigin()
        {
            var position = transform.position;
            var direction = -transform.up;
            position += direction * explosionOriginOffset;
            return new ExplosionOrigin(position, direction);
        }

        private void Explode()
        {
            if (hasExplodedOnClient)
                return;
            hasExplodedOnClient = true;

            var origin = GetExplosionOrigin();
            BetterExplosion.SpawnExplosion(origin.position, killRadius, hurtRadius, nonLethalDamage);
            BetterExplosion.DeadlySphereCastExplosion(origin.position, origin.direction, sphereCastRadius, sphereCastRange, BetterExplosion.GetEnemyDamage(nonLethalDamage));

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

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void OnDrawGizmosSelected()
        {
            using (new Handles.DrawingScope())
            {
                var origin = GetExplosionOrigin();

                Handles.color = Color.red;
                Handles.DrawWireDisc(origin.position, transform.forward, killRadius);

                var sphereCastEndpoint = origin.position - transform.up * sphereCastRange;
                Handles.DrawWireArc(origin.position, transform.forward, transform.right, 180, sphereCastRadius);
                Handles.DrawLine(origin.position - transform.right * sphereCastRadius, sphereCastEndpoint - transform.right * sphereCastRadius);
                Handles.DrawLine(origin.position + transform.right * sphereCastRadius, sphereCastEndpoint + transform.right * sphereCastRadius);
                Handles.DrawWireArc(sphereCastEndpoint, transform.forward, transform.right, -180, sphereCastRadius);
            }
        }
    }
}
