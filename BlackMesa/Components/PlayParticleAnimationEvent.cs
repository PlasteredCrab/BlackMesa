using UnityEngine;

namespace BlackMesa.Components;

internal class PlayParticleAnimationEvent : MonoBehaviour
{
    public ParticleSystem particleSystemA;

    public void PlayParticleSystemA()
    {
        particleSystemA.Play();
    }

    public void StopParticleSystemA()
    {
        particleSystemA.Stop();
    }
}
