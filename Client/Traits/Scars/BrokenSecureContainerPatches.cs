using System;
using System.Collections.Generic;
using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using OperatorTraits.Shared;

namespace OperatorTraits
{
    // Keep this event-based: mutating template filters would not reverse safely.
    [HarmonyPatch(typeof(Grid), nameof(Grid.CheckCompatibility))]
    internal static class BrokenSecureContainerCompatibilityPatch
    {
        private static readonly MongoID[] AllowedBaseClasses =
        {
            new MongoID("543be5dd4bdc2deb348b4569"), // Money
            new MongoID("543be5e94bdc2df1348b4568"), // Keys
            new MongoID("5447e0e74bdc2d3c308b4567")  // Special equipment
        };

        private static void Postfix(
            Grid __instance,
            Item item,
            ref bool __result)
        {
            if (!__result || !Plugin.HasTrait("broken-secure-container") ||
                !IsSecureContainerGrid(__instance))
                return;

            __result = IsAllowedTree(item);
        }

        private static bool IsSecureContainerGrid(Grid grid)
        {
            Item current = grid.ParentItem;
            var visited = new HashSet<Item>();
            while (current != null && visited.Add(current))
            {
                ItemAddress address = current.CurrentAddress;
                if (address?.Container != null &&
                    string.Equals(
                        address.Container.ID,
                        "SecuredContainer",
                        StringComparison.Ordinal))
                    return true;
                current = address?.Container?.ParentItem;
            }
            return false;
        }

        private static bool IsAllowedTree(Item item)
        {
            if (!IsAllowed(item))
                return false;

            if (!(item is CompoundItem compoundItem))
                return true;

            foreach (IContainer container in compoundItem.Containers)
            foreach (Item child in container.Items)
                if (!IsAllowedTree(child))
                    return false;
            return true;
        }

        private static bool IsAllowed(Item item) =>
            BrokenSecureContainerRules.IsExplicitlyAllowed(
                item.StringTemplateId) ||
            ItemFilter.CheckItem(item, AllowedBaseClasses);
    }
}
