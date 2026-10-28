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
        new("GEN-DOMESTIC", Regime.Crs, "The change stays in Romania: not relevant for tax", "Schimbarea rămâne în România: nu e relevantă fiscal", NotVerified),
        new("GEN-CHANGE-CIRC", Regime.Crs, "Change in circumstances that may invalidate the self-certification", "Schimbare de circumstanțe care poate invalida autocertificarea", "35"),
        new("CRS-IND-ADDR", Regime.Crs, "Current residence address in a reportable jurisdiction", "Adresă de reședință curentă într-o jurisdicție raportabilă", "29"),
        new("CRS-IND-MAIL", Regime.Crs, "Current mailing address in a reportable jurisdiction", "Adresă de corespondență curentă într-o jurisdicție raportabilă", "29"),
        new("CRS-IND-PHONE", Regime.Crs, "Phone in a reportable jurisdiction and no phone in Romania", "Telefon într-o jurisdicție raportabilă și niciun telefon în România", "29"),
        new("CRS-IND-SO", Regime.Crs, "Standing order to an account in a reportable jurisdiction, other than from a depository account", "Ordin permanent către un cont dintr-o jurisdicție raportabilă, cu excepția conturilor de depozit", "29"),
        new("CRS-IND-POA", Regime.Crs, "Power of attorney for a person with an address in a reportable jurisdiction", "Împuternicire pentru o persoană cu adresă într-o jurisdicție raportabilă", "29"),
        new("CRS-IND-CO", Regime.Crs, "In-care-of or hold-mail address in a reportable jurisdiction, the only address", "Adresă „în atenția” sau „post restant” într-o jurisdicție raportabilă, singura adresă", "29, 48"),
        new("FATCA-IND-CIT", Regime.Fatca, "US citizen or resident", "Cetățean sau rezident SUA", "31"),
        new("FATCA-IND-POB", Regime.Fatca, "Place of birth in the US", "Locul nașterii în SUA", "31"),
        new("FATCA-IND-ADDR", Regime.Fatca, "Current US residence or mailing address", "Adresă curentă de reședință sau de corespondență în SUA", "31"),
        new("FATCA-IND-PHONE", Regime.Fatca, "Current US phone number, even with a Romanian one", "Telefon curent din SUA, chiar dacă există și unul românesc", "31"),
        new("FATCA-IND-SO", Regime.Fatca, "Standing order to a US account (no depository exception)", "Ordin permanent către un cont din SUA (fără excepția pentru depozit)", "31"),
        new("FATCA-IND-POA", Regime.Fatca, "Power of attorney for a person with a US address", "Împuternicire pentru o persoană cu adresă în SUA", "31"),
        new("SC-TIN-FORMAT", Regime.Crs, "TIN in the wrong format for the declared country", "TIN în format greșit pentru țara declarată", "36-37"),
        new("SC-TIN-MISSING", Regime.Crs, "Missing TIN: accepted only with reason A, B or C", "TIN lipsă: acceptat doar cu motivul A, B sau C", "53, 37"),
        new("SC-CONSISTENT", Regime.Crs, "The self-certification covers every jurisdiction with an indicium", "Autocertificarea acoperă toate jurisdicțiile cu indicii", "33-34, 36-37"),
        new("DL-CHANGE-90", Regime.Crs, "90 calendar days for a new self-certification or a reasonable explanation", "90 de zile calendaristice pentru o autocertificare nouă sau o explicație rezonabilă", "29, 35-36"),
        new("DL-EXPIRED", Regime.Crs, "Deadline missed: treated as resident in the original and the new jurisdiction", "Termen depășit: tratat ca rezident în jurisdicția inițială și în cea nouă", "36"),
        new("RESP-EXPLANATION", Regime.Crs, "Reasonable explanations: student, teacher or trainee, diplomat, EU official", "Explicații rezonabile: student, profesor sau stagiar, diplomat, funcționar UE", "37"),
    ];

    public static RuleDefinition Get(string id) =>
        All.FirstOrDefault(r => r.Id == id) ?? throw new KeyNotFoundException($"Unknown rule {id}");
}
