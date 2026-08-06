using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace OperatorTraits.Server;

[Injectable]
public sealed record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.hysocs.operatortraits";
    public string Name { get; init; } = "Operator Traits";
    public string Author { get; init; } = "Hysocs";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("0.1.0");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; }
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "Apache-2.0";
}
