using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;

namespace OperatorTraits.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 1)]
public sealed class ThirdLegPatchLoader(
    OperatorTraitsStateStore stateStore,
    ItemHelper itemHelper) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        TherapistThirdLegDiscountPatch.StateStore = stateStore;
        TherapistThirdLegDiscountPatch.ItemHelper = itemHelper;
        new TherapistThirdLegDiscountPatch().Enable();
        return Task.CompletedTask;
    }
}

public sealed class TherapistThirdLegDiscountPatch : AbstractPatch
{
    private const double PriceMultiplier = 0.95d;

    internal static OperatorTraitsStateStore? StateStore { get; set; }
    internal static ItemHelper? ItemHelper { get; set; }

    protected override MethodBase? GetTargetMethod() =>
        typeof(TraderAssortHelper).GetMethod(
            nameof(TraderAssortHelper.GetAssort),
            [typeof(MongoId), typeof(MongoId), typeof(bool)]);

    [PatchPostfix]
    public static void Postfix(
        MongoId sessionId,
        MongoId traderId,
        TraderAssort __result)
    {
        if (StateStore is null || ItemHelper is null || __result is null ||
            traderId != Traders.THERAPIST ||
            !StateStore.GetTraits(sessionId.ToString()).Contains(
                "third-leg", StringComparer.Ordinal))
            return;

        foreach (List<List<BarterScheme>> alternatives in
                 __result.BarterScheme.Values)
        foreach (List<BarterScheme> requirements in alternatives)
        foreach (BarterScheme requirement in requirements)
        {
            if (requirement.Count is null ||
                !ItemHelper.IsOfBaseclass(requirement.Template, BaseClasses.MONEY))
                continue;

            requirement.Count = Math.Max(
                1d,
                Math.Ceiling(requirement.Count.Value * PriceMultiplier));
        }
    }
}
