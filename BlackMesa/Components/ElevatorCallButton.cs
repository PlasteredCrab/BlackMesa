using Unity.Netcode;
using UnityEngine;

namespace BlackMesa.Components;

public class ElevatorCallButton : NetworkBehaviour
{
    public ElevatorController controller;
    public ElevatorController.Position position;

    public Animator animator;
    public AudioSource audioSource;

    public AudioClip pressedSound;

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
        audioSource.PlayOneShot(pressedSound);
        animator.SetTrigger("Press");
        controller.CallElevator(position);
    }
}
