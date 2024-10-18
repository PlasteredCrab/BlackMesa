using UnityEngine;

namespace BlackMesa.Components;

internal class ChangeMaterialsEvent : MonoBehaviour
{
    public Renderer renderer;
    public Material[] materialsToUse;

    private Material[] originalMaterials;

    public void ReplaceMaterials()
    {
        originalMaterials = renderer.sharedMaterials;
        renderer.sharedMaterials = materialsToUse;
    }

    public void RestoreOriginalMaterials()
    {
        if (originalMaterials == null)
            return;
        renderer.sharedMaterials = originalMaterials;
        originalMaterials = null;
    }
}
