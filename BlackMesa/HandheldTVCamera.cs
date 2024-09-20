using BlackMesa.Utilities;
using System;
using UnityEngine;

namespace BlackMesa
{
    internal class HandheldTVCamera : WalkieTalkie, INightVisionCamera
    {
        [SerializeField]
        private Camera securityCamera;
        [SerializeField]
        private Light nightVisionLight;

        public Camera Camera => securityCamera;

        public Light NightVisionLight => nightVisionLight;


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

        public void DestroyTv()
        {
            if (playerHeldBy != null)
            {
                if (playerHeldBy.IsOwner)
                {
                    int itemSlot = Array.IndexOf(playerHeldBy.ItemSlots, this);
                    playerHeldBy.DestroyItemInSlotAndSync(itemSlot);
                }
                return;
            }

            if (IsServer)
                NetworkObject.Despawn();
        }

        public override void OnDestroy()
        {
            BetterExplosion.SpawnExplosion(transform.position, 1, 2, 90);

            base.OnDestroy();
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
