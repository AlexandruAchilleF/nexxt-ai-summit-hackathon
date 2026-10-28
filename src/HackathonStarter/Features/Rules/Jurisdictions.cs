namespace HackathonStarter.Features.Rules;

/// <summary>
/// Which countries the indicia look for. CRS: the other EU member states (DAC2, CPF Anexa 1, Sec. VIII.D pct. 4) plus the
/// ANAF list of jurisdictions subject to CRS reporting for 2026 (1.a reciprocal, 1.b temporarily non-reciprocal; list 2,
/// permanently non-reciprocal, is not reportable). The US is not a CRS jurisdiction: it is covered by FATCA only.
/// </summary>
public static class Jurisdictions
{
    public const string Romania = "RO";
    public const string UnitedStates = "US";

    public static readonly IReadOnlySet<string> EuMemberStates = new HashSet<string>
    {
        "AT", "BE", "BG", "HR", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR", "HU", "IE",
        "IT", "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SK", "SI", "ES", "SE",
    };

    // ANAF, "Lista jurisdicțiilor care fac obiectul raportării CRS", 2026, list 1.a (67 jurisdictions).
    private static readonly HashSet<string> AnafReciprocal =
    [
        "ZA", "AL", "AD", "SA", "AR", "AM", "AW", "AU", "AZ", "BB", "BZ", "BQ", "BR", "CA", "CL", "CN", "CO", "KR",
        "CR", "CW", "EC", "CH", "GE", "GH", "GI", "GL", "GG", "HK", "IN", "ID", "IM", "CK", "FO", "IS", "IL", "JM",
        "JP", "JE", "KZ", "KE", "LI", "MY", "MV", "MU", "MX", "MD", "MC", "NG", "NO", "NZ", "PK", "PA", "PE", "GB",
        "RU", "RW", "KN", "SM", "SN", "SC", "LC", "SG", "TH", "TR", "UA", "UG", "UY",
    ];

    // List 1.b (13): they send data but receive none yet; banks still collect and report to ANAF.
    private static readonly HashSet<string> AnafTemporarilyNonReciprocal =
        ["BN", "DM", "GD", "KW", "LB", "MO", "MS", "NU", "OM", "WS", "SX", "TT", "VU"];

    // List 2 (13): permanently non-reciprocal, not reported. Kept to explain why an address there is not an indicium.
    private static readonly HashSet<string> AnafNotReported =
        ["AI", "AG", "BS", "BH", "AE", "KY", "MH", "TC", "VG", "NR", "NC", "QA", "VC"];

    // OECD TIN sheets: does the jurisdiction issue a TIN to every resident individual? Only the sheets we read; null = unknown.
    private static readonly Dictionary<string, bool> IssuesTinToAllIndividuals = new()
    {
        ["AT"] = false, ["DE"] = true, ["ES"] = true, ["IT"] = true, ["RO"] = true,
    };

    // Names for the explanations; any other code is shown as is.
    private static readonly Dictionary<string, (string Ro, string En)> Names = new()
    {
        ["RO"] = ("România", "Romania"), ["DE"] = ("Germania", "Germany"), ["AT"] = ("Austria", "Austria"),
        ["IT"] = ("Italia", "Italy"), ["ES"] = ("Spania", "Spain"), ["FR"] = ("Franța", "France"),
        ["HU"] = ("Ungaria", "Hungary"), ["BG"] = ("Bulgaria", "Bulgaria"), ["NL"] = ("Țările de Jos", "the Netherlands"),
        ["GB"] = ("Regatul Unit", "the United Kingdom"), ["CH"] = ("Elveția", "Switzerland"),
        ["MD"] = ("Republica Moldova", "Moldova"), ["AE"] = ("Emiratele Arabe Unite", "the United Arab Emirates"),
        ["US"] = ("SUA", "the US"),
    };

    /// <summary>"DE" → "Germania (DE)" / "Germany (DE)"; unknown codes stay as they are.</summary>
    public static string Display(string? country, bool en = false)
    {
        var code = Normalize(country);
        return Names.TryGetValue(code, out var name) ? $"{(en ? name.En : name.Ro)} ({code})" : code;
    }

    /// <summary>"de", " DE " → "DE"; the EU code "EL" (Greece) → "GR" and the common "UK" → "GB" (ISO 3166); null → "".</summary>
    public static string Normalize(string? country)
    {
        var code = (country ?? "").Trim().ToUpperInvariant();
        return code switch
        {
            "EL" => "GR",
            "UK" => "GB",
            _ => code,
        };
    }

    public static bool IsRomania(string? country) => Normalize(country) == Romania;

    public static bool IsUnitedStates(string? country) => Normalize(country) == UnitedStates;

    /// <summary>A jurisdiction whose residents a Romanian bank reports under CRS / DAC2.</summary>
    public static bool IsCrsReportable(string? country)
    {
        var code = Normalize(country);
        return code != Romania && (EuMemberStates.Contains(code) || AnafReciprocal.Contains(code)
            || AnafTemporarilyNonReciprocal.Contains(code));
    }

    public static bool IsAnafNotReported(string? country) => AnafNotReported.Contains(Normalize(country));

    /// <summary>true / false from the OECD TIN sheet; null when we have not read the sheet for that jurisdiction.</summary>
    public static bool? IssuesTinToAll(string? country) =>
        IssuesTinToAllIndividuals.TryGetValue(Normalize(country), out var all) ? all : null;

    /// <summary>Why a foreign country is not a CRS jurisdiction (for the Cleared explanations), in RO and EN.</summary>
    public static (string Ro, string En) WhyNotCrsReportable(string? country)
    {
        var code = Normalize(country);
        if (code == UnitedStates)
            return ("SUA nu e jurisdicție CRS raportabilă (SUA intră doar la FATCA)",
                "the US is not a CRS reportable jurisdiction (it is covered by FATCA only)");
        if (AnafNotReported.Contains(code))
            return ($"{code} e pe lista ANAF a jurisdicțiilor care nu fac obiectul raportării CRS",
                $"{code} is on the ANAF list of jurisdictions not subject to CRS reporting");
        return ($"{code} nu e pe lista jurisdicțiilor CRS raportabile", $"{code} is not on the list of CRS reportable jurisdictions");
    }
}
