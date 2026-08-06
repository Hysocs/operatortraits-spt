using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using HarmonyLib;
using UnityEngine;

namespace OperatorTraits
{
    [HarmonyPatch(
        typeof(WorldInteractiveObject),
        nameof(WorldInteractiveObject.UnlockOperation))]
    internal static class SafecrackerPatch
    {
        private static void Prefix(
            KeyComponent key,
            Player player,
            out int __state)
        {
            __state = int.MinValue;
            if (!Plugin.HasTrait("safecracker") ||
                player == null ||
                !player.IsYourPlayer ||
                !(key?.Item is KeyMechanical) ||
                Random.value >= 0.2f)
                return;

            __state = key.NumberOfUsages;
            // UnlockOperation increments before checking whether the key is
            // exhausted. Offset it up front so a saved final use cannot queue
            // a discard operation, then restore the original count afterward.
            key.NumberOfUsages = __state - 1;
        }

        private static void Postfix(KeyComponent key, int __state)
        {
            if (__state == int.MinValue || key == null)
                return;

            key.NumberOfUsages = __state;
            key.Item.RaiseRefreshEvent();
            Plugin.Log.LogInfo(
                $"Safecracker preserved one use of {key.Item.ShortName}.");
        }
    }
}
