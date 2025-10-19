using System;
using Unity.Netcode;
using UnityEngine;

namespace BlackMesa.Components;

public class ElevatorInsideButton : NetworkBehaviour
{
    public ElevatorController controller;
    public AudioSource audioSource;

    public AudioClip pressedSound;
    public AudioClip disabledSound;

    public void PushButton()
    {
        PushButtonOnClient();
        PushButtonServerRpc(StartOfRound.Instance.localPlayerController.actualClientId);
    }

    [Rpc(SendTo.Server, RequireOwnership = false)]
    private void PushButtonServerRpc(ulong sendingClientID)
    {
        PushButtonClientRpc(RpcTarget.Not(sendingClientID, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams)]
    private void PushButtonClientRpc(RpcParams rpcParams)
    {
        PushButtonOnClient();
    }

    private void PushButtonOnClient()
    {
        if (!controller.doorsOpen)
        {
            audioSource.PlayOneShot(disabledSound);
            return;
        }

        audioSource.PlayOneShot(pressedSound);
        controller.CallElevator(controller.targetFloor switch
        {
            ElevatorController.Position.Bottom => ElevatorController.Position.Top,
            ElevatorController.Position.Top => ElevatorController.Position.Bottom,
            _ => throw new InvalidOperationException(),
        });
    }
}
