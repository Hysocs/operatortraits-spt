using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using Item = SPTarkov.Server.Core.Models.Eft.Common.Tables.Item;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SPTarkov.Server.Core.Services;
using SPTarkov.Server.Core.Services.Commerce;
using SPTarkov.Server.Core.Utils;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace OperatorTraits.Server;

[Injectable]
public sealed class OperatorTraitsCallbacks(
    ProfileHelper profileHelper,
    SaveServer saveServer,
    ItemHelper itemHelper,
    MailSendService mailSendService,
    HttpResponseUtil httpResponseUtil,
    OperatorTraitsStateStore stateStore)
{
    private const string GpCoinTemplateId = "5d235b4d86f7742e017bc88a";
    private const string RoubleTemplateId = "5449016a4bdc2d6f028b456f";
    private const string DollarTemplateId = "5696686a4bdc2da3298b456a";
    private const string EuroTemplateId = "569668774bdc2da2298b4568";
    private readonly object _streetTaxSync = new();

    public async ValueTask<string> ResetTraits(
        ResetPaymentRequest request,
        MongoId sessionId)
    {
        var profile = profileHelper.GetPmcProfile(sessionId);
        var item = profile?.Inventory?.Items?.FirstOrDefault(candidate =>
            candidate.Id.ToString() == request.ItemId);
        var count = item?.Upd?.StackObjectsCount ?? 0;

        if (item is null || item.Template.ToString() != GpCoinTemplateId ||
            request.Amount != 50 || count < request.Amount)
        {
            return httpResponseUtil.NoBody(new ResetPaymentResponse(
                false, request.ItemId, (int)count,
                "The requested stash GP stack cannot cover the reset."));
        }

        item.Upd!.StackObjectsCount = count - request.Amount;
        stateStore.SetTraits(sessionId.ToString(), []);
        await saveServer.SaveProfileAsync(sessionId);
        return httpResponseUtil.NoBody(new ResetPaymentResponse(
            true, request.ItemId, (int)item.Upd.StackObjectsCount.Value, null));
    }

    public ValueTask<string> SaveTraits(
        SaveTraitsRequest request,
        MongoId sessionId)
    {
        var traits = request.Traits
            .Where(trait => !string.IsNullOrWhiteSpace(trait))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        stateStore.SetTraits(sessionId.ToString(), traits);
        return ValueTask.FromResult(httpResponseUtil.NoBody(
            new TraitsStateResponse(
                true,
                traits,
                stateStore.GetAllergies(sessionId.ToString()),
                null)));
    }

    public ValueTask<string> LoadTraits(MongoId sessionId)
    {
        TryPayStreetTax(sessionId, false);
        return ValueTask.FromResult(httpResponseUtil.NoBody(
            new TraitsStateResponse(
                true,
                stateStore.GetTraits(sessionId.ToString()),
                stateStore.GetAllergies(sessionId.ToString()),
                null)));
    }

    public ValueTask<string> SimulateStreetTax(MongoId sessionId)
    {
        StreetTaxPayment? payment = TryPayStreetTax(sessionId, true);
        return ValueTask.FromResult(httpResponseUtil.NoBody(
            new StreetTaxResponse(
                payment is not null,
                payment?.Roubles ?? 0,
                payment?.Dollars ?? 0,
                payment?.Euros ?? 0,
                payment is not null
                    ? "A simulated Street Tax payment was sent to your messages."
                    : "Street Tax is disabled or a currency template could not be loaded.")));
    }

    private StreetTaxPayment? TryPayStreetTax(MongoId sessionId, bool simulate)
    {
        lock (_streetTaxSync)
        {
            string profileId = sessionId.ToString();
            StreetTaxConfig config = stateStore.GetStreetTaxConfig();
            if (!config.Enabled)
                return null;

            DateOnly currentWeek = GetMonday(DateTime.Now.Date);
            if (!simulate && !stateStore.CanPayStreetTax(profileId, currentWeek))
                return null;

            Item? roubles = CreateCurrencyItem(
                RoubleTemplateId,
                Roll(config.MinimumRoubles, config.MaximumRoubles));
            Item? dollars = CreateCurrencyItem(
                DollarTemplateId,
                Roll(config.MinimumDollars, config.MaximumDollars));
            Item? euros = CreateCurrencyItem(
                EuroTemplateId,
                Roll(config.MinimumEuros, config.MaximumEuros));
            if (roubles is null || dollars is null || euros is null)
                return null;

            mailSendService.SendSystemMessageToPlayer(
                sessionId,
                simulate
                    ? "Street Tax test: the local Scavs paid their share."
                    : "The local Scavs paid their share for the week.",
                [roubles, dollars, euros],
                Math.Max(60, config.MailLifetimeSeconds),
                null);

            if (!simulate)
                stateStore.MarkStreetTaxPaid(profileId, currentWeek);
            return new StreetTaxPayment(
                (int)roubles.Upd!.StackObjectsCount!.Value,
                (int)dollars.Upd!.StackObjectsCount!.Value,
                (int)euros.Upd!.StackObjectsCount!.Value);
        }
    }

    private Item? CreateCurrencyItem(string templateId, int amount)
    {
        MongoId id = new(templateId);
        var templateResult = itemHelper.GetItem(id);
        if (!templateResult.Key || templateResult.Value is null)
            return null;

        var item = new Item
        {
            Id = new MongoId(),
            Template = id,
            Upd = itemHelper.GenerateUpdForItem(templateResult.Value)
        };
        item.Upd.StackObjectsCount = amount;
        return item;
    }

    private static int Roll(int configuredMinimum, int configuredMaximum)
    {
        int minimum = Math.Clamp(configuredMinimum, 1, int.MaxValue - 1);
        int maximum = Math.Clamp(configuredMaximum, minimum, int.MaxValue - 1);
        return Random.Shared.Next(minimum, maximum + 1);
    }

    private static DateOnly GetMonday(DateTime date)
    {
        int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return DateOnly.FromDateTime(date.AddDays(-daysSinceMonday));
    }
}

[Injectable(InjectionType.Singleton)]
public sealed class OperatorTraitsStateStore
{
    private static readonly string StatePath = Path.Combine(
        Path.GetDirectoryName(typeof(OperatorTraitsStateStore).Assembly.Location)
            ?? AppContext.BaseDirectory,
        "data",
        "traits.json");
    private readonly object _sync = new();

    public OperatorTraitsStateStore()
    {
        lock (_sync)
        {
            if (!File.Exists(StatePath))
                Write(new TraitStateFile());
        }
    }

    public List<string> GetTraits(string profileId)
    {
        lock (_sync)
        {
            TraitStateFile state = Read();
            return state.Profiles.TryGetValue(profileId, out var profile)
                ? [.. profile.Traits]
                : [];
        }
    }

    public void SetTraits(string profileId, List<string> traits)
    {
        lock (_sync)
        {
            TraitStateFile state = Read();
            ProfileTraitState profile = GetOrCreateProfile(state, profileId);
            bool addingStreetTax = traits.Contains("street-tax", StringComparer.Ordinal) &&
                                   !profile.Traits.Contains("street-tax", StringComparer.Ordinal);
            bool hasAllergic = traits.Contains("allergic", StringComparer.Ordinal);
            profile.Traits = [.. traits];
            if (addingStreetTax && profile.StreetTax.NextEligibleWeek is null)
            {
                DateTime today = DateTime.Now.Date;
                int daysUntilMonday = (8 - (int)today.DayOfWeek) % 7;
                if (daysUntilMonday == 0)
                    daysUntilMonday = 7;
                profile.StreetTax.NextEligibleWeek =
                    DateOnly.FromDateTime(today.AddDays(daysUntilMonday)).ToString("yyyy-MM-dd");
            }

            if (hasAllergic && profile.Allergies.Count != 3)
            {
                var pool = AllergenPool.Items.ToList();
                for (int i = pool.Count - 1; i > 0; i--)
                {
                    int j = Random.Shared.Next(i + 1);
                    (pool[i], pool[j]) = (pool[j], pool[i]);
                }
                profile.Allergies = [.. pool.Take(3)];
            }
            else if (!hasAllergic)
            {
                profile.Allergies = [];
            }

            Write(state);
        }
    }

    public List<string> GetAllergies(string profileId)
    {
        lock (_sync)
        {
            TraitStateFile state = Read();
            return state.Profiles.TryGetValue(profileId, out var profile)
                ? [.. profile.Allergies]
                : [];
        }
    }

    public StreetTaxConfig GetStreetTaxConfig()
    {
        lock (_sync)
            return Read().Configuration.StreetTax;
    }

    public bool CanPayStreetTax(string profileId, DateOnly currentWeek)
    {
        lock (_sync)
        {
            TraitStateFile state = Read();
            if (!state.Profiles.TryGetValue(profileId, out ProfileTraitState? profile) ||
                !profile.Traits.Contains("street-tax", StringComparer.Ordinal))
                return false;

            string week = currentWeek.ToString("yyyy-MM-dd");
            return profile.StreetTax.LastPaidWeek != week &&
                   (profile.StreetTax.NextEligibleWeek is null ||
                    string.CompareOrdinal(week, profile.StreetTax.NextEligibleWeek) >= 0);
        }
    }

    public void MarkStreetTaxPaid(string profileId, DateOnly currentWeek)
    {
        lock (_sync)
        {
            TraitStateFile state = Read();
            ProfileTraitState profile = GetOrCreateProfile(state, profileId);
            profile.StreetTax.LastPaidWeek = currentWeek.ToString("yyyy-MM-dd");
            Write(state);
        }
    }

    private static ProfileTraitState GetOrCreateProfile(TraitStateFile state, string profileId)
    {
        if (!state.Profiles.TryGetValue(profileId, out ProfileTraitState? profile))
        {
            profile = new ProfileTraitState();
            state.Profiles[profileId] = profile;
        }
        return profile;
    }

    private static void Write(TraitStateFile state)
    {
        string? directory = Path.GetDirectoryName(StatePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(
            StatePath,
            System.Text.Json.JsonSerializer.Serialize(
                state,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }

    private static TraitStateFile Read()
    {
        if (!File.Exists(StatePath))
            return new TraitStateFile();

        try
        {
            string json = File.ReadAllText(StatePath);
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<TraitStateFile>(json) ??
                       new TraitStateFile();
            }
            catch (System.Text.Json.JsonException)
            {
                var legacy = System.Text.Json.JsonSerializer.Deserialize<LegacyTraitStateFile>(json);
                var migrated = new TraitStateFile();
                if (legacy is not null)
                    foreach (var entry in legacy.Profiles)
                        migrated.Profiles[entry.Key] = new ProfileTraitState { Traits = entry.Value };
                Write(migrated);
                return migrated;
            }
        }
        catch
        {
            return new TraitStateFile();
        }
    }
}

public sealed record TraitStateFile
{
    public OperatorTraitsConfiguration Configuration { get; init; } = new();
    public Dictionary<string, ProfileTraitState> Profiles { get; init; } = [];
}

public sealed record OperatorTraitsConfiguration
{
    public StreetTaxConfig StreetTax { get; init; } = new();
}

public sealed record StreetTaxConfig
{
    public bool Enabled { get; init; } = true;
    public int MinimumRoubles { get; init; } = 150000;
    public int MaximumRoubles { get; init; } = 400000;
    public int MinimumDollars { get; init; } = 1000;
    public int MaximumDollars { get; init; } = 1500;
    public int MinimumEuros { get; init; } = 1000;
    public int MaximumEuros { get; init; } = 1500;
    public int MailLifetimeSeconds { get; init; } = 604800;
}

public sealed class ProfileTraitState
{
    public List<string> Traits { get; set; } = [];
    public List<string> Allergies { get; set; } = [];
    public StreetTaxPaymentState StreetTax { get; init; } = new();
}

public static class AllergenPool
{
    public static readonly IReadOnlyList<string> Items = new[]
    {
        "5448fee04bdc2dbc018b4567", "5448ff904bdc2d6f028b456e", "544fb37f4bdc2dee738b4567", "544fb3f34bdc2d03748b456a",
        "544fb62a4bdc2dfb738b4568", "544fb6cc4bdc2d34748b456e", "5673de654bdc2d180f8b456d", "5734773724597737fd047c14",
        "57347d3d245977448f7b7f61", "57347d5f245977448b40fa81", "57347d692459774491567cf1", "57347d7224597744596b4e72",
        "57347d8724597744596b4e76", "57347d90245977448f7b7f65", "57347d9c245977448b40fa85", "57347da92459774491567cf5",
        "57505f6224597709a92585a9", "575062b524597720a31c09a1", "57513f07245977207e26a311", "57513f9324597720a7128161",
        "57513fcc24597720a31c09a6", "5751435d24597720a27126d1", "57514643245977207f2c2d09", "575146b724597720a27126d5",
        "5751487e245977207e26a315", "5751496424597720a27126da", "5751a89d24597722aa0e8db0", "5755383e24597772cb798966",
        "590c5d4b86f774784e1b9c45", "590c5f0d86f77413997acfab", "590c695186f7741e566b64a2", "59e3577886f774176a362503",
        "5af0548586f7743a532b7e99", "5bc9b156d4351e00367fbce9", "5bc9c29cd4351e003562b8a3", "5c0e530286f7747fa1419862",
        "5c0e531286f7747fa54205c2", "5c0e531d86f7747fa23f4d42", "5c0e533786f7747fa23f4d47", "5c0e534186f7747fa1419867",
        "5c0fa877d174af02a012e1cf", "5c10c8fd86f7743d7d706df3", "5d1b33a686f7742523398398", "5d1b376e86f774252519444e",
        "5d403f9186f7743cac3f229b", "5d40407c86f774318526545a", "5e8f3423fd7471236e6e3b64", "5ed515c8d380ab312177c0fa",
        "5ed515e03a40a50460332579", "5ed515ece452db0eb56fc028", "5ed515f6915ec335206e4152", "5ed5160a87bb8443d10680b5",
        "5ed51652f6c34d2cc26336a1", "5ed5166ad380ab312177c100", "5fca138c2a7b221b2852a5c6", "5fca13ca637ee0341a484f46",
        "60098b1705871270cd5352a1", "60b0f93284c20f0feb453da7", "62a09f32621468534a797acb", "635a758bfefc88a93f021b8a",
        "637b60c3b7afa97bfc3d7001", "637b612fb7afa97bfc3d7005", "637b6179104668754b72f8f5", "637b620db7afa97bfc3d7009",
        "637b6251104668754b72f8f9", "656df4fec921ad01000481a2", "65815f0e647e3d7246384e14", "66507eabf5ddb0818b085b68"
    };
}

public sealed class StreetTaxPaymentState
{
    public string? LastPaidWeek { get; set; }
    public string? NextEligibleWeek { get; set; }
}

public sealed record LegacyTraitStateFile
{
    public Dictionary<string, List<string>> Profiles { get; init; } = [];
}

public sealed record ResetPaymentRequest : IRequestData
{
    public string ItemId { get; init; } = string.Empty;
    public int Amount { get; init; }
}

public sealed record ResetPaymentResponse(
    bool Success,
    string ItemId,
    int NewCount,
    string? Error);

public sealed record SaveTraitsRequest : IRequestData
{
    public List<string> Traits { get; init; } = [];
}

public sealed record EmptyTraitsRequest : IRequestData;

public sealed record TraitsStateResponse(
    bool Success,
    List<string> Traits,
    List<string> Allergies,
    string? Error);

public sealed record StreetTaxPayment(int Roubles, int Dollars, int Euros);

public sealed record StreetTaxResponse(
    bool Success,
    int Roubles,
    int Dollars,
    int Euros,
    string Message);
