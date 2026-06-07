using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace BlackMesa.Components;

[Serializable]
public class DiffusionProfileMapping
{
    public string diffusionProfileName;
    public Material[] materials;
}

public class DiffusionProfileMappings : MonoBehaviour
{
    public DiffusionProfileMapping[] mappings;

    private void Start()
    {
        if (mappings == null)
        {
            BlackMesaInterior.Logger.LogError($"Diffusion profile mappings were missing\n{new StackTrace()}");
            return;
        }

        var diffusionProfileList = HDRenderPipelineGlobalSettings.instance.GetOrCreateDiffusionProfileList();

        foreach (var diffusionProfileSettings in diffusionProfileList.diffusionProfiles.value)
        {
            if (diffusionProfileSettings == null)
                continue;

            var hashAsFloat = BitConverter.Int32BitsToSingle((int)diffusionProfileSettings.profile.hash);

            for (var i = 0; i < mappings.Length; i++)
            {
                var mapping = mappings[i];
                if (mapping.diffusionProfileName != diffusionProfileSettings.name)
                    continue;
                foreach (var material in mapping.materials)
                    material.SetFloat("_DiffusionProfileHash", hashAsFloat);
            }
        }
    }
}
