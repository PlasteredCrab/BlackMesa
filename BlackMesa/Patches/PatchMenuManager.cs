using BlackMesa.Components;
using HarmonyLib;

namespace BlackMesa.Patches;

[HarmonyPatch(typeof(MenuManager))]
internal static class PatchMenuManager
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(MenuManager.Start))]
    private static void StartPostfix()
    {
        LightSwitcher.CacheVanillaLightSounds();
    }
}
