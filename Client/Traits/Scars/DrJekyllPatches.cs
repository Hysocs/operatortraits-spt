using EFT.HealthSystem;
using HarmonyLib;

namespace OperatorTraits
{
    // Only natural expiry is blocked; explicit medical removal must still work.
    [HarmonyPatch(
        typeof(ActiveHealthController.Wound),
        nameof(ActiveHealthController.Wound.DefaultWorkTime),
        MethodType.Getter)]
    internal static class DrJekyllWoundDurationPatch
    {
        private static void Postfix(ref float __result)
        {
            if (!Plugin.HasTrait("dr--jekyll"))
                return;

            __result = float.PositiveInfinity;
        }
    }
}
