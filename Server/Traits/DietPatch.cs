using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.Controllers;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Health;

namespace OperatorTraits.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 1)]
public sealed class DietPatchLoader(OperatorTraitsStateStore stateStore) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        OffRaidDietPatch.StateStore = stateStore;
        new OffRaidDietPatch().Enable();
        return Task.CompletedTask;
    }
}

public sealed class OffRaidDietPatch : AbstractPatch
{
    internal static OperatorTraitsStateStore? StateStore { get; set; }

    protected override MethodBase GetTargetMethod() =>
        typeof(HealthController).GetMethod(nameof(HealthController.OffRaidEat))!;

    [PatchPrefix]
    public static void Prefix(
        PmcData pmcData,
        OffraidEatRequestData request,
        MongoId sessionID)
    {
        if (StateStore is null ||
            !StateStore.GetTraits(sessionID.ToString()).Contains(
                "diet", StringComparer.Ordinal))
            return;

        var item = pmcData.Inventory?.Items?.FirstOrDefault(candidate =>
            candidate.Id == request.Item);
        if (item?.Upd?.FoodDrink?.HpPercent is null)
            return;

        double current = item.Upd.FoodDrink.HpPercent.Value;
        if (request.Count >= current * 1.5d)
        {
            // Doubled amount is the client/server Use All protocol.
            item.Upd.FoodDrink.HpPercent += current;
        }
        else
        {
            item.Upd.FoodDrink.HpPercent += request.Count * 0.5d;
        }
    }
}
