namespace HackathonStarter.Features.Rules;

/// <summary>
/// The rule catalog (docs/PLAN.md 8.2, pages checked in docs/reguli-verificate.md). IDs are final: tests, the UI and the
/// audit log refer to them. Owner: R1.
/// </summary>
public static class RuleCatalog
{
    public const string NotVerified = "NEVERIFICAT";

    public static readonly IReadOnlyList<RuleDefinition> All =
    [
        new("GEN-DOMESTIC", Regime.Crs, "The change stays in Romania: no indicium from the closed list", "Schimbarea rămâne în România: niciun indiciu din lista închisă", NotVerified),
        new("GEN-CHANGE-CIRC", Regime.Crs, "Change in circumstances: new relevant or contradictory information, also on linked accounts", "Schimbare de situație: informație nouă relevantă sau contradictorie, inclusiv la conturile asociate", "30, 35-36"),
        new("CRS-IND-RES", Regime.Crs, "Account holder identified as a resident of a reportable jurisdiction", "Titularul e identificat ca rezident al unei jurisdicții raportabile", "29"),
        new("CRS-IND-ADDR", Regime.Crs, "Current residence address (including a PO box) in a reportable jurisdiction", "Adresă de reședință curentă (inclusiv căsuță poștală) într-o jurisdicție raportabilă", "29"),
        new("CRS-IND-MAIL", Regime.Crs, "Current mailing address in a reportable jurisdiction", "Adresă de corespondență curentă într-o jurisdicție raportabilă", "29"),
        new("CRS-IND-PHONE", Regime.Crs, "Phone in a reportable jurisdiction and no phone in Romania", "Telefon într-o jurisdicție raportabilă și niciun telefon în România", "29"),
        new("CRS-IND-SO", Regime.Crs, "Standing order to an account in a reportable jurisdiction, other than from a depository account", "Instrucțiune permanentă de transfer către un cont dintr-o jurisdicție raportabilă, cu excepția conturilor de depozit", "29"),
        new("CRS-IND-POA", Regime.Crs, "Valid power of attorney or signatory authority for a person with an address in a reportable jurisdiction", "Împuternicire sau drept de semnătură valabil pentru o persoană cu adresă într-o jurisdicție raportabilă", "29"),
        new("CRS-IND-CO", Regime.Crs, "In-care-of or hold-mail address in a reportable jurisdiction, when the bank has no other address", "Adresă „în atenția” sau „post restant” într-o jurisdicție raportabilă, dacă banca nu are altă adresă", "29, 48"),
        new("FATCA-IND-CIT", Regime.Fatca, "US citizen or resident", "Cetățean sau rezident al SUA", "31"),
        new("FATCA-IND-POB", Regime.Fatca, "Unambiguous US place of birth", "Locul nașterii în SUA, indicat fără ambiguitate", "31"),
        new("FATCA-IND-ADDR", Regime.Fatca, "Current US residence or mailing address, including a PO box", "Adresă actuală în SUA (reședință sau corespondență, inclusiv căsuță poștală)", "31"),
        new("FATCA-IND-PHONE", Regime.Fatca, "Any current US phone number, even with a Romanian one", "Orice telefon actual din SUA, chiar dacă există și unul românesc", "31"),
        new("FATCA-IND-SO", Regime.Fatca, "Standing order to a US account (no depository exception)", "Ordin permanent către un cont din SUA (fără excepția pentru conturile de depozit)", "31"),
        new("FATCA-IND-POA", Regime.Fatca, "Power of attorney or signatory authority for a person with a US address", "Împuternicire sau drept de semnătură pentru o persoană cu adresă în SUA", "31"),
        new("FATCA-IND-CO", Regime.Fatca, "In-care-of or hold-mail address as the only address (not for a non-US c/o on a low-value preexisting account)", "Adresă „în grija” sau „păstrare corespondență”, singura adresă (nu și c/o din afara SUA la contul preexistent de valoare mică)", "31"),
        new("FATCA-SC", Regime.Fatca, "Change in circumstances: a new self-certification; without it, the account is US reportable", "Schimbare de situație: autocertificare nouă; fără ea, contul e raportabil SUA", "38"),
        new("SC-VALID", Regime.Crs, "Valid self-certification: signed or confirmed, dated, with name, residence address, jurisdictions, TIN per reportable jurisdiction, date of birth", "Autocertificare validă: semnată sau confirmată, datată, cu nume, adresă de rezidență, jurisdicții, TIN pentru fiecare jurisdicție raportabilă, data nașterii", "34"),
        new("SC-TIN-FORMAT", Regime.Crs, "TIN in the wrong format for the declared country", "TIN în format greșit pentru țara declarată", "36-37"),
        new("SC-TIN-MISSING", Regime.Crs, "Missing TIN: accepted only with reason A, B or C", "TIN lipsă: acceptat doar cu motivul A, B sau C", "53, 37"),
        new("SC-CONSISTENT", Regime.Crs, "The self-certification covers every jurisdiction with an indicium and passes the reasonableness test", "Autocertificarea acoperă toate jurisdicțiile cu indicii și trece testul de rezonabilitate", "33-34, 36-37"),
        new("DL-NEW-90", Regime.Crs, "New account: 90 calendar days from the loss of validity of the self-certification", "Cont nou: 90 de zile calendaristice de la pierderea valabilității autocertificării", "20-21, 35-36"),
        new("DL-PRE-YEAR", Regime.Crs, "Preexisting account: the end of the year or 90 days, whichever is later (our reading; the guide says \"or\")", "Cont preexistent: sfârșitul anului sau 90 de zile, data mai târzie (interpretarea noastră; ghidul spune „sau”)", "29"),
        new("DL-EXPIRED-NEW", Regime.Crs, "Expired, new account: resident in the original and in the new jurisdiction", "Expirat, cont nou: rezident și în jurisdicția inițială, și în cea nouă", "35-36"),
        new("DL-EXPIRED-PRE", Regime.Crs, "Expired, preexisting account: resident at least in the new jurisdiction", "Expirat, cont preexistent: rezident cel puțin în noua jurisdicție", "29"),
        new("RESP-EXPLANATION", Regime.Crs, "Reasonable explanations: student (with a visa where needed), teacher or trainee, diplomat, EU official", "Explicații rezonabile: student (cu viza corespunzătoare, dacă este cazul), profesor sau stagiar, diplomat, funcționar UE", "37"),
    ];

    public static RuleDefinition Get(string id) =>
        All.FirstOrDefault(r => r.Id == id) ?? throw new KeyNotFoundException($"Unknown rule {id}");
}
