using System;
using EFT.HealthSystem;
using HarmonyLib;

namespace OperatorTraits
{
    // Thread-local depth is required because ApplyDamage can nest.
    internal static class LocalDamageContext
    {
        [ThreadStatic]
        private static int _depth;

        internal static bool Active => _depth > 0;
        internal static void Enter() => _depth++;
        internal static void Exit()
        {
            if (_depth > 0)
                _depth--;
        }
    }

    [HarmonyPatch(
        typeof(ActiveHealthController),
        nameof(ActiveHealthController.ApplyDamage))]
    internal static class LocalDamageContextPatch
    {
        private static void Prefix(
            ActiveHealthController __instance,
            out bool __state)
        {
            __state = __instance.Player != null &&
                      __instance.Player.IsYourPlayer &&
                      HasProbabilityTrait();
            if (__state)
                LocalDamageContext.Enter();
        }

        private static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state)
                LocalDamageContext.Exit();
            return __exception;
        }

        private static bool HasProbabilityTrait() =>
            Plugin.HasTrait(TraitIds.Thrombophilia) ||
            Plugin.HasTrait(TraitIds.Hemophilia) ||
            Plugin.HasTrait(TraitIds.SturdyBones) ||
            Plugin.HasTrait(TraitIds.Osteoporosis);
    }
}
