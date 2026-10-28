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

    // ISO 3166-1 alpha-2, the 249 officially assigned codes, plus XK (Kosovo, the code the EU uses). Policy: what counts as
    // a real jurisdiction on a self-certification; never the TIN-format, OECD-sheet or ANAF tables, which are much smaller.
    private static readonly HashSet<string> Iso3166 =
    [
        "AD", "AE", "AF", "AG", "AI", "AL", "AM", "AO", "AQ", "AR", "AS", "AT", "AU", "AW", "AX", "AZ", "BA", "BB", "BD", "BE",
        "BF", "BG", "BH", "BI", "BJ", "BL", "BM", "BN", "BO", "BQ", "BR", "BS", "BT", "BV", "BW", "BY", "BZ", "CA", "CC", "CD",
        "CF", "CG", "CH", "CI", "CK", "CL", "CM", "CN", "CO", "CR", "CU", "CV", "CW", "CX", "CY", "CZ", "DE", "DJ", "DK", "DM",
        "DO", "DZ", "EC", "EE", "EG", "EH", "ER", "ES", "ET", "FI", "FJ", "FK", "FM", "FO", "FR", "GA", "GB", "GD", "GE", "GF",
        "GG", "GH", "GI", "GL", "GM", "GN", "GP", "GQ", "GR", "GS", "GT", "GU", "GW", "GY", "HK", "HM", "HN", "HR", "HT", "HU",
        "ID", "IE", "IL", "IM", "IN", "IO", "IQ", "IR", "IS", "IT", "JE", "JM", "JO", "JP", "KE", "KG", "KH", "KI", "KM", "KN",
        "KP", "KR", "KW", "KY", "KZ", "LA", "LB", "LC", "LI", "LK", "LR", "LS", "LT", "LU", "LV", "LY", "MA", "MC", "MD", "ME",
        "MF", "MG", "MH", "MK", "ML", "MM", "MN", "MO", "MP", "MQ", "MR", "MS", "MT", "MU", "MV", "MW", "MX", "MY", "MZ", "NA",
        "NC", "NE", "NF", "NG", "NI", "NL", "NO", "NP", "NR", "NU", "NZ", "OM", "PA", "PE", "PF", "PG", "PH", "PK", "PL", "PM",
        "PN", "PR", "PS", "PT", "PW", "PY", "QA", "RE", "RO", "RS", "RU", "RW", "SA", "SB", "SC", "SD", "SE", "SG", "SH", "SI",
        "SJ", "SK", "SL", "SM", "SN", "SO", "SR", "SS", "ST", "SV", "SX", "SY", "SZ", "TC", "TD", "TF", "TG", "TH", "TJ", "TK",
        "TL", "TM", "TN", "TO", "TR", "TT", "TV", "TW", "TZ", "UA", "UG", "UM", "US", "UY", "UZ", "VA", "VC", "VE", "VG", "VI",
        "VN", "VU", "WF", "WS", "YE", "YT", "ZA", "ZM", "ZW",
        "XK",
    ];

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

    /// <summary>A real country or territory code after <see cref="Normalize"/> ("de", "EL", "XK" yes; "", "XX", "UK" no).</summary>
    public static bool IsKnown(string? country) => Iso3166.Contains(Normalize(country));

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
