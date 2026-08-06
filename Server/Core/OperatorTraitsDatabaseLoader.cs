using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Servers;

namespace OperatorTraits.Server;

[Injectable(TypePriority = OnLoadOrder.PostLoad - 1)]
public sealed class OperatorTraitsDatabaseLoader(GlobalTable globals) : IOnLoad
{
    public const string SailorsNostalgiaBuff =
        "BuffsOperatorTraitsSailorsNostalgia";

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        globals.Configuration.Health.Effects.Stimulator.Buffs[
            SailorsNostalgiaBuff] =
            [
                new Buff
                {
                    BuffType = "HealthRate",
                    Chance = 1,
                    Delay = 0,
                    Duration = 30,
                    Value = 2,
                    AbsoluteValue = true,
                    SkillName = string.Empty,
                    AppliesTo = []
                }
            ];
        return Task.CompletedTask;
    }
}
