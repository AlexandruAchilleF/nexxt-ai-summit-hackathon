namespace HackathonStarter.Features.Rules;

/// <summary>
/// Preexisting vs new is computed from the opening date, never stored, and separately per regime (guide p. 20):
/// CRS preexisting = held on 31.12.2015 (CPF Anexa 1, Sec. VIII.C pct. 11-12), FATCA preexisting = held on 30.06.2014
/// (IGA art. 1). An account opened between the two dates is new for FATCA and preexisting for CRS.
/// </summary>
public static class AccountStatus
{
    public static readonly DateOnly CrsCutoff = new(2015, 12, 31);
    public static readonly DateOnly FatcaCutoff = new(2014, 6, 30);

    /// <summary>IGA Anexa I, Sec. II: a preexisting account above USD 1,000,000 is a high value account.</summary>
    public const decimal FatcaLowerValueLimitUsd = 1_000_000m;

    public static bool IsPreexisting(Regime regime, DateOnly openedOn) => regime switch
    {
        Regime.Crs => openedOn <= CrsCutoff,
        Regime.Fatca => openedOn <= FatcaCutoff,
        _ => throw new ArgumentOutOfRangeException(nameof(regime), "Preexisting status is per regime: Crs or Fatca."),
    };

    public static bool IsCrsPreexisting(DateOnly openedOn) => IsPreexisting(Regime.Crs, openedOn);

    public static bool IsFatcaPreexisting(DateOnly openedOn) => IsPreexisting(Regime.Fatca, openedOn);

    // Upper bounds of USD per unit, so the conversion never makes an account look smaller than it is (policy, not a rate feed).
    private static readonly Dictionary<string, decimal> UsdPerUnitUpperBound = new()
    {
        ["USD"] = 1m, ["RON"] = 0.25m, ["EUR"] = 1.25m, ["GBP"] = 1.45m, ["CHF"] = 1.30m,
    };

    /// <summary>
    /// Balance in USD, rounded up by the fixed upper-bound rates above; null for a currency we have no bound for (then the
    /// account is never treated as lower value). Policy: the IGA uses the balance at 30.06.2014 / 31 December, we use the
    /// current one.
    /// </summary>
    public static decimal? UsdUpperBound(decimal balance, string? currency) =>
        UsdPerUnitUpperBound.TryGetValue((currency ?? "").Trim().ToUpperInvariant(), out var rate) ? balance * rate : null;
}
