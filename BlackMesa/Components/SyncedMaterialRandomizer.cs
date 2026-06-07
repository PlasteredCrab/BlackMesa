using System;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using BlackMesa.Utilities;
using GameNetcodeStuff;

namespace BlackMesa.Components;

[Serializable]
internal class MeshAndMaterials
{
    public Mesh mesh;
    public Material[] materials;
}

internal class SyncedVariantRandomizer : NetworkBehaviour
{
    public Renderer renderer;
    public MeshFilter meshFilter;
    public MeshAndMaterials[] variants;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
            return;
        if (variants.Length == 0)
            return;
        var index = UnityEngine.Random.Range(0, variants.Length);
        SetVariantServerRpc(index);
    }

    [ServerRpc(RequireOwnership = true)]
    private void SetVariantServerRpc(int variantIndex)
    {
        SetVariantClientRpc(variantIndex);
    }

    [ClientRpc]
    private void SetVariantClientRpc(int variantIndex)
    {
        SetVariantOnClient(variantIndex);
    }

    private void SetVariantOnClient(int variantIndex)
    {
        var variant = variants[variantIndex];
        if (renderer != null) {
            if (variant.materials != null && variant.materials.Length > 0)
                renderer.sharedMaterials = [..variant.materials];

            if (variant.mesh != null && renderer is SkinnedMeshRenderer skinned)
                skinned.sharedMesh = variant.mesh;
        }

        if (variant.mesh != null && meshFilter != null)
            meshFilter.mesh = variant.mesh;
    }
}
