namespace HackathonStarter.Features.Tin;

public enum TinCheckSource { NotChecked, LocalRules, TinOnEuropaLive, TinOnEuropaReplay }

/// <summary>
/// Result of a TIN check. We only ever claim "format valid", never "TIN valid": neither the local rules nor TIN on Europa
/// confirm that the number exists (TIN on Europa returns only validStructure and validSyntax).
/// <see cref="FormatValid"/> means nothing when <see cref="Source"/> is NotChecked (no rule for that country yet): treat it
/// as unknown, never as a wrong format.
/// </summary>
public record TinCheck(string Country, string Normalized, bool FormatValid, TinCheckSource Source, string Note);

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

/// <summary>Phase 0 placeholder: nothing checked yet. R3 replaces it.</summary>
public sealed class TinFormat : ITinFormat
{
    public TinCheck Check(string country, string tin) =>
        new(country.ToUpperInvariant(), tin.Trim(), false, TinCheckSource.NotChecked, "TIN format rules not implemented yet.");

    public bool? IssuesTinToAllResidents(string country) => null;
}

/// <summary>Phase 0 placeholder: the format only (no live call yet). R3 adds TIN on Europa.</summary>
public sealed class TinValidator(ITinFormat format) : ITinValidator
{
    public Task<TinCheck> CheckAsync(string country, string tin, CancellationToken ct = default) =>
        Task.FromResult(format.Check(country, tin));
}

public static class TinSetup
{
    public static IServiceCollection AddTin(this IServiceCollection services)
    {
        services.AddSingleton<ITinFormat, TinFormat>();
        services.AddSingleton<ITinValidator, TinValidator>();
        return services;
    }
}
