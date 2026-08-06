using System;
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using HarmonyLib;

namespace OperatorTraits
{
    [HarmonyPatch]
    internal static class MarathonRunnerConsumptionPatch
    {
        private const float ConsumptionRefundFraction = 0.2f;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(
                typeof(Stamina), nameof(Stamina.Process));
            yield return AccessTools.DeclaredMethod(
                typeof(Stamina), nameof(Stamina.Consume));
        }

        private static void Prefix(Stamina __instance, out float __state)
        {
            __state = float.NaN;
            if (!Plugin.HasTrait(TraitIds.MarathonRunner) ||
                !Singleton<GameWorld>.Instantiated)
                return;

            Player player = Singleton<GameWorld>.Instance.MainPlayer;
            if (player?.Physical == null ||
                (!ReferenceEquals(__instance, player.Physical.Stamina) &&
                 !ReferenceEquals(__instance, player.Physical.HandsStamina)))
                return;

            __state = __instance.Current;
        }

        private static void Postfix(Stamina __instance, float __state)
        {
            if (float.IsNaN(__state))
                return;

            float drain = __state - __instance.Current;
            if (drain > 0f)
                __instance.Current += drain * ConsumptionRefundFraction;
        }
    }
}
