using System.Globalization;
using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Rules;
using HackathonStarter.Services;

namespace HackathonStarter.Features.Cases;

/// <summary>
/// Outcome and reminder texts written by code from templates (EN and RO), with the rule ID and guide page, so a model
/// never words a legal consequence. Codes only: no names, phones, IBANs or addresses.
/// </summary>
public static class CaseTexts
{
    // Needs tzdata (see DeadlineCalculator): dates in letters are shown in Bucharest time.
    private static readonly TimeZoneInfo Bucharest = TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest");

    public static string LocalDate(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Bucharest).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    private static string Page(string ruleId) => RuleCatalog.Get(ruleId).GuidePage;

    private static string List(IEnumerable<string> jurisdictions, string none)
    {
        var list = jurisdictions.ToList();
        return list.Count == 0 ? none : string.Join(", ", list);
    }

    /// <param name="indicia">CRS indicia only: the deadline that expired is the CRS one.</param>
    /// <param name="fatcaFollowUp">The FATCA-only case that continues the FATCA indicia of a Both case (design choice).</param>
    public static (string En, string Ro) ExpiredNew(IReadOnlyList<string> original, IReadOnlyList<string> indicia, string? fatcaFollowUp = null) =>
        ($"Expired: no valid self-certification or confirmation within 90 days. The account holder is treated as resident in the jurisdiction of the original self-certification ({List(original, "none on file")}) and in {List(indicia, "-")} (DL-EXPIRED-NEW, p. {Page("DL-EXPIRED-NEW")}).{FollowUpEn(fatcaFollowUp)}",
         $"Expirat: nicio autocertificare validă sau confirmare în 90 de zile. Titularul este tratat ca rezident în jurisdicția din autocertificarea inițială ({List(original, "niciuna la dosar")}) și în {List(indicia, "-")} (DL-EXPIRED-NEW, p. {Page("DL-EXPIRED-NEW")}).{FollowUpRo(fatcaFollowUp)}");

    public static (string En, string Ro) ExpiredPreexisting(IReadOnlyList<string> indicia, string? fatcaFollowUp = null) =>
        ($"Expired: no new self-certification and documentary evidence by the deadline. The account holder is treated as resident at least in {List(indicia, "-")} (DL-EXPIRED-PRE, p. {Page("DL-EXPIRED-PRE")}).{FollowUpEn(fatcaFollowUp)}",
         $"Expirat: nicio autocertificare nouă și niciun document justificativ până la termen. Titularul este tratat ca rezident cel puțin în {List(indicia, "-")} (DL-EXPIRED-PRE, p. {Page("DL-EXPIRED-PRE")}).{FollowUpRo(fatcaFollowUp)}");

    private static string FollowUpEn(string? code) => code is null ? ""
        : $" The FATCA indicia continue in case {code}, with no deadline in the guide (FATCA-SC, p. {Page("FATCA-SC")}; design choice).";

    private static string FollowUpRo(string? code) => code is null ? ""
        : $" Indiciile FATCA continuă în cazul {code}, fără termen în ghid (FATCA-SC, p. {Page("FATCA-SC")}; alegere de design).";

    public static (string En, string Ro) ClosedOnExplanation(ExplanationCategory category, IReadOnlyList<string> jurisdictions) =>
        ($"Closed: reasonable explanation accepted ({CategoryEn(category)}) for {List(jurisdictions, "-")}; the account is not reportable for it (RESP-EXPLANATION, p. {Page("RESP-EXPLANATION")}).",
         $"Închis: explicație rezonabilă acceptată ({CategoryRo(category)}) pentru {List(jurisdictions, "-")}; contul nu este raportabil pentru aceasta (RESP-EXPLANATION, p. {Page("RESP-EXPLANATION")}).");

    public static (string En, string Ro) ClosedOnSelfCertification(Regime regime, IReadOnlyList<string> reportableFor)
    {
        if (regime == Regime.Fatca)
            return ($"Closed: new FATCA self-certification received; reportable for {List(reportableFor, "no jurisdiction")} (FATCA-SC, p. {Page("FATCA-SC")}).",
                    $"Închis: autocertificare FATCA nouă primită; raportabil pentru {List(reportableFor, "nicio jurisdicție")} (FATCA-SC, p. {Page("FATCA-SC")}).");
        var fatcaEn = regime == Regime.Both ? $" It also answers the FATCA indicia (FATCA-SC, p. {Page("FATCA-SC")})." : "";
        var fatcaRo = regime == Regime.Both ? $" Răspunde și indiciilor FATCA (FATCA-SC, p. {Page("FATCA-SC")})." : "";
        return ($"Closed: the new self-certification covers every indicium; reportable for {List(reportableFor, "no jurisdiction")} (SC-CONSISTENT, p. {Page("SC-CONSISTENT")}).{fatcaEn}",
                $"Închis: autocertificarea nouă acoperă toate indiciile; raportabil pentru {List(reportableFor, "nicio jurisdicție")} (SC-CONSISTENT, p. {Page("SC-CONSISTENT")}).{fatcaRo}");
    }

    /// <summary>The reminder the watcher drafts (Romanian, the default language of the bank's letters). An operator approves it.</summary>
    public static string ReminderBodyRo(DateTime dueAt, IReadOnlyList<string> documents)
    {
        var lines = string.Join("\n", documents.Select(d => "- " + Lang.Translations.GetValueOrDefault(d, d)));
        return "Bună ziua,\n\n" +
               $"Vă reamintim că, pentru actualizarea rezidenței fiscale, așteptăm de la dumneavoastră până la {LocalDate(dueAt)}:\n" +
               lines + "\n\n" +
               "Dacă le-ați trimis deja, vă rugăm să ignorați acest mesaj.\n\n" +
               "Cu stimă,\nEchipa de conformitate fiscală";
    }

    public static string DefaultCollectionSteps(ClientResponse response, ClientRequest? request, string approvedBy, ConfirmationChannel channel)
    {
        var invariant = CultureInfo.InvariantCulture;
        var sent = request?.ApprovedAt is { } at ? string.Create(invariant, $"Request sent via {request.Channel} on {at:yyyy-MM-dd HH:mm} UTC. ") : "";
        var category = response.Kind == ResponseKind.ReasonableExplanation ? $" ({response.Category})" : "";
        return sent + string.Create(invariant, $"Response received via {response.Channel} on {response.ReceivedAt:yyyy-MM-dd HH:mm} UTC: {response.Kind}{category}. ") +
               $"Re-evaluated by code before closing. Closing approved by {approvedBy} ({channel}).";
    }

    private static string CategoryEn(ExplanationCategory category) => category switch
    {
        ExplanationCategory.Student => "student",
        ExplanationCategory.TeacherOrTrainee => "teacher or trainee",
        ExplanationCategory.Diplomat => "diplomat",
        ExplanationCategory.EuOfficial => "EU official",
        _ => category.ToString(),
    };

    private static string CategoryRo(ExplanationCategory category) => category switch
    {
        ExplanationCategory.Student => "student",
        ExplanationCategory.TeacherOrTrainee => "profesor sau stagiar",
        ExplanationCategory.Diplomat => "diplomat",
        ExplanationCategory.EuOfficial => "funcționar UE",
        _ => category.ToString(),
    };
}
