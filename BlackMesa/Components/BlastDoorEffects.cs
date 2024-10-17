using UnityEngine;

namespace BlackMesa.Components;

public class BlastDoorEffects : MonoBehaviour
{
    public AudioSource[] effectAudioSources = [];
    public ParticleSystem[] effectSparks = [];
    public AudioSource slamAudioSource;
    public AudioSource announcementAudioSource;

    private System.Random random;

    private void Start()
    {
        random = new(GetInstanceID());
    }

    public void StartEffects()
    {
        foreach (var gearAudioSource in effectAudioSources)
        {
            gearAudioSource.time = (float)(random.NextDouble() * gearAudioSource.clip.length);
            gearAudioSource.Play();
        }

        foreach (var sparkEmitter in effectSparks)
            sparkEmitter.Play();
    }

    public void EndEffects()
    {
        foreach (var gearAudioSource in effectAudioSources)
            gearAudioSource.Stop();

        foreach (var sparkEmitter in effectSparks)
            sparkEmitter.Stop();
    }

    public void PlaySlam()
    {
        slamAudioSource.Play();

        if (StartOfRound.Instance.audioListener == null)
            return;
        float cameraDistance = Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, slamAudioSource.transform.position);
        if (cameraDistance < 6)
            HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
        else if (cameraDistance < 12)
            HUDManager.Instance.ShakeCamera(ScreenShakeType.Small);
    }

    public void PlayAnnouncement()
    {
        announcementAudioSource.Play();
    }
}
