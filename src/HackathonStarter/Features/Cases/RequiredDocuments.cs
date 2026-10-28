using HackathonStarter.Features.Rules;

namespace HackathonStarter.Features.Cases;

/// <summary>
/// The documents a client must send, computed by code from the case (regime, account age, indicia), never by a model.
/// Stored as plain English sentences on the request; the UI translates them (Services/Lang.Ro.Cases.cs).
/// </summary>
public static class RequiredDocuments
{
    /// <summary>CRS new account: a valid self-certification OR a reasonable explanation with documents (guide p. 35).</summary>
    public const string SelfCertificationOrExplanation =
        "A new self-certification, or a reasonable explanation with supporting documents (if applicable)";
    /// <summary>CRS preexisting account: a self-certification AND new documentary evidence (guide p. 29).</summary>
    public const string SelfCertification = "A new self-certification";
    public const string DocumentaryEvidence = "New documentary evidence of residence";
    /// <summary>FATCA change of circumstances: a new valid self-certification (guide p. 38).</summary>
    public const string FatcaSelfCertification = "A new FATCA self-certification";
    public const string ProofOfAddress = "Proof of the new address";

    /// <summary>Indicia that come from an address (residence, mailing, in-care-of / hold mail).</summary>
    private static readonly string[] AddressRules =
        ["CRS-IND-ADDR", "CRS-IND-MAIL", "CRS-IND-CO", "FATCA-IND-ADDR", "FATCA-IND-CO"];

    /// <param name="preexisting">From the case (computed on the CRS cutoff when the case has CRS indicia).</param>
    /// <returns>Distinct, in a stable order: regime documents first, then the proof of address.</returns>
    public static IReadOnlyList<string> For(Regime regime, bool preexisting, IEnumerable<string> ruleIds)
    {
        var documents = new List<string>();
        if (regime is Regime.Crs or Regime.Both)
        {
            if (preexisting)
            {
                documents.Add(SelfCertification);
                documents.Add(DocumentaryEvidence);
            }
            else documents.Add(SelfCertificationOrExplanation);
        }
        if (regime is Regime.Fatca or Regime.Both) documents.Add(FatcaSelfCertification);
        if (ruleIds.Any(id => AddressRules.Contains(id, StringComparer.Ordinal))) documents.Add(ProofOfAddress);
        return documents.Distinct(StringComparer.Ordinal).ToList();
    }
}
