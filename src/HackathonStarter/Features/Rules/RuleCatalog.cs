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

    public const string NotVerified = "NEVERIFICAT";

    private const string CpfIndicia = "CPF Anexa 1, Sec. III.B pct. 12";
    private const string IgaIndicia = "IGA Anexa I, Sec. II.B.1";

    public static IReadOnlyList<RuleDefinition> All { get; } =
    [
        Rule(RuleIds.Domestic, Regime.Both, "Schimbare în România",
            "Datele din lista închisă de indicii rămân în România: nicio regulă nu se aplică, deci nu se deschide caz.",
            NotVerified, "Change within Romania",
            "CPF Anexa 1, Sec. III.B pct. 12 (listă închisă); Sec. VII.A pct. 5 lit. c)", RuleBasis.Interpretation,
            "Everything on the closed list of indicia stays in Romania: no rule applies, so no case is opened."),

        Rule(RuleIds.ChangeOfCircumstances, Regime.Crs, "Schimbare de circumstanțe",
            "Orice informație nouă relevantă sau care contrazice statutul titularului, inclusiv la conturile asociate.",
            "30, 35-36", "Change in circumstances",
            "CPF Anexa 2 pct. 1; Anexa 1, Sec. IV.C pct. 4-7", RuleBasis.Verified,
            "Any new relevant information, or information that contradicts the holder's status, including on linked accounts."),

        Rule(RuleIds.NewAccountIndicia, Regime.Both, "Indicii pe un cont nou",
            "Lista de indicii e procedura conturilor preexistente. Pe conturile noi o folosim ca «motive să știe» că " +
            "autocertificarea e contrazisă. VII.A pct. 5 lit. d) spune că indiciile de la lit. b)-d) singure nu fac " +
            "autocertificarea incorectă (Comentariul OCDE: lit. c)-e)): de confirmat cu specialistul.",
            "29, 33, 35-36", "Indicia on a new account",
            "CPF Anexa 1, Sec. IV.C pct. 1 și 4; Sec. VII.A pct. 5 lit. d); IGA Anexa I, Sec. III.B.2", RuleBasis.Interpretation,
            "The list of indicia is the procedure for preexisting accounts. On new accounts we use it as 'reason to know' " +
            "that the self-certification is contradicted. VII.A(5)(d) says indicia (b)-(d) alone do not make it incorrect " +
            "(OECD Commentary: (c)-(e)): to confirm with the domain expert."),

        // CRS indicia (the closed list, CPF Anexa 1, Sec. III.B pct. 12; guide p. 29).
        Rule(RuleIds.CrsResidence, Regime.Crs, "Identificat ca rezident al unei jurisdicții raportabile",
            "Banca are la dosar (în afara autocertificării) rezidența fiscală a titularului într-o jurisdicție raportabilă.",
            "29", "Identified as resident of a reportable jurisdiction", $"{CpfIndicia} lit. a)", RuleBasis.Verified,
            "The bank's records (outside the self-certification) show the holder as tax resident in a reportable jurisdiction."),

        Rule(RuleIds.CrsAddress, Regime.Crs, "Adresă de reședință într-o jurisdicție raportabilă",
            "Adresă de reședință actuală (inclusiv căsuță poștală) într-o jurisdicție raportabilă.",
            "29", "Residence address in a reportable jurisdiction", $"{CpfIndicia} lit. b); pct. 13", RuleBasis.Verified,
            "Current residence address (including a post office box) in a reportable jurisdiction."),

        Rule(RuleIds.CrsMailing, Regime.Crs, "Adresă de corespondență într-o jurisdicție raportabilă",
            "Adresă de corespondență actuală (inclusiv căsuță poștală) într-o jurisdicție raportabilă.",
            "29", "Mailing address in a reportable jurisdiction", $"{CpfIndicia} lit. b); pct. 13", RuleBasis.Verified,
            "Current mailing address (including a post office box) in a reportable jurisdiction."),

        Rule(RuleIds.CrsPhone, Regime.Crs, "Telefon într-o jurisdicție raportabilă, fără telefon în România",
            "Unul sau mai multe telefoane dintr-o jurisdicție raportabilă și niciun telefon în România. Telefonul străin " +
            "lângă unul românesc nu e indiciu. Motorul citește doar telefoanele actuale (lectura noastră).",
            "29", "Phone in a reportable jurisdiction, no Romanian phone", $"{CpfIndicia} lit. c); pct. 13",
            RuleBasis.Verified,
            "One or more phone numbers in a reportable jurisdiction and no phone number in Romania. A foreign phone next to " +
            "a Romanian one is not an indicium. The engine reads only current phones (our reading)."),

        Rule(RuleIds.CrsStandingOrder, Regime.Crs, "Ordin permanent către o jurisdicție raportabilă",
            "Instrucțiune permanentă activă de transfer către un cont dintr-o jurisdicție raportabilă, cu excepția celor " +
            "privind un cont de depozit (contul curent e cont de depozit).",
            "29, 17-18", "Standing order to a reportable jurisdiction",
            $"{CpfIndicia} lit. d); pct. 13; Sec. VIII.C pct. 2", RuleBasis.Verified,
            "Active standing instruction to transfer funds to an account in a reportable jurisdiction, other than one " +
            "concerning a depository account (a current account is a depository account)."),

        Rule(RuleIds.CrsPowerOfAttorney, Regime.Crs, "Împuternicire pentru o persoană dintr-o jurisdicție raportabilă",
            "Împuternicire sau drept de semnătură valabil, acordat unei persoane cu adresă într-o jurisdicție raportabilă.",
            "29", "Power of attorney to a person in a reportable jurisdiction", $"{CpfIndicia} lit. e)",
            RuleBasis.Verified,
            "Currently effective power of attorney or signatory authority granted to a person with an address in a " +
            "reportable jurisdiction."),

        Rule(RuleIds.CrsInCareOf, Regime.Crs, "Adresă «în atenția» sau «post-restant», singura adresă",
            "Adresă «în atenția» sau «post-restant» într-o jurisdicție raportabilă, când banca nu are altă adresă. " +
            "Dacă nu există alte indicii: căutare pe hârtie sau autocertificare / documente, altfel cont nedocumentat.",
            "29, 31, 48", "In-care-of or hold-mail address as the only address",
            $"{CpfIndicia} lit. f); pct. 13, 17-18", RuleBasis.Verified,
            "In-care-of or hold-mail address in a reportable jurisdiction when the bank has no other address. With no other " +
            "indicia: paper record search or self-certification / documents, otherwise an undocumented account."),

        // FATCA indicia (IGA Anexa I, Sec. II.B.1; guide p. 31). No CRS exceptions are inherited.
        Rule(RuleIds.FatcaCitizenship, Regime.Fatca, "Cetățean sau rezident al SUA",
            "Titularul e identificat ca cetățean sau rezident al SUA. Singura situație în care cetățenia contează.",
            "31", "US citizen or resident", $"{IgaIndicia} lit. a)", RuleBasis.Verified,
            "The holder is identified as a US citizen or resident. The only case where citizenship counts."),

        Rule(RuleIds.FatcaBirthPlace, Regime.Fatca, "Locul nașterii în SUA",
            "Locul nașterii în SUA, indicat fără ambiguitate.",
            "31", "US place of birth", $"{IgaIndicia} lit. b)", RuleBasis.Verified,
            "Unambiguous indication of a US place of birth."),

        Rule(RuleIds.FatcaAddress, Regime.Fatca, "Adresă în SUA",
            "Adresă actuală de reședință sau de corespondență în SUA (inclusiv căsuță poștală).",
            "31", "US address", $"{IgaIndicia} lit. c)", RuleBasis.Verified,
            "Current US residence or mailing address (including a US post office box)."),

        Rule(RuleIds.FatcaPhone, Regime.Fatca, "Telefon în SUA",
            "Orice telefon actual din SUA, chiar dacă există și un telefon în România (excepția CRS nu se aplică).",
            "31", "US phone", $"{IgaIndicia} lit. d)", RuleBasis.Verified,
            "Any current US phone number, even next to a Romanian one (the CRS exception does not apply)."),

        Rule(RuleIds.FatcaStandingOrder, Regime.Fatca, "Ordin permanent către un cont din SUA",
            "Instrucțiune permanentă de transfer către un cont din SUA, din orice cont: excepția CRS pentru conturile de " +
            "depozit nu există la FATCA.",
            "31", "Standing order to a US account", $"{IgaIndicia} lit. e)", RuleBasis.Verified,
            "Standing instruction to transfer funds to a US account, from any account: the CRS depository exception does " +
            "not exist under FATCA."),

        Rule(RuleIds.FatcaPowerOfAttorney, Regime.Fatca, "Împuternicire pentru o persoană cu adresă în SUA",
            "Împuternicire sau drept de semnătură valabil, acordat unei persoane cu adresă în SUA.",
            "31", "Power of attorney to a person with a US address", $"{IgaIndicia} lit. f)", RuleBasis.Verified,
            "Currently effective power of attorney or signatory authority granted to a person with a US address."),

        Rule(RuleIds.FatcaInCareOf, Regime.Fatca, "Adresă «în grija» sau «păstrare corespondență», singura adresă",
            "Adresă «în grija» sau «păstrare corespondență», singura adresă la dosar, în orice țară. Excepție: la contul " +
            "preexistent de valoare mică, c/o din afara SUA și «păstrare corespondență» nu sunt indicii.",
            "31", "In-care-of or hold-mail address as the only address", $"{IgaIndicia} lit. g)", RuleBasis.Verified,
            "In-care-of or hold-mail address that is the only address on file, in any country. Exception: on a preexisting " +
            "lower value account, a non-US in-care-of address or a hold-mail instruction is not an indicium."),

        Rule(RuleIds.FatcaChange, Regime.Fatca, "FATCA: schimbare de situație",
            "Schimbarea dă motive să știe că autocertificarea e incorectă. Cont nou: autocertificare nouă validă care " +
            "stabilește dacă titularul e persoană SUA; fără ea, contul e raportabil SUA. Cont preexistent: raportabil SUA, " +
            "în afară de excepțiile din II.B.4 (autocertificare «nu sunt persoană SUA» și, după indiciu, documente). " +
            "Cetățenia SUA la dosar nu are excepție. Acordul nu dă un termen.",
            "38", "FATCA change in circumstances", "IGA Anexa I, Sec. III.B.2; Sec. II.B.3-4; Sec. VI.A", RuleBasis.Verified,
            "The change gives reason to know the self-certification is incorrect. New account: a new valid " +
            "self-certification that establishes whether the holder is a US person; without it, the account is US " +
            "reportable. Preexisting account: US reportable, except under II.B.4 (a \"not a US person\" self-certification " +
            "and, depending on the indicium, documents). US citizenship on file has no exception. The agreement sets no deadline."),

        Rule(RuleIds.FatcaScopeAllAccounts, Regime.Fatca, "FATCA: verificăm toate conturile",
            "Banca nu aplică pragurile de 50.000 USD (conturi preexistente și conturi de depozit noi): toate conturile " +
            "persoanelor fizice sunt verificate.",
            "25", "FATCA: every account is reviewed", "IGA Anexa I, Sec. II.A; Sec. III.A", RuleBasis.Policy,
            "The bank does not apply the USD 50,000 thresholds (preexisting accounts and new depository accounts): every " +
            "individual account is reviewed."),

        // Self-certification (CPF Anexa 1, Sec. IV.B-IV.C, VII.A; guide p. 33-37).
        Rule(RuleIds.SelfCertValid, Regime.Crs, "Autocertificare validă",
            "Semnată sau confirmată, datată, cu: nume, adresa de rezidență, jurisdicțiile de rezidență, TIN pentru fiecare " +
            "jurisdicție raportabilă (sau motivul lipsei), data nașterii.",
            "34", "Valid self-certification", "CPF Anexa 1, Sec. IV.B pct. 3; Sec. I.D", RuleBasis.Verified,
            "Signed or confirmed, dated, with: name, residence address, jurisdictions of residence, TIN for each reportable " +
            "jurisdiction (or the reason it is missing), date of birth."),

        Rule(RuleIds.TinFormat, Regime.Crs, "TIN în format greșit",
            "TIN-ul declarat nu are formatul țării (verificare de format, nu de titular). Tratat ca motiv de clarificare.",
            "36-37", "TIN in the wrong format", "CPF Anexa 1, Sec. IV.C pct. 16-18 (rezonabilitate)", RuleBasis.Policy,
            "The declared TIN does not match the country's format (a format check, not an ownership check). Treated as a " +
            "reason to clarify."),

        Rule(RuleIds.TinMissing, Regime.Crs, "TIN lipsă",
            "TIN lipsă acceptat doar cu motiv (A/B/C, formularul de la p. 53). Fără TIN, deși jurisdicția emite TIN tuturor " +
            "rezidenților (fișa OCDE), orice motiv: banca are motive să știe că autocertificarea e nesigură (p. 37). " +
            "TIN-ul SUA al unui rezident SUA nu poate lipsi (p. 38).",
            "37, 38, 48, 53", "Missing TIN", "CPF Anexa 1, Sec. VII.A pct. 3 lit. a) pct. (iv); Sec. I.D pct. 1-4",
            RuleBasis.Verified,
            "A missing TIN is accepted only with a reason (A/B/C, the form on p. 53). No TIN although the jurisdiction issues " +
            "TINs to all residents (OECD sheet), whatever the reason: the bank has reason to know the self-certification is " +
            "unreliable (p. 37). A US resident's US TIN cannot be missing (p. 38)."),

        Rule(RuleIds.SelfCertConsistent, Regime.Crs, "Autocertificare consecventă cu dosarul",
            "Jurisdicțiile din indicii sunt declarate în autocertificarea validă: contul e raportabil pentru fiecare " +
            "jurisdicție raportabilă declarată.",
            "33-34, 36-37", "Self-certification consistent with the file",
            "CPF Anexa 1, Sec. IV.B pct. 1; Sec. IV.C pct. 16-18", RuleBasis.Verified,
            "The jurisdictions of the indicia are declared in the valid self-certification: the account is reportable for " +
            "each declared reportable jurisdiction."),

        // Deadlines (computed by R2; the engine never computes a deadline).
        Rule(RuleIds.DeadlineNew90, Regime.Crs, "Cont nou: 90 de zile",
            "Autocertificarea își pierde valabilitatea când banca știe sau are motive să știe; banca poate păstra statutul " +
            "vechi cel mult 90 de zile, până la autocertificare nouă sau explicație.",
            "20-21, 35-36", "New account: 90 days", "CPF Anexa 1, Sec. IV.C pct. 4-5", RuleBasis.Verified,
            "The self-certification loses validity when the bank knows or has reason to know; the bank may keep the old " +
            "status for at most 90 days, until a new self-certification or an explanation."),

        Rule(RuleIds.DeadlinePreexistingYear, Regime.Crs, "Cont preexistent: sfârșitul anului sau 90 de zile",
            "Ultima zi a anului calendaristic relevant sau 90 de zile de la notificare / descoperire, cea mai târzie. Pe ruta " +
            "indiciilor legea spune doar „ori” (Anexa 1, Sec. III.B pct. 16), Comentariul OCDE „the later of” (Sec. III pct. 16); " +
            "ghidul spune „sau”. „Cea mai târzie” e interpretarea noastră (GOLDEN-CASES N1).",
            "29", "Preexisting account: year end or 90 days",
            "CPF Anexa 1, Sec. III.B pct. 16 (ruta indiciilor: „ori”); Comentariul OCDE, Sec. III pct. 16 („the later of”)",
            RuleBasis.Interpretation,
            "The last day of the relevant calendar year or 90 days after notice / discovery, whichever is later. On the indicia " +
            "route the law says only \"or\" (Anexa 1, III.B(16)), the OECD Commentary \"the later of\" (Section III, para. 16); " +
            "the guide says \"or\". \"Whichever is later\" is our interpretation (GOLDEN-CASES N1)."),

        Rule(RuleIds.DeadlineExpiredNew, Regime.Crs, "Expirare, cont nou",
            "Fără autocertificare nouă: titularul e rezident și în jurisdicția inițială, și în cea nouă.",
            "35-36", "Expired, new account", "CPF Anexa 1, Sec. IV.C pct. 7", RuleBasis.Verified,
            "No new self-certification: the holder is resident both in the original jurisdiction and in the new one."),

        Rule(RuleIds.DeadlineExpiredPreexisting, Regime.Crs, "Expirare, cont preexistent",
            "Fără autocertificare și documente: se aplică căutarea în evidența electronică; rezident cel puțin în noua " +
            "jurisdicție.",
            "29", "Expired, preexisting account", "CPF Anexa 2 pct. 1; Anexa 1, Sec. III.B pct. 14-15",
            RuleBasis.Verified,
            "No self-certification and documents: the electronic record search applies; resident at least in the new " +
            "jurisdiction."),

        Rule(RuleIds.Explanation, Regime.Crs, "Explicație rezonabilă acceptată",
            "Cont nou: explicație rezonabilă plus documente (ex. student, profesor sau stagiar, diplomat). Indiciul e " +
            "acoperit și jurisdicția nu devine raportabilă. Nu există la FATCA.",
            "35, 37", "Accepted reasonable explanation", "CPF Anexa 1, Sec. IV.C pct. 1 lit. b)", RuleBasis.Verified,
            "New account: reasonable explanation plus documents (e.g. student, teacher or trainee, diplomat). The indicium is " +
            "covered and the jurisdiction does not become reportable. Not available under FATCA."),
    ];

    // Entries are written Romanian first, as they were checked against the guide; the record keeps English in Title / Description.
    private static RuleDefinition Rule(string id, Regime regime, string titleRo, string descriptionRo, string guidePage,
        string title, string legalBasis, RuleBasis basis, string description) =>
        new(id, regime, title, titleRo, guidePage, Version, description, descriptionRo, legalBasis, basis);

    private static readonly Dictionary<string, RuleDefinition> ById = All.ToDictionary(r => r.Id);

    public static RuleDefinition Get(string id) =>
        ById.TryGetValue(id, out var rule) ? rule : throw new KeyNotFoundException($"Unknown rule {id}.");

    public static bool Contains(string id) => ById.ContainsKey(id);
}
