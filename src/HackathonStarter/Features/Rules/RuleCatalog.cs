namespace HackathonStarter.Features.Rules;

/// <summary>Rule IDs (final, docs/PLAN.md 8.2). Cases, the audit log and the agent refer to rules only by these.</summary>
public static class RuleIds
{
    public const string Domestic = "GEN-DOMESTIC";
    public const string ChangeOfCircumstances = "GEN-CHANGE-CIRC";
    public const string NewAccountIndicia = "GEN-NEW-ACCOUNT";

    public const string CrsResidence = "CRS-IND-RES";
    public const string CrsAddress = "CRS-IND-ADDR";
    public const string CrsMailing = "CRS-IND-MAIL";
    public const string CrsPhone = "CRS-IND-PHONE";
    public const string CrsStandingOrder = "CRS-IND-SO";
    public const string CrsPowerOfAttorney = "CRS-IND-POA";
    public const string CrsInCareOf = "CRS-IND-CO";

    public const string FatcaCitizenship = "FATCA-IND-CIT";
    public const string FatcaBirthPlace = "FATCA-IND-POB";
    public const string FatcaAddress = "FATCA-IND-ADDR";
    public const string FatcaPhone = "FATCA-IND-PHONE";
    public const string FatcaStandingOrder = "FATCA-IND-SO";
    public const string FatcaPowerOfAttorney = "FATCA-IND-POA";
    public const string FatcaInCareOf = "FATCA-IND-CO";
    public const string FatcaChange = "FATCA-SC";
    public const string FatcaScopeAllAccounts = "FATCA-SCOPE-ALL";

    public const string SelfCertValid = "SC-VALID";
    public const string TinFormat = "SC-TIN-FORMAT";
    public const string TinMissing = "SC-TIN-MISSING";
    public const string SelfCertConsistent = "SC-CONSISTENT";

    // Deadlines: computed by R2 (Features/Deadlines). Listed here so the catalog, the UI and the audit log know them.
    public const string DeadlineNew90 = "DL-NEW-90";
    public const string DeadlinePreexistingYear = "DL-PRE-YEAR";
    public const string DeadlineExpiredNew = "DL-EXPIRED-NEW";
    public const string DeadlineExpiredPreexisting = "DL-EXPIRED-PRE";

    public const string Explanation = "RESP-EXPLANATION";
}

/// <summary>
/// Every rule the engine (or R2, for deadlines) can cite, with the legal basis and the page of the ANAF guide
/// (29.12.2025). "CPF" = Legea 207/2015 (Codul de procedură fiscală), consolidated 08.08.2026; "IGA" = the Romania-US
/// FATCA agreement (Legea 233/2015). Basis says whether the rule is the text of the law, our interpretation or a policy.
/// </summary>
public static class RuleCatalog
{
    public const int Version = 1;

    private const string CpfIndicia = "CPF Anexa 1, Sec. III.B pct. 12";
    private const string IgaIndicia = "IGA Anexa I, Sec. II.B.1";

    public static IReadOnlyList<RuleDefinition> All { get; } =
    [
        new(RuleIds.Domestic, Regime.Both, "Schimbare în România",
            "Datele din lista închisă de indicii rămân în România: nicio regulă nu se aplică, deci nu se deschide caz.",
            "29-31", Version, "Change within Romania",
            "CPF Anexa 1, Sec. III.B pct. 12 (listă închisă); Sec. VII.A pct. 5 lit. c)", RuleBasis.Interpretation,
            "Everything on the closed list of indicia stays in Romania: no rule applies, so no case is opened."),

        new(RuleIds.ChangeOfCircumstances, Regime.Crs, "Schimbare de circumstanțe",
            "Orice informație nouă relevantă sau care contrazice statutul titularului, inclusiv la conturile asociate.",
            "30, 35-36", Version, "Change in circumstances",
            "CPF Anexa 2 pct. 1; Anexa 1, Sec. IV.C pct. 4-7", RuleBasis.Verified,
            "Any new relevant information, or information that contradicts the holder's status, including on linked accounts."),

        new(RuleIds.NewAccountIndicia, Regime.Both, "Indicii pe un cont nou",
            "Lista de indicii e procedura conturilor preexistente. Pe conturile noi o folosim ca «motive să știe» că " +
            "autocertificarea e contrazisă. VII.A pct. 5 lit. d) spune că indiciile de la lit. b)-d) singure nu fac " +
            "autocertificarea incorectă (Comentariul OCDE: lit. c)-e)): de confirmat cu specialistul.",
            "29, 33, 35-36", Version, "Indicia on a new account",
            "CPF Anexa 1, Sec. IV.C pct. 1 și 4; Sec. VII.A pct. 5 lit. d); IGA Anexa I, Sec. III.B.2", RuleBasis.Interpretation,
            "The list of indicia is the procedure for preexisting accounts. On new accounts we use it as 'reason to know' " +
            "that the self-certification is contradicted. VII.A(5)(d) says indicia (b)-(d) alone do not make it incorrect " +
            "(OECD Commentary: (c)-(e)): to confirm with the domain expert."),

        // CRS indicia (the closed list, CPF Anexa 1, Sec. III.B pct. 12; guide p. 29).
        new(RuleIds.CrsResidence, Regime.Crs, "Identificat ca rezident al unei jurisdicții raportabile",
            "Banca are la dosar (în afara autocertificării) rezidența fiscală a titularului într-o jurisdicție raportabilă.",
            "29", Version, "Identified as resident of a reportable jurisdiction", $"{CpfIndicia} lit. a)", RuleBasis.Verified,
            "The bank's records (outside the self-certification) show the holder as tax resident in a reportable jurisdiction."),

        new(RuleIds.CrsAddress, Regime.Crs, "Adresă de reședință într-o jurisdicție raportabilă",
            "Adresă de reședință actuală (inclusiv căsuță poștală) într-o jurisdicție raportabilă. Adresa unui " +
            "prestator de servicii nu contează când există și alte adrese.",
            "29", Version, "Residence address in a reportable jurisdiction", $"{CpfIndicia} lit. b); pct. 13", RuleBasis.Verified,
            "Current residence address (including a post office box) in a reportable jurisdiction. A service provider's " +
            "address does not count when there are other addresses."),

        new(RuleIds.CrsMailing, Regime.Crs, "Adresă de corespondență într-o jurisdicție raportabilă",
            "Adresă de corespondență actuală (inclusiv căsuță poștală) într-o jurisdicție raportabilă.",
            "29", Version, "Mailing address in a reportable jurisdiction", $"{CpfIndicia} lit. b); pct. 13", RuleBasis.Verified,
            "Current mailing address (including a post office box) in a reportable jurisdiction."),

        new(RuleIds.CrsPhone, Regime.Crs, "Telefon într-o jurisdicție raportabilă, fără telefon în România",
            "Unul sau mai multe telefoane actuale dintr-o jurisdicție raportabilă și niciun telefon în România. " +
            "Telefonul străin lângă unul românesc nu e indiciu.",
            "29", Version, "Phone in a reportable jurisdiction, no Romanian phone", $"{CpfIndicia} lit. c); pct. 13",
            RuleBasis.Verified,
            "One or more current phone numbers in a reportable jurisdiction and no phone number in Romania. A foreign phone " +
            "next to a Romanian one is not an indicium."),

        new(RuleIds.CrsStandingOrder, Regime.Crs, "Ordin permanent către o jurisdicție raportabilă",
            "Instrucțiune permanentă activă de transfer către un cont dintr-o jurisdicție raportabilă, cu excepția celor " +
            "privind un cont de depozit (contul curent e cont de depozit).",
            "29, 17-18", Version, "Standing order to a reportable jurisdiction",
            $"{CpfIndicia} lit. d); pct. 13; Sec. VIII.C pct. 2", RuleBasis.Verified,
            "Active standing instruction to transfer funds to an account in a reportable jurisdiction, other than one " +
            "concerning a depository account (a current account is a depository account)."),

        new(RuleIds.CrsPowerOfAttorney, Regime.Crs, "Împuternicire pentru o persoană dintr-o jurisdicție raportabilă",
            "Împuternicire sau drept de semnătură valabil, acordat unei persoane cu adresă într-o jurisdicție raportabilă.",
            "29", Version, "Power of attorney to a person in a reportable jurisdiction", $"{CpfIndicia} lit. e)",
            RuleBasis.Verified,
            "Currently effective power of attorney or signatory authority granted to a person with an address in a " +
            "reportable jurisdiction."),

        new(RuleIds.CrsInCareOf, Regime.Crs, "Adresă «în atenția» sau «post-restant», singura adresă",
            "Adresă «în atenția» sau «post-restant» într-o jurisdicție raportabilă, când banca nu are altă adresă. " +
            "Dacă nu există alte indicii: căutare pe hârtie sau autocertificare / documente, altfel cont nedocumentat.",
            "29, 31, 48", Version, "In-care-of or hold-mail address as the only address",
            $"{CpfIndicia} lit. f); pct. 13, 17-18", RuleBasis.Verified,
            "In-care-of or hold-mail address in a reportable jurisdiction when the bank has no other address. With no other " +
            "indicia: paper record search or self-certification / documents, otherwise an undocumented account."),

        // FATCA indicia (IGA Anexa I, Sec. II.B.1; guide p. 31). No CRS exceptions are inherited.
        new(RuleIds.FatcaCitizenship, Regime.Fatca, "Cetățean sau rezident al SUA",
            "Titularul e identificat ca cetățean sau rezident al SUA. Singura situație în care cetățenia contează.",
            "31", Version, "US citizen or resident", $"{IgaIndicia} lit. a)", RuleBasis.Verified,
            "The holder is identified as a US citizen or resident. The only case where citizenship counts."),

        new(RuleIds.FatcaBirthPlace, Regime.Fatca, "Locul nașterii în SUA",
            "Locul nașterii în SUA, indicat fără ambiguitate.",
            "31", Version, "US place of birth", $"{IgaIndicia} lit. b)", RuleBasis.Verified,
            "Unambiguous indication of a US place of birth."),

        new(RuleIds.FatcaAddress, Regime.Fatca, "Adresă în SUA",
            "Adresă actuală de reședință sau de corespondență în SUA (inclusiv căsuță poștală).",
            "31", Version, "US address", $"{IgaIndicia} lit. c)", RuleBasis.Verified,
            "Current US residence or mailing address (including a US post office box)."),

        new(RuleIds.FatcaPhone, Regime.Fatca, "Telefon în SUA",
            "Orice telefon actual din SUA, chiar dacă există și un telefon în România (excepția CRS nu se aplică).",
            "31", Version, "US phone", $"{IgaIndicia} lit. d)", RuleBasis.Verified,
            "Any current US phone number, even next to a Romanian one (the CRS exception does not apply)."),

        new(RuleIds.FatcaStandingOrder, Regime.Fatca, "Ordin permanent către un cont din SUA",
            "Instrucțiune permanentă de transfer către un cont din SUA, din orice cont: excepția CRS pentru conturile de " +
            "depozit nu există la FATCA.",
            "31", Version, "Standing order to a US account", $"{IgaIndicia} lit. e)", RuleBasis.Verified,
            "Standing instruction to transfer funds to a US account, from any account: the CRS depository exception does " +
            "not exist under FATCA."),

        new(RuleIds.FatcaPowerOfAttorney, Regime.Fatca, "Împuternicire pentru o persoană cu adresă în SUA",
            "Împuternicire sau drept de semnătură valabil, acordat unei persoane cu adresă în SUA.",
            "31", Version, "Power of attorney to a person with a US address", $"{IgaIndicia} lit. f)", RuleBasis.Verified,
            "Currently effective power of attorney or signatory authority granted to a person with a US address."),

        new(RuleIds.FatcaInCareOf, Regime.Fatca, "Adresă «în grija» sau «păstrare corespondență», singura adresă",
            "Adresă «în grija» sau «păstrare corespondență», singura adresă la dosar, în orice țară. Excepție: la contul " +
            "preexistent de valoare mică, c/o din afara SUA și «păstrare corespondență» nu sunt indicii.",
            "31", Version, "In-care-of or hold-mail address as the only address", $"{IgaIndicia} lit. g)", RuleBasis.Verified,
            "In-care-of or hold-mail address that is the only address on file, in any country. Exception: on a preexisting " +
            "lower value account, a non-US in-care-of address or a hold-mail instruction is not an indicium."),

        new(RuleIds.FatcaChange, Regime.Fatca, "FATCA: schimbare de situație",
            "Schimbarea dă motive să știe că autocertificarea e incorectă: autocertificare nouă (cetățean / rezident SUA " +
            "sau nu); fără ea, contul e raportabil SUA. Acordul nu dă un termen și nu are varianta «explicație».",
            "38", Version, "FATCA change in circumstances", "IGA Anexa I, Sec. III.B.2; Sec. II.B.4", RuleBasis.Verified,
            "The change gives reason to know the self-certification is incorrect: new self-certification (US citizen / " +
            "resident or not); without it, the account is a US reportable account. The agreement sets no deadline and has " +
            "no 'reasonable explanation' route."),

        new(RuleIds.FatcaScopeAllAccounts, Regime.Fatca, "FATCA: verificăm toate conturile",
            "Banca nu aplică pragurile de 50.000 USD (conturi preexistente și conturi de depozit noi): toate conturile " +
            "persoanelor fizice sunt verificate.",
            "31, 38", Version, "FATCA: every account is reviewed", "IGA Anexa I, Sec. II.A; Sec. III.A", RuleBasis.Policy,
            "The bank does not apply the USD 50,000 thresholds (preexisting accounts and new depository accounts): every " +
            "individual account is reviewed."),

        // Self-certification (CPF Anexa 1, Sec. IV.B-IV.C, VII.A; guide p. 33-37).
        new(RuleIds.SelfCertValid, Regime.Crs, "Autocertificare validă",
            "Semnată sau confirmată, datată, cu: nume, adresa de rezidență, jurisdicțiile de rezidență, TIN pentru fiecare " +
            "jurisdicție raportabilă (sau motivul lipsei), data nașterii.",
            "34", Version, "Valid self-certification", "CPF Anexa 1, Sec. IV.B pct. 3; Sec. I.D", RuleBasis.Verified,
            "Signed or confirmed, dated, with: name, residence address, jurisdictions of residence, TIN for each reportable " +
            "jurisdiction (or the reason it is missing), date of birth."),

        new(RuleIds.TinFormat, Regime.Crs, "TIN în format greșit",
            "TIN-ul declarat nu are formatul țării (verificare de format, nu de titular). Tratat ca motiv de clarificare.",
            "36-37", Version, "TIN in the wrong format", "CPF Anexa 1, Sec. IV.C pct. 16-18 (rezonabilitate)", RuleBasis.Policy,
            "The declared TIN does not match the country's format (a format check, not an ownership check). Treated as a " +
            "reason to clarify."),

        new(RuleIds.TinMissing, Regime.Crs, "TIN lipsă",
            "TIN lipsă acceptat doar cu motiv (A/B/C). Fără TIN, deși jurisdicția emite TIN tuturor persoanelor fizice " +
            "(fișa OCDE): banca are motive să știe că autocertificarea e nesigură.",
            "37, 48, 53", Version, "Missing TIN", "CPF Anexa 1, Sec. VII.A pct. 3 lit. a) pct. (iv); Sec. I.D pct. 1-4",
            RuleBasis.Verified,
            "A missing TIN is accepted only with a reason (A/B/C). No TIN although the jurisdiction issues TINs to all " +
            "individuals (OECD sheet): the bank has reason to know the self-certification is unreliable."),

        new(RuleIds.SelfCertConsistent, Regime.Crs, "Autocertificare consecventă cu dosarul",
            "Jurisdicțiile din indicii sunt declarate în autocertificarea validă: contul e raportabil pentru fiecare " +
            "jurisdicție raportabilă declarată.",
            "33-34, 36-37", Version, "Self-certification consistent with the file",
            "CPF Anexa 1, Sec. IV.B pct. 1; Sec. IV.C pct. 16-18", RuleBasis.Verified,
            "The jurisdictions of the indicia are declared in the valid self-certification: the account is reportable for " +
            "each declared reportable jurisdiction."),

        // Deadlines (computed by R2; the engine never computes a deadline).
        new(RuleIds.DeadlineNew90, Regime.Crs, "Cont nou: 90 de zile",
            "Autocertificarea își pierde valabilitatea când banca știe sau are motive să știe; banca poate păstra statutul " +
            "vechi cel mult 90 de zile, până la autocertificare nouă sau explicație.",
            "20-21, 35-36", Version, "New account: 90 days", "CPF Anexa 1, Sec. IV.C pct. 4-5", RuleBasis.Verified,
            "The self-certification loses validity when the bank knows or has reason to know; the bank may keep the old " +
            "status for at most 90 days, until a new self-certification or an explanation."),

        new(RuleIds.DeadlinePreexistingYear, Regime.Crs, "Cont preexistent: sfârșitul anului sau 90 de zile",
            "Ultima zi a anului calendaristic relevant sau 90 de zile de la notificare / descoperire, cea mai târzie. Ghidul " +
            "spune „sau”; textul „oricare dintre acestea este mai recentă” e scris pentru testul adresei de rezidență, iar " +
            "aplicarea lui generală e decizia echipei.",
            "29", Version, "Preexisting account: year end or 90 days", "CPF Anexa 2 pct. 1; ghid p. 29", RuleBasis.Interpretation,
            "The last day of the relevant calendar year or 90 days after notice / discovery, whichever is later. The guide says " +
            "\"or\"; the \"whichever is later\" text is written for the residence address test, and applying it generally is the team's decision."),

        new(RuleIds.DeadlineExpiredNew, Regime.Crs, "Expirare, cont nou",
            "Fără autocertificare nouă: titularul e rezident și în jurisdicția inițială, și în cea nouă.",
            "35-36", Version, "Expired, new account", "CPF Anexa 1, Sec. IV.C pct. 7", RuleBasis.Verified,
            "No new self-certification: the holder is resident both in the original jurisdiction and in the new one."),

        new(RuleIds.DeadlineExpiredPreexisting, Regime.Crs, "Expirare, cont preexistent",
            "Fără autocertificare și documente: se aplică căutarea în evidența electronică; rezident cel puțin în noua " +
            "jurisdicție.",
            "29", Version, "Expired, preexisting account", "CPF Anexa 2 pct. 1; Anexa 1, Sec. III.B pct. 14-15",
            RuleBasis.Verified,
            "No self-certification and documents: the electronic record search applies; resident at least in the new " +
            "jurisdiction."),

        new(RuleIds.Explanation, Regime.Crs, "Explicație rezonabilă acceptată",
            "Cont nou: explicație rezonabilă plus documente (ex. student, profesor sau stagiar, diplomat). Indiciul e " +
            "acoperit și jurisdicția nu devine raportabilă. Nu există la FATCA.",
            "35, 37", Version, "Accepted reasonable explanation", "CPF Anexa 1, Sec. IV.C pct. 1 lit. b)", RuleBasis.Verified,
            "New account: reasonable explanation plus documents (e.g. student, teacher or trainee, diplomat). The indicium is " +
            "covered and the jurisdiction does not become reportable. Not available under FATCA."),
    ];

    private static readonly Dictionary<string, RuleDefinition> ById = All.ToDictionary(r => r.Id);

    public static RuleDefinition Get(string id) =>
        ById.TryGetValue(id, out var rule) ? rule : throw new KeyNotFoundException($"Unknown rule {id}.");

    public static bool Contains(string id) => ById.ContainsKey(id);
}
