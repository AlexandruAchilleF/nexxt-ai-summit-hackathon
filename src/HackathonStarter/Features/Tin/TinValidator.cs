using HackathonStarter.Features.Targets;
using HackathonStarter.Services;

namespace HackathonStarter.Features.Tin;

public enum TinCheckSource { NotChecked, LocalRules, TinOnEuropaLive, TinOnEuropaReplay }

/// <summary>
/// Result of a TIN check. We only ever claim "format valid", never "TIN valid": neither the local rules nor TIN on Europa
/// confirm that the number exists (TIN on Europa returns only validStructure and validSyntax).
/// <see cref="FormatValid"/> means nothing when <see cref="Source"/> is NotChecked (no rule for that country yet): treat it
/// as unknown, never as a wrong format.
/// </summary>
public record TinCheck(string Country, string Normalized, bool FormatValid, TinCheckSource Source, string Note)
{
    /// <summary>Missing (no TIN) and Unsupported (no rule for the country) are never a wrong format.</summary>
    public TinFormatStatus Status =>
        Normalized.Length == 0 ? TinFormatStatus.Missing
        : Source == TinCheckSource.NotChecked ? TinFormatStatus.Unsupported
        : FormatValid ? TinFormatStatus.Valid
        : TinFormatStatus.Invalid;

    /// <summary>For the UI: always says where the answer came from, and "format valid", never "TIN valid".</summary>
    public string Label => Status switch
    {
        TinFormatStatus.Valid => "format valid" + Via,
        TinFormatStatus.Invalid => "format invalid" + Via,
        TinFormatStatus.Missing => Lang.Pick("TIN lipsă", "TIN missing"),
        _ => Lang.Pick($"fără regulă de format pentru {Country}", $"no format rule for {Country}"),
    };

    private string Via => Source switch
    {
        TinCheckSource.TinOnEuropaLive => Lang.Pick(" · verificat live la Comisia Europeană", " · verified live at the European Commission"),
        TinCheckSource.TinOnEuropaReplay => Lang.Pick(" · înregistrare TIN on Europa", " · TIN on Europa recording"),
        _ => Lang.Pick(" · reguli locale", " · local rules"),
    };
}

public enum TinFormatStatus { Valid, Invalid, Missing, Unsupported }

/// <summary>
/// Offline TIN format rules per country (OECD TIN sheets): synchronous and pure, so the rule engine can use it. Owner: R3.
/// </summary>
public interface ITinFormat
{
    TinCheck Check(string country, string tin);

    /// <summary>Does the country issue a TIN to all its residents (OECD sheet)? Null = unknown. Guide p. 37: a missing TIN is
    /// doubtful when the country issues one to everybody (CRS-RZ-3).</summary>
    bool? IssuesTinToAllResidents(string country);
}

/// <summary>Format first (<see cref="ITinFormat"/>), then TIN on Europa through TargetClient for EU states. Owner: R3.</summary>
public interface ITinValidator
{
    Task<TinCheck> CheckAsync(string country, string tin, CancellationToken ct = default);
}

/// <summary>
/// Local format rules, and for EU states the European Commission's answer (live, else recorded), which wins. Falls back to
/// the local rules when the service is down. Writes nothing to the audit log: the caller knows the case and the actor.
/// </summary>
public sealed class TinValidator(ITinFormat format, TinOnEuropaClient europa) : ITinValidator
{
    private const string CommissionSource = "TIN on Europa (European Commission)";

    public async Task<TinCheck> CheckAsync(string country, string tin, CancellationToken ct = default)
    {
        var local = format.Check(country, tin);
        if (local.Status == TinFormatStatus.Missing || !TinOnEuropaClient.EuCountries.Contains(local.Country)) return local;

        var (answer, source, error) = await europa.CheckAsync(local.Country, local.Normalized, ct);
        if (answer is null) return local with { Note = $"{local.Note}. TIN on Europa unavailable: {error}" };

        var valid = answer.ValidStructure && answer.ValidSyntax;
        var note = local.Source == TinCheckSource.LocalRules && local.FormatValid != valid
            ? $"{CommissionSource}. Local rules say format {(local.FormatValid ? "valid" : "invalid")}."
            : CommissionSource;
        return new(local.Country, local.Normalized, valid,
            source == TargetSource.Live ? TinCheckSource.TinOnEuropaLive : TinCheckSource.TinOnEuropaReplay, note);
    }
}

public static class TinSetup
{
    public static IServiceCollection AddTin(this IServiceCollection services)
    {
        services.AddSingleton<ITinFormat, TinFormat>();
        // TIN on Europa is optional: without TargetClient (or Targets:tin-on-europa) the local rules answer alone.
        services.AddSingleton(sp => new TinOnEuropaClient(sp.GetService<TargetClient>()));
        services.AddSingleton<ITinValidator, TinValidator>();
        return services;
    }
}
