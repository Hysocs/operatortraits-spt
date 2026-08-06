using Comfort.Common;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace OperatorTraits
{
    [HarmonyPatch(
        typeof(MovementContext),
        nameof(MovementContext.DirectApplyMotion))]
    internal static class SprinterPatch
    {
        private const float SprintSpeedMultiplier = 1.05f;

        private static void Prefix(
            MovementContext __instance,
            ref Vector3 __0,
            out float __state)
        {
            __state = float.NaN;
            if (!Plugin.HasTrait("sprinter"))
                return;

            Player player = Singleton<GameWorld>.Instance?.MainPlayer;
            if (player?.Physical == null ||
                !ReferenceEquals(player.MovementContext, __instance) ||
                !player.Physical.Sprinting)
                return;

            __0.x *= SprintSpeedMultiplier;
            __0.z *= SprintSpeedMultiplier;

            ICharacterController controller = __instance.CharacterController;
            if (controller != null)
            {
                __state = controller.SpeedLimit;
                controller.SpeedLimit = -1f;
            }
        }

        private static void Postfix(MovementContext __instance, float __state)
        {
            ICharacterController controller = __instance.CharacterController;
            if (controller != null && !float.IsNaN(__state))
                controller.SpeedLimit = __state;
        }
    }
}
