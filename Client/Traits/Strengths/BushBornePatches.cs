using EFT;
using EFT.Interactive;
using HarmonyLib;

namespace OperatorTraits
{
    [HarmonyPatch(
        typeof(MovementContext),
        nameof(MovementContext.AddStateSpeedLimit),
        new[] { typeof(float), typeof(Player.ESpeedLimit) })]
    internal static class BushBorneSlowdownPatch
    {
        private static void Prefix(
            MovementContext __instance,
            ref float speedLimit,
            Player.ESpeedLimit cause)
        {
            if (Plugin.HasTrait("bush-borne") &&
                cause == Player.ESpeedLimit.Swamp &&
                GamePlayerOwner.MyPlayer != null &&
                ReferenceEquals(
                    GamePlayerOwner.MyPlayer.MovementContext,
                    __instance))
            {
                speedLimit = 1f - (1f - speedLimit) * 0.25f;
            }
        }
    }

    [HarmonyPatch(
        typeof(TreeInteractive),
        nameof(TreeInteractive.PlaySoundBank))]
    internal static class BushBorneNoisePatch
    {
        private static void Prefix(
            IObserverToPlayerBridge player,
            ref float ____lastVolume,
            out float __state)
        {
            __state = ____lastVolume;
            if (Plugin.HasTrait("bush-borne") &&
                player?.iPlayer != null &&
                player.iPlayer.IsYourPlayer)
                ____lastVolume *= 0.25f;
        }

        private static void Postfix(
            IObserverToPlayerBridge player,
            ref float ____lastVolume,
            float __state)
        {
            if (Plugin.HasTrait("bush-borne") &&
                player?.iPlayer != null &&
                player.iPlayer.IsYourPlayer)
                ____lastVolume = __state;
        }
    }
}
