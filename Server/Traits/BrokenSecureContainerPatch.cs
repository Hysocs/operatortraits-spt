using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Eft.Inventory;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;
using OperatorTraits.Shared;
using Item = SPTarkov.Server.Core.Models.Eft.Common.Tables.Item;

namespace OperatorTraits.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 1)]
public sealed class BrokenSecureContainerPatchLoader(
    OperatorTraitsStateStore stateStore,
    ItemHelper itemHelper,
    HttpResponseUtil httpResponseUtil,
    EventOutputHolder eventOutputHolder) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        BrokenSecureContainerGuard.StateStore = stateStore;
        BrokenSecureContainerGuard.ItemHelper = itemHelper;
        BrokenSecureContainerGuard.HttpResponseUtil = httpResponseUtil;
        BrokenSecureContainerGuard.EventOutputHolder = eventOutputHolder;

        new BrokenSecureContainerMovePatch().Enable();
        new BrokenSecureContainerSwapPatch().Enable();
        new BrokenSecureContainerSplitPatch().Enable();
        return Task.CompletedTask;
    }
}

internal static class BrokenSecureContainerGuard
{
    private const string TraitId = "broken-secure-container";
    private const string SecureContainerSlot = "SecuredContainer";
    private const string RejectionMessage =
        "Broken Secure Container only accepts cash, keys, dogtags, " +
        "special equipment, and compact utility containers.";

    internal static OperatorTraitsStateStore? StateStore { get; set; }
    internal static ItemHelper? ItemHelper { get; set; }
    internal static HttpResponseUtil? HttpResponseUtil { get; set; }
    internal static EventOutputHolder? EventOutputHolder { get; set; }

    internal static bool ShouldBlock(
        PmcData profile,
        MongoId sessionId,
        MongoId? itemId,
        string? destinationParentId)
    {
        if (StateStore is null || ItemHelper is null || itemId is null ||
            string.IsNullOrEmpty(destinationParentId) ||
            !StateStore.GetTraits(sessionId.ToString()).Contains(
                TraitId, StringComparer.Ordinal))
            return false;

        List<Item> items = profile.Inventory?.Items ?? [];
        if (!IsInsideSecureContainer(items, destinationParentId))
            return false;

        Item? root = items.FirstOrDefault(item => item.Id == itemId);
        if (root is null)
            return false;

        HashSet<MongoId> movingIds = [root.Id];
        bool addedChild;
        do
        {
            addedChild = false;
            foreach (Item item in items)
            {
                if (item.ParentId is null ||
                    !movingIds.Contains(new MongoId(item.ParentId)) ||
                    !movingIds.Add(item.Id))
                    continue;
                addedChild = true;
            }
        } while (addedChild);

        return items.Any(item =>
            movingIds.Contains(item.Id) && !IsAllowed(item.Template));
    }

    internal static void Reject(ItemEventRouterResponse output) =>
        HttpResponseUtil?.AppendErrorToOutput(output, RejectionMessage);

    internal static ItemEventRouterResponse Reject(MongoId sessionId)
    {
        ItemEventRouterResponse output = EventOutputHolder!.GetOutput(sessionId);
        Reject(output);
        return output;
    }

    private static bool IsAllowed(MongoId templateId) =>
        BrokenSecureContainerRules.IsExplicitlyAllowed(
            templateId.ToString()) ||
        ItemHelper!.IsOfBaseclasses(
            templateId,
            [BaseClasses.MONEY, BaseClasses.KEY, BaseClasses.SPEC_ITEM]);

    private static bool IsInsideSecureContainer(
        IReadOnlyCollection<Item> items,
        string destinationParentId)
    {
        Item? secureContainer = items.FirstOrDefault(item =>
            string.Equals(
                item.SlotId,
                SecureContainerSlot,
                StringComparison.Ordinal));
        if (secureContainer is null)
            return false;

        string? currentId = destinationParentId;
        HashSet<string> visited = new(StringComparer.Ordinal);
        while (!string.IsNullOrEmpty(currentId) && visited.Add(currentId))
        {
            if (currentId == secureContainer.Id.ToString())
                return true;
            currentId = items.FirstOrDefault(item =>
                item.Id.ToString() == currentId)?.ParentId;
        }
        return false;
    }
}

public sealed class BrokenSecureContainerMovePatch : AbstractPatch
{
    protected override MethodBase? GetTargetMethod() =>
        typeof(InventoryController).GetMethod(
            nameof(InventoryController.MoveItem),
            [typeof(PmcData), typeof(InventoryMoveRequestData),
             typeof(MongoId), typeof(ItemEventRouterResponse)]);

    [PatchPrefix]
    public static bool Prefix(
        PmcData pmcData,
        InventoryMoveRequestData moveRequest,
        MongoId sessionId,
        ItemEventRouterResponse output)
    {
        if (!BrokenSecureContainerGuard.ShouldBlock(
                pmcData, sessionId, moveRequest.Item, moveRequest.To?.Id))
            return true;

        BrokenSecureContainerGuard.Reject(output);
        return false;
    }
}

public sealed class BrokenSecureContainerSwapPatch : AbstractPatch
{
    protected override MethodBase? GetTargetMethod() =>
        typeof(InventoryController).GetMethod(
            nameof(InventoryController.SwapItem),
            [typeof(PmcData), typeof(InventorySwapRequestData), typeof(MongoId)]);

    [PatchPrefix]
    public static bool Prefix(
        PmcData pmcData,
        InventorySwapRequestData request,
        MongoId sessionId,
        ref ItemEventRouterResponse __result)
    {
        bool blocked = BrokenSecureContainerGuard.ShouldBlock(
                           pmcData, sessionId, request.Item, request.To?.Id) ||
                       BrokenSecureContainerGuard.ShouldBlock(
                           pmcData, sessionId, request.Item2, request.To2?.Id);
        if (!blocked)
            return true;

        __result = BrokenSecureContainerGuard.Reject(sessionId);
        return false;
    }
}

public sealed class BrokenSecureContainerSplitPatch : AbstractPatch
{
    protected override MethodBase? GetTargetMethod() =>
        typeof(InventoryController).GetMethod(
            nameof(InventoryController.SplitItem),
            [typeof(PmcData), typeof(InventorySplitRequestData),
             typeof(MongoId), typeof(ItemEventRouterResponse)]);

    [PatchPrefix]
    public static bool Prefix(
        PmcData pmcData,
        InventorySplitRequestData request,
        MongoId sessionID,
        ItemEventRouterResponse output)
    {
        if (!BrokenSecureContainerGuard.ShouldBlock(
                pmcData, sessionID, request.SplitItem, request.Container?.Id))
            return true;

        BrokenSecureContainerGuard.Reject(output);
        return false;
    }
}
