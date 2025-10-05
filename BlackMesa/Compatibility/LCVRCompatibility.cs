
using System.Runtime.CompilerServices;
using BepInEx.Bootstrap;
using UnityEngine;

namespace BlackMesa.Compatibility;

internal static class LCVRCompatibility
{
    internal static void AddElevatorButtonInteractions(GameObject[] buttons)
    {
#if !BUILD_FOR_EDITOR
        if (Chainloader.PluginInfos.ContainsKey("io.daxcess.lcvr"))
            AddElevatorButtonInteractionsImpl(buttons);
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AddElevatorButtonInteractionsImpl(GameObject[] buttons)
    {
#if !BUILD_FOR_EDITOR
        if (LCVR.Plugin.Config.DisableElevatorButtonInteraction.Value)
            return;

        foreach (var button in buttons)
            button.AddComponent<LCVR.Physics.Interactions.ElevatorButton>();
#endif
    }
}
