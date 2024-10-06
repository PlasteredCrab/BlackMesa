using System.Text;
using UnityEngine;

namespace BlackMesa.Utilities;

public static class Extensions
{
    public static string GetPath(this Transform transform)
    {
        StringBuilder builder = new(transform.name);

        while (transform.parent != null)
        {
            transform = transform.parent;
            builder.Insert(0, '/');
            builder.Insert(0, transform.name);
        }

        return builder.ToString();
    }

    public static void SetMaterial(this Renderer renderer, int index, Material material)
    {
        var materials = renderer.sharedMaterials;
        materials[index] = material;
        renderer.sharedMaterials = materials;
    }
}
