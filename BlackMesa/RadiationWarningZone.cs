using GameNetcodeStuff;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Bindings;

namespace BlackMesa
{
    public class RadiationWarningZone : MonoBehaviour
    {
        // Called when you enter the trigger
        void OnTriggerEnter(Collider radiationZone)
        {
            if (!radiationZone.gameObject.CompareTag("Player") || radiationZone == null)
                return;

            PlayerControllerB player = radiationZone.gameObject.GetComponent<PlayerControllerB>(); // get player that entered the trigger zone

            if (player != StartOfRound.Instance.localPlayerController)
                return;

            Debug.Log("Entered Radiation Warning Zone"); // Console message when entering the zone

            HUDManager.Instance.RadiationWarningHUD();
            //yield return new WaitForSeconds(2.5f);
            HUDManager.Instance.gasHelmetAnimator.SetBool("gasEmitting", value: true);
        }

        // Called when you exit the trigger
        void OnTriggerExit(Collider radiationZone)
        {
            if (!radiationZone.gameObject.CompareTag("Player") || radiationZone == null)
                return;

            PlayerControllerB player = radiationZone.gameObject.GetComponent<PlayerControllerB>(); // get player that entered the trigger zone
            //player.playerHudUIContainer.BroadcastMessage 
            //HUDManager.

            Debug.Log("Exited Radiation Warning Zone"); // Console message when exiting the zone
            HUDManager.Instance.gasHelmetAnimator.SetBool("gasEmitting", value: false);
        }
    }
}