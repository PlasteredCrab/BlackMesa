using UnityEngine;

namespace BlackMesa.Components;

internal class ChangeMaterialsEvent : MonoBehaviour
{
    public Renderer renderer;
    public Material[] materialsToUse;

    private Material[] originalMaterials;

    private void OnEnable()
    {
        originalMaterials = renderer.sharedMaterials;
        renderer.sharedMaterials = materialsToUse;
    }

    private void OnDisable()
    {
        if (originalMaterials == null)
            return;
        renderer.sharedMaterials = originalMaterials;
        originalMaterials = null;
    }
}
