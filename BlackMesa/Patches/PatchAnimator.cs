using BlackMesa.Components;
using HarmonyLib;
using UnityEngine;

namespace BlackMesa.Patches;

[HarmonyPatch(typeof(Animator))]
internal static class PatchAnimator
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(Animator.SetTriggerString))]
    private static void SetTriggerStringPostfix(Animator __instance)
    {
        AnimatorCuller.OnAnimationTriggered(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Animator.SetTriggerID))]
    private static void SetTriggerIDPostfix(Animator __instance)
    {
        AnimatorCuller.OnAnimationTriggered(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Animator.SetBoolString))]
    private static void SetBoolStringPostfix(Animator __instance)
    {
        AnimatorCuller.OnAnimationTriggered(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(Animator.SetBoolID))]
    private static void SetBoolIDPostfix(Animator __instance)
    {
        AnimatorCuller.OnAnimationTriggered(__instance);
    }
}
