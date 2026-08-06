using System;
using System.Collections.Generic;
using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;

namespace OperatorTraits
{
    internal static class JuiceTime
    {
        private const float DurationSeconds = 60f;

        // SPT 4.1.1 juice templates. The shared Drinks parent also contains
        // water, milk, soda, alcohol, and energy drinks, so it is too broad.
        private static readonly HashSet<string> JuiceTemplateIds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "544fb62a4bdc2dfb738b4568", // Russian Army pineapple
                "57513f07245977207e26a311", // Apple
                "57513f9324597720a7128161", // Grand grapefruit
                "57513fcc24597720a31c09a6"  // Vita
            };

        internal static bool IsEligible(Item item)
        {
            return Plugin.HasTrait("juice-time") &&
                   item is Drink &&
                   JuiceTemplateIds.Contains(item.StringTemplateId);
        }

        internal static void ApplyInRaid(
            ActiveHealthController healthController,
            Item sourceItem)
        {
            healthController.AddStackableEffect<
                ActiveHealthController.PainKiller>(
                EBodyPart.Head,
                sourceItem,
                1f,
                0f,
                DurationSeconds,
                0f,
                effect => effect.StoreValues(
                    sourceItem.TemplateId,
                    DurationSeconds));
        }

        internal static void ApplyOutOfRaid(
            OfflineHealthController healthController,
            Item sourceItem)
        {
            OfflineHealthController.PainKiller effect =
                OfflineHealthController.Effect.Create<
                    OfflineHealthController.PainKiller>(
                healthController,
                EBodyPart.Head,
                new Profile.HealthInfo.EffectInfo
                {
                    Time = DurationSeconds
                },
                healthController.UpdateTime);
            effect.StoreValues(sourceItem.TemplateId, DurationSeconds);
        }
    }

    [HarmonyPatch(
        typeof(ActiveHealthController.MedEffect),
        nameof(ActiveHealthController.MedEffect.Residue))]
    internal static class RaidJuiceTimePatch
    {
        private static void Postfix(ActiveHealthController.MedEffect __instance)
        {
            if (!__instance._interrupted &&
                JuiceTime.IsEligible(__instance.MedItem))
            {
                JuiceTime.ApplyInRaid(
                    __instance.HealthController,
                    __instance.MedItem);
            }
        }
    }

    [HarmonyPatch(
        typeof(OfflineHealthController.MedEffect),
        nameof(OfflineHealthController.MedEffect.Started))]
    internal static class StashJuiceTimePatch
    {
        private static void Postfix(OfflineHealthController.MedEffect __instance)
        {
            if (JuiceTime.IsEligible(__instance.MedItem))
            {
                JuiceTime.ApplyOutOfRaid(
                    __instance._health,
                    __instance.MedItem);
            }
        }
    }
}
