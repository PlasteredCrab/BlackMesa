using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.VFX;

namespace BlackMesa.Components;

public class BarnacleSounds : NetworkBehaviour
{
    private static readonly System.Random soundRandomizer = new();

    [Header("Components")]
    public AudioSource mouthAudioSource;
    public AudioSource attachmentAudioSource;
    public AudioSource groundAudioSource;

    [Header("Clips")]
    public AudioClip[] idleSounds;

    public AudioClip[] flinchSounds;

    public AudioClip[] contactSounds;
    public AudioClip[] pullSounds;

    public AudioClip[] grabPlayerSounds;
    public AudioClip[] grabItemSounds;

    public AudioClip[] bigBiteSounds;
    public AudioClip[] smallBiteSounds;

    public AudioClip[] digestSounds;

    public AudioClip[] deathSounds;

    public AudioClip[] pukeSounds;
    public AudioClip[] splashSoundsA;
    public AudioClip[] splashSoundsB;

    private AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips.Length == 0)
            return null;
        var index = soundRandomizer.Next(clips.Length);
        return clips[index];
    }

    internal void PlayRandomSound(AudioSource source, AudioClip[] clips)
    {
        if (source == null)
            return;
        source.clip = GetRandomClip(clips);
        if (source.clip == null)
        {
            source.Stop();
            return;
        }
        source.Play();
    }

    internal void PlayRandomSoundOneShot(AudioSource source, AudioClip[] clips)
    {
        if (source == null)
            return;
        var clip = GetRandomClip(clips);
        if (clip == null)
            return;
        source.PlayOneShot(clip);
    }

    [ServerRpc]
    internal void PlayIdleSoundServerRpc()
    {
        PlayIdleSoundClientRpc(soundRandomizer.Next(idleSounds.Length));
    }

    [ClientRpc]
    private void PlayIdleSoundClientRpc(int index)
    {
        if (index < 0 || index >= idleSounds.Length)
            return;
        mouthAudioSource.clip = idleSounds[index];
        mouthAudioSource.Play();
    }

    internal void PlayContactSound()
    {
        PlayRandomSound(attachmentAudioSource, contactSounds);
    }

    internal void PlayGrabItemSound()
    {
        PlayRandomSoundOneShot(attachmentAudioSource, grabItemSounds);
    }

    internal void PlayGrabPlayerSound()
    {
        PlayRandomSoundOneShot(mouthAudioSource, grabPlayerSounds);
    }

    internal void PlayYankSound()
    {
        PlayRandomSound(mouthAudioSource, pullSounds);
    }

    public void PlayBigBiteSound()
    {
        PlayRandomSound(mouthAudioSource, bigBiteSounds);
    }

    public void PlaySmallBiteSound()
    {
        PlayRandomSound(mouthAudioSource, smallBiteSounds);
    }

    public void PlayDigestSound()
    {
        PlayRandomSound(mouthAudioSource, digestSounds);
    }

    public void PlayFlinchSound()
    {
        PlayRandomSound(mouthAudioSource, flinchSounds);
    }

    public void PlayPukeSound()
    {
        PlayRandomSound(mouthAudioSource, pukeSounds);
    }

    internal void PlaySplashSound()
    {
        PlayRandomSound(groundAudioSource, splashSoundsA);
        PlayRandomSoundOneShot(groundAudioSource, splashSoundsB);
    }

    internal void PlayDeathSound()
    {
        PlayRandomSound(mouthAudioSource, deathSounds);
    }
}
