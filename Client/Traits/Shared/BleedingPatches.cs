using EFT.HealthSystem;
using HarmonyLib;

namespace OperatorTraits
{
    [HarmonyPatch(
        typeof(EffectsSettings.ProbabilitySetting),
        nameof(EffectsSettings.ProbabilitySetting.Try))]
    internal static class BleedChancePatch
    {
        // Probability.Try rolls against cap, so divide cap to scale chance.

        private static void Prefix(
            EffectsSettings.ProbabilitySetting __instance,
            ref float cap)
        {
            if (!LocalDamageContext.Active)
                return;

            EffectsSettings effects =
                ActiveHealthController.Effect.EffectsSettings;
            if (!ReferenceEquals(
                    __instance,
                    effects.HeavyBleeding.Probability) &&
                !ReferenceEquals(
                    __instance,
                    effects.LightBleeding.Probability))
                return;

            if (Plugin.HasTrait(TraitIds.Thrombophilia))
                cap /= 0.75f;
            if (Plugin.HasTrait(TraitIds.Hemophilia))
                cap /= 1.25f;
        }
    }
}
