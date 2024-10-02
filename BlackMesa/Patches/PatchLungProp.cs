using BlackMesa.Components;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace BlackMesa.Patches;

[HarmonyPatch(typeof(LungProp))]
internal class PatchLungProp
{
    [HarmonyTranspiler]
    [HarmonyPatch(nameof(LungProp.DisconnectFromMachinery), MethodType.Enumerator)]
    private static IEnumerable<CodeInstruction> DisconnectFromMachineryTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var matcher = new CodeMatcher(instructions)
            .MatchForward(true, [
                new CodeMatch(OpCodes.Ldc_I4_0),
                new CodeMatch(OpCodes.Callvirt, typeof(RoundManager).GetMethod(nameof(RoundManager.SwitchPower), [typeof(bool)])),
            ]);
        if (matcher.IsInvalid)
        {
            BlackMesaInterior.Logger.LogError($"Failed to patch DisconnectFromMachinery()");
            return instructions;
        }
        matcher
            .Advance(1)
            .Insert([
                new CodeInstruction(OpCodes.Call, typeof(LungPropDisconnectedHandler).GetMethod(nameof(LungPropDisconnectedHandler.TriggerAllEvents), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static))
            ]);
        return matcher.Instructions();
    }
}
