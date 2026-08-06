using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EFT.UI;
using HarmonyLib;

namespace OperatorTraits
{
    [HarmonyPatch]
    internal static class InventoryScreenShowPatch
    {
        private static IEnumerable<MethodBase> TargetMethods() =>
            AccessTools.GetDeclaredMethods(typeof(InventoryScreen))
                .Where(method => method.Name == "Show");

        private static void Prefix(InventoryScreen __instance)
        {
            if (!OperatorTraitsTab.TryAttach(__instance))
                Plugin.Log.LogWarning(
                    "Could not attach to Tarkov's inventory Tasks tab.");
        }
    }
}
