using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using HarmonyLib;
using System.Collections.Generic;
using Comfort.Common;
using EFT.NetworkPackets;

namespace OperatorTraits
{
    internal static class DietUseIntent
    {
        private static readonly HashSet<string> UseAllItems =
            new HashSet<string>(System.StringComparer.Ordinal);

        internal static void Capture(Item item, float? amount)
        {
            if (!(item is FoodDrink foodDrink))
                return;

            if (!amount.HasValue)
                UseAllItems.Add(foodDrink.Id);
            else
                UseAllItems.Remove(foodDrink.Id);
        }

        internal static bool ConsumeUseAll(FoodDrinkComponent foodDrink)
        {
            return foodDrink != null && UseAllItems.Remove(foodDrink.Item.Id);
        }
    }

    [HarmonyPatch(
        typeof(PlayerHealthController),
        nameof(PlayerHealthController.ApplyItem),
        new[] { typeof(Item), typeof(OneAndList<EBodyPart>), typeof(float?) })]
    internal static class RaidDietIntentPatch
    {
        private static void Prefix(Item item, float? amount)
        {
            DietUseIntent.Capture(item, amount);
        }
    }

    [HarmonyPatch(
        typeof(OfflineHealthController),
        nameof(OfflineHealthController.ApplyItem),
        new[] { typeof(Item), typeof(OneAndList<EBodyPart>), typeof(float?) })]
    internal static class StashDietIntentPatch
    {
        private static void Prefix(Item item, float? amount)
        {
            DietUseIntent.Capture(item, amount);
        }
    }

    // Change resource drain after Started so nutrition is not reduced too.
    [HarmonyPatch(
        typeof(ActiveHealthController.MedEffect),
        nameof(ActiveHealthController.MedEffect.Started))]
    internal static class RaidDietPatch
    {
        private static void Prefix(
            ActiveHealthController.MedEffect __instance,
            out bool __state)
        {
            __state = false;
            if (!Plugin.HasTrait("diet") || __instance._foodDrink == null ||
                __instance._foodDrink.MaxResource <= 1f)
                return;

            __state = DietUseIntent.ConsumeUseAll(__instance._foodDrink);
            if (__state)
            {
                __instance._foodDrink.HpPercent +=
                    __instance._foodDrink.HpPercent;
                __instance.Amount *= 2f;
            }
        }

        private static void Postfix(
            ActiveHealthController.MedEffect __instance,
            bool __state)
        {
            if (!__state && Plugin.HasTrait("diet") &&
                __instance._foodDrink != null &&
                __instance._foodDrink.MaxResource > 1f)
            {
                __instance.Amount *= 0.5f;
            }
        }
    }

    // Pre-refund here or EFT can queue removal before the server correction.
    [HarmonyPatch(
        typeof(OfflineHealthController.MedEffect),
        nameof(OfflineHealthController.MedEffect.Started))]
    internal static class StashDietClientPatch
    {
        private static void Prefix(OfflineHealthController.MedEffect __instance)
        {
            if (!Plugin.HasTrait("diet") ||
                __instance._foodDrink == null ||
                __instance._foodDrink.MaxResource <= 1f)
                return;

            float requested = UnityEngine.Mathf.Round(
                __instance._foodDrink.MaxResource * __instance._amount);
            if (DietUseIntent.ConsumeUseAll(__instance._foodDrink))
            {
                __instance._foodDrink.HpPercent +=
                    __instance._foodDrink.HpPercent;
                __instance._amount *= 2f;
            }
            else
            {
                __instance._foodDrink.HpPercent += requested * 0.5f;
            }
        }
    }
}
