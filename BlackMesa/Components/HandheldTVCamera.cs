using BlackMesa.Utilities;
using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace BlackMesa.Components
{
    public class HandheldTVCamera : WalkieTalkie, INightVisionCamera
    {
        public Camera securityCamera;
        public Light nightVisionLight;

        public Camera Camera => securityCamera;

        public Light NightVisionLight => nightVisionLight;

        public AudioClip HandheldExplosionWarning;

        public float HandheldExplosionDelay = 5;

        private bool hasExploded = false;

        public override void Start()
        {
            base.Start();
            securityCamera.targetTexture = new RenderTexture(securityCamera.targetTexture);
            onMaterial = new Material(onMaterial)
            {
                mainTexture = securityCamera.targetTexture
            };

            SecurityCameraManager.Instance.AssignHandheldTVFeed(this, onMaterial);
        }

        public void ShipIsLeaving()
        {
            if (!IsServer)
                return;
            StartCoroutine(ExplodeAfterDelay());
        }

        private IEnumerator ExplodeAfterDelay()
        {
            yield return new WaitForSeconds(10);
            thisAudio.PlayOneShot(HandheldExplosionWarning);
            yield return new WaitForSeconds(HandheldExplosionDelay);
            ExplodeClientRPC();
        }

        [ClientRpc]
        public void ExplodeClientRPC()
        {
            if (hasExploded)
                return;
            hasExploded = true;

            BetterExplosion.SpawnExplosion(transform.position, 0.5f, 2, 20);

            if (playerHeldBy != null)
            {
                int itemSlot = Array.IndexOf(playerHeldBy.ItemSlots, this);
                playerHeldBy.DestroyItemInSlot(itemSlot);
                return;
            }

            if (!IsServer)
                return;
            NetworkObject.Despawn();
        }

        public override void LateUpdate()
        {
            base.LateUpdate();
            if (isPocketed && playerHeldBy != null)
            {
                Vector3 positionOffset = new Vector3(0, 1, 0);
                Vector3 rotationOffset = new Vector3(0, 90, 90);

                var playerSpineTransform = playerHeldBy.lowerSpine;
                transform.position = playerSpineTransform.position + positionOffset;
                transform.rotation = playerSpineTransform.rotation * Quaternion.Euler(rotationOffset);
            }
        }
    }
}
