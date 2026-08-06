using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace OperatorTraits
{
    [HarmonyPatch(typeof(MovementContext), nameof(MovementContext.DirectApplyMotion))]
    internal static class ThirdLegMovementPatch
    {
        private const float MovementMultiplier = 0.99f;

        private static void Prefix(MovementContext __instance, ref Vector3 __0)
        {
            if (!Plugin.HasTrait("third-leg") ||
                !Singleton<GameWorld>.Instantiated)
                return;

            Player player = Singleton<GameWorld>.Instance.MainPlayer;
            if (player == null ||
                !ReferenceEquals(player.MovementContext, __instance))
                return;

            __0.x *= MovementMultiplier;
            __0.z *= MovementMultiplier;
        }
    }
}
