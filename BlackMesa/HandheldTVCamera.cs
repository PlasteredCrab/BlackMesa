using GameNetcodeStuff;
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
            onMaterial = new Material(onMaterial);
            onMaterial.mainTexture = securityCamera.targetTexture;

            SecurityCameraManager.Instance.AssignHandheldTVFeed(this, onMaterial);
        }

        public void TurnOffCamera()
        {
            if (isBeingUsed && !isPocketed)
                securityCamera.enabled = true;
            else
                securityCamera.enabled = false;
        }

        public void DestroyTv()
        {
            var explosionAt = transform;
            // We're running in the Update() loop, but LateUpdate() will move the item.
            // Spawn the explosion at its future position.
            if (parentObject != null)
                explosionAt = parentObject.transform;
            BetterExplosion.SpawnExplosion(explosionAt.position, 1, 2, 90);

            if (playerHeldBy != null)
            {
                int itemSlot = Array.IndexOf(playerHeldBy.ItemSlots, this);
                playerHeldBy.DestroyItemInSlotAndSync(itemSlot);
                return;
            }

            if (IsServer)
                Destroy(gameObject);
        }

        public override void ItemInteractLeftRight(bool right)
        {
            base.ItemInteractLeftRight(right);
            TurnOffCamera();
        }

        public override void UseUpBatteries()
        {
            base.UseUpBatteries();
            TurnOffCamera();
        }

        public override void PocketItem()
        {
            base.PocketItem();
            TurnOffCamera();
        }

        public override void EquipItem()
        {
            base.EquipItem();
            TurnOffCamera();
        }

        public override void LateUpdate()
        {
            base.LateUpdate();
            if (isPocketed && playerHeldBy != null)
            {
                Vector3 positionOffset = new Vector3(0, 2, 0);
                Vector3 rotationOffset = new Vector3(0, 90, 90);

                var playerSpineTransform = playerHeldBy.lowerSpine;
                transform.position = playerSpineTransform.position + positionOffset;
                transform.rotation = playerSpineTransform.rotation * Quaternion.Euler(rotationOffset);
            }
        }
    }
}
