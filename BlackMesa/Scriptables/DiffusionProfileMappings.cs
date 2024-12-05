using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace BlackMesa.Scriptables;

[CreateAssetMenu(menuName = "Black Mesa/Diffusion Profile Mappings")]
public class DiffusionProfileMappings : ScriptableObject
{
    public DiffusionProfileMapping[] mappings;

    internal void Apply()
    {
        var diffusionProfileList = HDRenderPipelineGlobalSettings.instance.GetOrCreateDiffusionProfileList();

        foreach (var diffusionProfileSettings in diffusionProfileList.diffusionProfiles.value)
        {
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
