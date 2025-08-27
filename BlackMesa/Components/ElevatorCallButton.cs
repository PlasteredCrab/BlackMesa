using UnityEngine;

namespace BlackMesa.Components;

public class ElevatorCallButton : MonoBehaviour
{
    public ElevatorController controller;
    public ElevatorController.Position position;

    public Animator animator;
    public AudioSource audioSource;

    public AudioClip pressedSound;

    public void PushButton()
    {
        audioSource.PlayOneShot(pressedSound);
        animator.SetTrigger("Press");
        controller.CallElevator(position);
    }
}
