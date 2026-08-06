using EFT;
using EFT.HealthSystem;
using HarmonyLib;
using UnityEngine;

namespace OperatorTraits
{
    [HarmonyPatch(
        typeof(ActiveHealthController),
        nameof(ActiveHealthController.HandleFall))]
    internal static class BoneFallDamagePatch
    {
        private const float SturdyBonesMultiplier = 0.8f;
        private const float OsteoporosisMultiplier = 1.2f;

        private static void Prefix(
            ActiveHealthController __instance,
            ref float height)
        {
            bool sturdyBones = Plugin.HasTrait(TraitIds.SturdyBones);
            bool osteoporosis = Plugin.HasTrait(TraitIds.Osteoporosis);
            if ((!sturdyBones && !osteoporosis) ||
                __instance.Player == null ||
                !__instance.Player.IsYourPlayer)
                return;

            float excessHeight = Mathf.Max(
                0f,
                height - __instance.FallSafeHeight);
            if (excessHeight <= 0f)
                return;

            // EFT's fall damage is proportional to excessHeight^1.5, so the
            // height adjustment uses the 2/3 power of the desired multiplier.
            float combined = 1f;
            if (sturdyBones)
                combined *= Mathf.Pow(SturdyBonesMultiplier, 2f / 3f);
            if (osteoporosis)
                combined *= Mathf.Pow(OsteoporosisMultiplier, 2f / 3f);
            height = __instance.FallSafeHeight + excessHeight * combined;
        }
    }

    [HarmonyPatch(
        typeof(EffectsSettings.ProbabilitySetting),
        nameof(EffectsSettings.ProbabilitySetting.Try))]
    internal static class BoneFractureChancePatch
    {
        private static void Prefix(
            EffectsSettings.ProbabilitySetting __instance,
            ref float cap)
        {
            if (!LocalDamageContext.Active)
                return;

            EffectsSettings.FractureSettings fracture =
                ActiveHealthController.Effect.EffectsSettings.Fracture;
            if (!ReferenceEquals(__instance, fracture.FallingProbability) &&
                !ReferenceEquals(__instance, fracture.BulletHitProbability))
                return;

            if (Plugin.HasTrait(TraitIds.SturdyBones))
                cap /= 0.75f;
            if (Plugin.HasTrait(TraitIds.Osteoporosis))
                cap /= 1.25f;
        }
    }
}
