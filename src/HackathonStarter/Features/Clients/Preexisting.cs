using HackathonStarter.Features.Rules;

namespace HackathonStarter.Features.Clients;

/// <summary>
/// Preexisting or new is computed, never stored, and depends on the regime (guide p. 20): CRS preexisting = held on
/// 31 Dec 2015, new = opened on or after 1 Jan 2016; FATCA preexisting = held on 30 Jun 2014. The same account can be new
/// for CRS and preexisting for FATCA.
/// </summary>
public static class Preexisting
{
    public static readonly DateOnly CrsCutoff = new(2015, 12, 31);
    public static readonly DateOnly FatcaCutoff = new(2014, 6, 30);

    public static bool For(Regime regime, DateOnly openedOn) => regime switch
    {
        Regime.Fatca => openedOn <= FatcaCutoff,
        _ => openedOn <= CrsCutoff,
    };
}
