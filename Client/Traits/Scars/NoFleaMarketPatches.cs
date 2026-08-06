using EFT.UI;
using HarmonyLib;
using UnityEngine.UI;

namespace OperatorTraits
{
    // UI-only lock: never persist or mutate EFT ragfair availability.
    [HarmonyPatch(
        typeof(TradingScreen),
        nameof(TradingScreen.Show),
        new[] { typeof(TradingScreen.TradingScreenController) })]
    internal static class TradingScreenShowPatch
    {
        private const string LockMessage =
            "The Flea Market is unavailable while the No Flea Market trait is active. " +
            "Remove the trait (Operator Traits reset for 50 GP) to restore access.";

        private static void Postfix(TradingScreen __instance)
        {
            if (!Plugin.HasTrait("no-flea-market"))
                return;

            ApplyFleaLock(__instance, redirectIfSelected: true);
        }

        internal static void ApplyFleaLock(
            TradingScreen screen,
            bool redirectIfSelected)
        {
            if (screen == null)
                return;

            if (screen._ragfairToggle != null &&
                screen._ragfairToggle.SpawnedObject != null)
                screen._ragfairToggle.SpawnedObject.interactable = false;

            screen._ragfairToggle?.SetActive(active: false);
            if (screen._ragfairLockIcon != null)
                screen._ragfairLockIcon.SetActive(true);

            if (screen._tooltipArea != null)
                screen._tooltipArea.SetMessageText(LockMessage);

            if (!redirectIfSelected)
                return;

            bool fleaActive = false;
            if (screen._ragfairToggle != null &&
                screen._ragfairToggle.SpawnedObject != null)
                fleaActive = screen._ragfairToggle.SpawnedObject.IsToggled;

            if (!fleaActive)
                return;

            screen._ragfairToggle.ToggleSilently(show: false);
            if (screen._merchantsToggle == null ||
                screen._merchantsToggle.SpawnedObject == null)
                return;

            screen._merchantsToggle.SpawnedObject.isOn = true;
        }
    }

    [HarmonyPatch(typeof(TradingScreen), nameof(TradingScreen.UpdateRagfairAvailability))]
    internal static class TradingScreenRagfairAvailabilityPatch
    {
        private static void Postfix(TradingScreen __instance)
        {
            if (!Plugin.HasTrait("no-flea-market"))
                return;

            TradingScreenShowPatch.ApplyFleaLock(__instance, redirectIfSelected: false);
        }
    }
}
