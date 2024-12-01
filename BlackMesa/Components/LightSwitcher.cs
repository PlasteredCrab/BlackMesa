using System;
using System.Collections.Generic;
using UnityEngine;

namespace BlackMesa.Components;

public class LightSwitcher : MonoBehaviour
{
    [Serializable]
    public struct LightMaterialReference
    {
        public Renderer renderer;
        public int index;
        public Material onMaterial;
        public Material offMaterial;
    }

    public static HashSet<LightSwitcher> lightSwitchers = [];

    private static AudioClip vanillaOnSound = null;
    private static AudioClip vanillaOffSound = null;
    private static AudioClip vanillaFlickerSound = null;

    internal static void CacheVanillaLightSounds()
    {
        if (vanillaOnSound != null)
            return;

        var audioClips = Resources.FindObjectsOfTypeAll<AudioClip>();
        foreach (var audioClip in audioClips)
        {
            if (vanillaOnSound == null && audioClip.name == "LightOn")
                vanillaOnSound = audioClip;
            else if (vanillaOffSound == null && audioClip.name == "LightOff")
                vanillaOffSound = audioClip;
            else if (vanillaFlickerSound == null && audioClip.name == "LightFlicker")
                vanillaFlickerSound = audioClip;
        }
    }

    public Light[] lights = [];
    public LightMaterialReference[] materialReferences = [];

    public AudioSource audioSource = null;
    public bool useVanillaAudio = false;
    public AudioClip onSound = null;
    public AudioClip offSound = null;
    public AudioClip flickerSound = null;

    private void Start()
    {
        if (useVanillaAudio)
        {
            onSound = vanillaOnSound;
            offSound = vanillaOffSound;
            flickerSound = vanillaFlickerSound;
        }
    }

    private void OnEnable()
    {
        lightSwitchers.Add(this);
    }

    private void OnDisable()
    {
        lightSwitchers.Remove(this);
    }

    internal void SwitchLight(bool on, bool sound)
    {
        foreach (var light in lights)
        {
            if (light == null)
                continue;
            light.enabled = on;
        }

        foreach (var materialRef in materialReferences)
        {
            if (materialRef.renderer == null)
                continue;
            var materials = materialRef.renderer.sharedMaterials;
            if (materialRef.index >= materials.Length)
            {
                Debug.LogWarning($"{this} references a material index that is out of bounds ({materialRef.index})");
                continue;
            }

            materials[materialRef.index] = on ? materialRef.onMaterial : materialRef.offMaterial;
            materialRef.renderer.sharedMaterials = materials;
        }

        if (sound)
        {
            var soundClip = on ? onSound : offSound;
            if (soundClip != null)
                audioSource.PlayOneShot(soundClip);
        }
    }

    internal void PlayFlickerSound()
    {
        if (audioSource == null)
            return;
        if (flickerSound == null)
            return;
        audioSource.pitch = UnityEngine.Random.Range(0.6f, 1.4f);
        audioSource.PlayOneShot(flickerSound);
    }
}
