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
            Debug.Log($"{isBeingUsed} {isPocketed}");
            //BlackMesaInterior.Instance.mls.LogInfo($"{isBeingUsed} {isPocketed}" );

        }

        public void DestroyTv()
        {
            //Debug.Log("I have blown up");
            //Landmine.mineAudio.pitch = Random.Range(0.93f, 1.07f);
            //mineAudio.PlayOneShot(mineDetonate, 1f);
            //RoundManager.PlayRandomClip

            //Vector3 explosionPosition, bool spawnExplosionEffect = false, float killRange = 1f, float damageRange = 1f)
            Landmine.SpawnExplosion(this.transform.position, spawnExplosionEffect: true, 1f, 2f); //, 90);  //for v50

            // drop the item
            if (this.playerHeldBy != null)
            {
                int itemSlot = Array.IndexOf(this.playerHeldBy.ItemSlots, this);
                this.playerHeldBy.DestroyItemInSlotAndSync(itemSlot);
            }
            //this.playerHeldBy.ItemSlots

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
