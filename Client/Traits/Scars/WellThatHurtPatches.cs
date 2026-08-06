using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace OperatorTraits
{
    // Drain after healing; scaling _healthPoints would incorrectly boost healing.
    [HarmonyPatch(
        typeof(ActiveHealthController.MedEffect),
        nameof(ActiveHealthController.MedEffect.RegularUpdate))]
    internal static class RaidWellThatHurtPatch
    {
        private const float ExtraDrainFraction = 0.25f;

        private static void Prefix(
            ActiveHealthController.MedEffect __instance,
            out float __state)
        {
            __state = 0f;
            if (!Plugin.HasTrait("well-that-hurt") ||
                __instance._medKit == null ||
                __instance._interrupted)
                return;

            __state = __instance._medKit.HpResource;
        }

        private static void Postfix(
            ActiveHealthController.MedEffect __instance,
            float __state)
        {
            if (__state == 0f || __instance._medKit == null)
                return;

            float consumed = __state - __instance._medKit.HpResource;
            if (consumed <= 0f)
                return;

            float extra = consumed * ExtraDrainFraction;
            __instance._medKit.HpResource = Mathf.Max(
                0f,
                __instance._medKit.HpResource - extra);

            // Preserve EFT's resource network synchronization.
            __instance.HealthController.NetworkSyncEffectMedResource(
                __instance, __instance._medKit.HpResource);
        }
    }
}
