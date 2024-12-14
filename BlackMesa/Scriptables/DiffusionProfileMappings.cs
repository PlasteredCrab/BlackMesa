using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace BlackMesa.Scriptables;

[CreateAssetMenu(menuName = "Black Mesa/Diffusion Profile Mappings")]
public class DiffusionProfileMappings : ScriptableObject
{
    public DiffusionProfileMapping[] mappings;

    internal void Apply()
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
                if (mapping.diffusionProfileName == diffusionProfileSettings.name)
                    mapping.material.SetFloat("_DiffusionProfileHash", hashAsFloat);
            }
        }
    }
}

[Serializable]
public class DiffusionProfileMapping
{
    public Material material;
    public string diffusionProfileName;
}
