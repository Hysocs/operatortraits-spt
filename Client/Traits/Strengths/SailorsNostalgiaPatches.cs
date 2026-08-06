using System;
using System.Collections.Generic;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;

namespace OperatorTraits
{
    internal static class SailorsNostalgia
    {
        private const float DurationSeconds = 30f;
        private const string StimulatorBuff =
            "BuffsOperatorTraitsSailorsNostalgia";

        private static readonly HashSet<string> CannedFishTemplateIds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "5673de654bdc2d180f8b456d", // Pacific saury
                "57347d5f245977448b40fa81", // Pink salmon
                "57347d9c245977448b40fa85", // Herring
                "5bc9c29cd4351e003562b8a3"  // Sprats
            };

        internal static bool IsEligible(Item item)
        {
            return Plugin.HasTrait("sailor-s-nostalgia") &&
                   item is FoodDrink &&
                   CannedFishTemplateIds.Contains(item.StringTemplateId);
        }

        internal static void Apply(
            ActiveHealthController healthController,
            Item sourceItem)
        {
            healthController.AddStackableEffect<
                ActiveHealthController.Stimulator>(
                EBodyPart.Head,
                sourceItem,
                1f,
                0f,
                DurationSeconds,
                0f,
                effect => effect.StoreValues(
                    StimulatorBuff,
                    sourceItem.TemplateId,
                    EBodyPart.Head));
        }
    }

    [HarmonyPatch(
        typeof(ActiveHealthController.MedEffect),
        nameof(ActiveHealthController.MedEffect.Residue))]
    internal static class RaidSailorsNostalgiaPatch
    {
        private static void Prefix(
            ActiveHealthController.MedEffect __instance,
            out bool __state)
        {
            // Capture eligibility before EFT finishes and potentially removes
            // an exhausted Use All item from its inventory owner.
            __state = !__instance._interrupted &&
                      SailorsNostalgia.IsEligible(__instance.MedItem);
        }

        private static void Postfix(
            ActiveHealthController.MedEffect __instance,
            bool __state)
        {
            if (__state && !__instance._interrupted)
            {
                SailorsNostalgia.Apply(
                    __instance.HealthController,
                    __instance.MedItem);
                Plugin.Log.LogInfo(
                    $"Sailor's Nostalgia applied from {__instance.MedItem.StringTemplateId}: " +
                    "+2 health regeneration for 30 seconds.");
            }
        }
    }

    [HarmonyPatch(
        typeof(OfflineHealthController.MedEffect),
        nameof(OfflineHealthController.MedEffect.Started))]
    internal static class StashSailorsNostalgiaPatch
    {
        private static void Prefix(
            OfflineHealthController.MedEffect __instance,
            out bool __state)
        {
            __state = SailorsNostalgia.IsEligible(__instance.MedItem);
        }

        private static void Postfix(
            OfflineHealthController.MedEffect __instance,
            bool __state)
        {
            if (!__state)
                return;

            OfflineHealthController.Effect<StimulatorStore>.Create<
                OfflineHealthController.Stimulator>(
                __instance._health,
                EBodyPart.Head,
                new Profile.HealthInfo.EffectInfo
                {
                    Time = 10f
                },
                __instance._health.UpdateTime,
                new StimulatorStore
                {
                    BuffsName = "BuffsOperatorTraitsSailorsNostalgia",
                    ItemTemplateId = __instance.MedItem.StringTemplateId,
                    LastAppliedTo = EBodyPart.Head
                });

            Plugin.Log.LogInfo(
                "Sailor's Nostalgia stimulant effect applied from stash consumption.");
        }
    }
}
