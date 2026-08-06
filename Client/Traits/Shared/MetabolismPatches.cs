using EFT.HealthSystem;
using HarmonyLib;

namespace OperatorTraits
{
    [HarmonyPatch(
        typeof(ActiveHealthController.Existence),
        nameof(ActiveHealthController.Existence.GetHydrationDamage))]
    internal static class HypodipsiaPatch
    {
        private const float HydrationConsumptionMultiplier = 0.8f;

        private static void Postfix(ref float __result)
        {
            if (Plugin.HasTrait("hypodipsia"))
                __result *= HydrationConsumptionMultiplier;
            if (Plugin.HasTrait("polydipsia"))
                __result *= 1.2f;
        }
    }

    [HarmonyPatch(
        typeof(ActiveHealthController.Existence),
        nameof(ActiveHealthController.Existence.GetEnergyDamage))]
    internal static class PolyphagiaPatch
    {
        private const float EnergyConsumptionMultiplier = 0.8f;

        private static void Postfix(ref float __result)
        {
            if (Plugin.HasTrait("polyphagia"))
                __result *= EnergyConsumptionMultiplier;
            if (Plugin.HasTrait("youth"))
                __result *= 0.8f;
            if (Plugin.HasTrait("chronic-fatigue-syndrome"))
                __result *= 1.15f;
        }
    }
}
