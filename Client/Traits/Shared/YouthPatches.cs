using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Comfort.Common;
using EFT;
using HarmonyLib;

namespace OperatorTraits
{
    [HarmonyPatch]
    internal static class YouthStaminaCapacityPatch
    {
        private static MethodBase TargetMethod()
        {
            System.Type computeType =
                typeof(Stamina).GetField(nameof(Stamina.TotalCapacity))!
                    .FieldType;
            return computeType.GetMethods(
                    BindingFlags.Public | BindingFlags.Static)
                .Single(method =>
                    method.Name == "op_Implicit" &&
                    method.ReturnType == typeof(float));
        }

        private static void Postfix(object __0, ref float __result)
        {
            if (!Singleton<GameWorld>.Instantiated)
                return;

            Player player = Singleton<GameWorld>.Instance.MainPlayer;
            if (player?.Physical == null)
                return;

            if (!ReferenceEquals(__0, player.Physical.Stamina.TotalCapacity) &&
                !ReferenceEquals(__0, player.Physical.HandsStamina.TotalCapacity))
                return;

            if (Plugin.HasTrait("youth"))
                __result += 10f;
            if (Plugin.HasTrait("exhaustion"))
                __result -= 10f;
        }
    }

    [HarmonyPatch]
    internal static class ExhaustionStaminaRestorationPatch
    {
        private static MethodBase TargetMethod()
        {
            System.Type computeType =
                typeof(Stamina).GetField(nameof(Stamina.SelfRestoration))!
                    .FieldType;
            return computeType.GetMethods(
                    BindingFlags.Public | BindingFlags.Static)
                .Single(method =>
                    method.Name == "op_Implicit" &&
                    method.ReturnType == typeof(float));
        }

        private const float RestorationMultiplier = 0.8f;

        private static void Postfix(object __0, ref float __result)
        {
            if (!Plugin.HasTrait("exhaustion") ||
                !Singleton<GameWorld>.Instantiated)
                return;

            Player player = Singleton<GameWorld>.Instance.MainPlayer;
            if (player?.Physical == null)
                return;

            if (ReferenceEquals(__0, player.Physical.Stamina.SelfRestoration) ||
                ReferenceEquals(__0, player.Physical.HandsStamina.SelfRestoration))
                __result *= RestorationMultiplier;
        }
    }
}
