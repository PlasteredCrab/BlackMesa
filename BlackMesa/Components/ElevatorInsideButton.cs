using System;
using UnityEngine;

namespace BlackMesa.Components;

public class ElevatorInsideButton : MonoBehaviour
{
    public ElevatorController controller;
    public AudioSource audioSource;

    public AudioClip pressedSound;
    public AudioClip disabledSound;

    public void PushButton()
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
