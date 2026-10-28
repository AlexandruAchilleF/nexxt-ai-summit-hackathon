using HackathonStarter.Features.Checks;
using Microsoft.Extensions.DependencyInjection;

namespace HackathonStarter.Tests.Crs;

/// <summary>The CRS agent's verdict checks (docs/PLAN.md 13.4): TIN wording, signals, rule citations, quotes.</summary>
public class CrsChecksTests
{
    private static readonly CheckEngine Engine = new(
        [new NoTinValidClaimCheck(), new SignalsNeverAlertCheck(), new RuleCitedCheck(), new EvidenceQuotedCheck()]);

    private static CheckResult Run(string id, string text, string? toolResults = null) =>
        Assert.Single(Engine.Evaluate(
            new CheckSubject(text, Facts: toolResults is null ? null : new Dictionary<string, string> { [EvidenceQuotedCheck.ToolResultsFact] = toolResults }),
            [id]).Results);

    // ─── no-tin-valid-claim ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("TIN-ul este valid.")]
    [InlineData("TIN-UL ESTE CORECT, putem închide cazul.")]
    [InlineData("Clientul are un TIN valid pentru Germania.")]
    [InlineData("Am verificat: cod fiscal valid.")]
    [InlineData("The TIN is valid.")]
    [InlineData("This is a valid TIN for Spain.")]
    [InlineData("Codul TIN este valid.")]
    [InlineData("Codul TIN e corect.")]
    [InlineData("Clientul are TIN-ul valid.")]
    [InlineData("TINul este valid.")]
    [InlineData("TINul e valid, putem închide cazul.")]
    [InlineData("The client provided valid TINs for Germany and Austria.")] // plural endings
    [InlineData("Valid TINs were declared for both jurisdictions.")]
    [InlineData("TIN valide pentru DE și AT.")]
    [InlineData("TIN-uri valide pentru DE și AT.")]
    [InlineData("The TINs are valid.")]
    public void Tin_validity_claims_fail(string text) =>
        Assert.Equal(CheckOutcome.Fail, Run("no-tin-valid-claim", text).Outcome);

    [Theory]
    [InlineData("TIN-ul are format valid.")]
    [InlineData("Formatul TIN este valid.")]
    [InlineData("The TIN has a valid format.")]
    [InlineData("valid format")]
    [InlineData("TIN-ul lipsește; motivul declarat este B.")]
    [InlineData("Formatul este valid ținând cont de regulile Germaniei.")] // "valid ținând" is not "valid TIN"
    [InlineData("- Format TIN valid (DE)")]
    [InlineData("Formatul TIN valid este 11 cifre.")]
    [InlineData("The TIN has a valid TIN format.")]
    [InlineData("Clarification needed: invalid TIN format for Italy (SC-TIN-FORMAT).")]
    [InlineData("The self-certification has an invalid TIN.")]
    [InlineData("TIN validation checks only the format.")]
    [InlineData("TIN-ul este validat doar ca format.")]
    [InlineData("Codul fiscal este validat doar ca format.")]
    [InlineData("The TINs have a valid format.")]
    [InlineData("The client has valid TINs format for DE.")]
    public void Format_wording_passes(string text) =>
        Assert.Equal(CheckOutcome.Pass, Run("no-tin-valid-claim", text).Outcome);

    [Fact]
    public void Tin_claim_finding_names_the_phrase()
    {
        var result = Run("no-tin-valid-claim", "Codul este în regulă: TIN-ul e valid.");

        var finding = Assert.Single(result.Findings);
        Assert.Equal("“TIN-ul e valid”", finding.What);
        Assert.Contains("TIN-ul e valid", finding.Excerpt);
    }

    // ─── signals-never-alert ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Salariul de la Müller Logistik GmbH este un semnal, fără alertă.")]
    [InlineData("Studiile în Franța sunt un semnal contextual; nu e un indiciu.")]
    [InlineData("The salary paid from Germany is a signal, not an alert.")]
    [InlineData("The employer is a signal that isn't an alert.")]
    [InlineData("Salariul din Germania este un semnal. Adresa din Germania este un indiciu CRS-IND-ADDR (p. 29).")]
    [InlineData("Indiciul SC-TIN-MISSING este acoperit, motivul declarat este B. Salariul din Austria este un semnal de context.")]
    [InlineData("| Semnal | Alertă |\n|--|--|\n| Salariul din Germania (semnal) | nu |")] // a table header is not a claim
    public void Signals_without_alerts_pass(string text) =>
        Assert.Equal(CheckOutcome.Pass, Run("signals-never-alert", text).Outcome);

    [Theory]
    [InlineData("Salariul din Germania este un semnal și o alertă FATCA.")]
    [InlineData("SEMNAL SI ALERTA: angajatorul din Austria.")]
    [InlineData("The employer in Austria is a signal and an indicium.")]
    [InlineData("Semnalul (studii în Italia) devine indiciu.")]
    [InlineData("Salariul din Germania este un semnal și o alertă FATCA. Pașii următori:")] // a label after the claim
    [InlineData("Salariul din Germania este un semnal și o alertă FATCA. Pașii următori:\n- cerem documente")]
    [InlineData("Salariul din Germania este un semnal și o alertă FATCA, deoarece:\n- angajatorul e german")]
    [InlineData("## Salariul din Germania: semnal și alertă FATCA")] // a heading is a claim too
    [InlineData("Salariul din Germania este un semnal și o alertă FATCA.\n\n---\n\nAm trimis scrisoarea.")] // before a rule
    public void Signals_presented_as_alerts_fail(string text) =>
        Assert.Equal(CheckOutcome.Fail, Run("signals-never-alert", text).Outcome);

    [Fact]
    public void Signal_failure_points_at_the_alert_word()
    {
        var result = Run("signals-never-alert", "Adresa e în România. Salariul din Germania este un semnal și o alertă FATCA.");

        var finding = Assert.Single(result.Findings);
        Assert.Equal("signal presented as an alert", finding.What);
        Assert.Contains("alertă FATCA", finding.Excerpt);
    }

    [Theory]
    [InlineData("Adresa din Germania este un indiciu CRS-IND-ADDR (p. 29).")]
    [InlineData("Clientul are rezidență fiscală doar în România.")]
    public void Without_a_signal_the_signal_check_does_not_apply(string text) =>
        Assert.Equal(CheckOutcome.NotApplicable, Run("signals-never-alert", text).Outcome);

    // ─── rule-cited ───────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Adresa din Germania este un indiciu CRS-IND-ADDR (p. 29).")]
    [InlineData("Telefonul cu prefix +1 este un indiciu FATCA-IND-PHONE.")]
    [InlineData("Indiciul de adresă este descris în ghid la pagina 29.")]
    [InlineData("Two indicia were found: address and phone (p.35-36).")]
    [InlineData("Am găsit un indiciu (ex. CRS-IND-ADDR). Termenul este DL-NEW-90.")]
    [InlineData("Indicia (e.g. CRS-IND-ADDR) need documents.")]
    [InlineData("| Indiciu | Regula |\n|--|--|\n| Adresa din Germania (indiciu) | CRS-IND-ADDR |")] // GFM accepts one dash per cell
    [InlineData("| Indiciu | Regula |\n|:-|-:|\n| Adresa din Germania (indiciu) | CRS-IND-ADDR |")]
    [InlineData("Am găsit două indicii:\n- Adresa din Germania (CRS-IND-ADDR)\n- Telefonul german (p. 29)")] // the list cites
    [InlineData("Am găsit două indicii:\n\n1. Adresa din Germania\n   Regula: CRS-IND-ADDR\n2. Telefonul german\n   - pagina 29")]
    [InlineData("Am găsit următoarele două indicii de rezidență fiscală:\n- Adresa (CRS-IND-ADDR)\n- Telefonul (CRS-IND-PHONE)")]
    [InlineData("**Indicii găsite:**\n\n| Element | Regula |\n| --- | --- |\n| Adresa | CRS-IND-ADDR |")] // the table cites
    [InlineData("## Indicii găsite\n\nAm verificat documentele. Adresa este în Germania (CRS-IND-ADDR, p. 29).")] // the paragraph cites
    [InlineData("Indicii găsite\n---\n- Adresa (CRS-IND-ADDR)\n- Telefonul german (p. 29)")] // a setext heading
    // A heading over an intro label, or over a subheading, is cited by what that block introduces.
    [InlineData("## Indicii identificate\n\nAm verificat documentele clientului și am găsit următoarele:\n\n- Adresa din Germania (CRS-IND-ADDR, p. 29)\n- Telefonul german (CRS-IND-PHONE, p. 29)")]
    [InlineData("## Indicii\n\nAm găsit două:\n\n- Adresa (CRS-IND-ADDR)\n- Telefonul (CRS-IND-PHONE)")]
    [InlineData("### Indicii CRS\n\nDin datele băncii rezultă:\n\n- adresă în Germania — CRS-IND-ADDR (p. 29)")]
    [InlineData("## Indicii\n\n### Adresa din Germania\n\nRegula CRS-IND-ADDR (p. 29): adresa de domiciliu este în Germania.\n\n### Telefon german\n\nRegula CRS-IND-PHONE (p. 29).")]
    [InlineData("## Indicii\nAm găsit următoarele:\n- Adresa (CRS-IND-ADDR)\n- Telefonul (CRS-IND-PHONE)")] // no blank lines
    [InlineData("## Indicii\n\nConform CRS-IND-ADDR (p. 29), am găsit:\n\n- Adresa\n- Telefonul")] // the label cites
    // A label that is a list item introduces only its own nested items, not the parent list's next items.
    [InlineData("- **Indicii:**\n  - Adresă în Germania (CRS-IND-ADDR, p. 29)\n  - Telefon german (CRS-IND-PHONE, p. 29)\n- **Semnale (fără alertă):**\n  - Salariu din Germania\n- **Termen:** 15.01.2027 (DL-NEW-90)")]
    [InlineData("1. Indicii găsite:\n   - Adresă în Germania (CRS-IND-ADDR, p. 29)\n2. Termen: 15.01.2027")]
    // A bold title line is a heading.
    [InlineData("**Indicii găsite**\n- Adresă în Germania (CRS-IND-ADDR, p. 29)")]
    [InlineData("**Indicii identificate**\n\n- Adresă în Germania — CRS-IND-ADDR (p. 29)\n- Telefon cu prefix german — CRS-IND-PHONE (p. 29)")]
    [InlineData("__Indicii găsite__\n\n1. Adresă în Germania — CRS-IND-ADDR (p. 29)")]
    public void Indicia_with_a_rule_or_page_pass(string text) =>
        Assert.Equal(CheckOutcome.Pass, Run("rule-cited", text).Outcome);

    [Theory]
    [InlineData("Adresa din Germania este un indiciu.")]
    [InlineData("Cazul CRS-2026-0001 are un indiciu de adresă.")] // a case code is not a rule
    [InlineData("The phone number is an indicium of US residence.")]
    [InlineData("Am găsit două indicii:\n- Adresa din Germania\n- Telefonul german")] // a label over an uncited list
    [InlineData("Am găsit două indicii:\n- Adresa din Germania (CRS-IND-ADDR)\n- Telefonul german")] // one item uncited
    [InlineData("Indicii găsite:\n\n| Element | Regula |\n|--|--|\n| Adresa | CRS-IND-ADDR |\n| Telefonul german | |")]
    [InlineData("## Indicii\n\nAdresa din Germania și telefonul german.")] // a heading over an uncited paragraph
    [InlineData("## Indicii găsite")] // a heading over nothing
    [InlineData("Adresa din Germania este un indiciu. Documente necesare:")] // a label after the claim
    [InlineData("Telefonul german este un indiciu. Ce urmează:\n- cerem documente")]
    [InlineData("Adresa din Germania este un indiciu, deoarece:\n- clientul locuiește la Berlin")]
    [InlineData("Adresa din Germania este un indiciu.\n\n---\n\nAm trimis scrisoarea.")] // before a horizontal rule
    [InlineData("Adresa din Germania este un indiciu.\n---")]
    [InlineData("Adresa este un indiciu CRS-IND-ADDR, motivul B. Telefonul german este un indiciu.")] // "B." ends a sentence
    [InlineData("## Indicii\n\nAm găsit două:\n\n- Adresa (CRS-IND-ADDR)\n- Telefonul german")] // heading, label, one item uncited
    [InlineData("## Indicii\nAm găsit:\n- Adresa (CRS-IND-ADDR)\n- Telefonul german")] // the same without blank lines
    [InlineData("## Indicii\n\n### Adresa din Germania\n\nClientul locuiește la Berlin.")] // a subheading over an uncited paragraph
    [InlineData("## Indicii\n\nAm găsit următoarele:")] // a label over nothing
    [InlineData("- **Indicii:**\n  - Adresă în Germania (CRS-IND-ADDR)\n  - Telefon german\n- **Termen:** 15.01.2027")]
    [InlineData("**Indicii găsite**\n- Adresa din Germania")] // a bold title over an uncited list
    [InlineData("**Adresa din Germania este un indiciu.**\n\nTelefonul (CRS-IND-PHONE).")] // a bold sentence is not a title
    public void Indicia_without_a_citation_fail(string text) =>
        Assert.Equal(CheckOutcome.Fail, Run("rule-cited", text).Outcome);

    [Fact]
    public void Every_sentence_needs_its_own_citation()
    {
        var result = Run("rule-cited", "Adresa este un indiciu CRS-IND-ADDR (p. 29). Telefonul este tot un indiciu.");

        Assert.Equal(CheckOutcome.Fail, result.Outcome);
        var finding = Assert.Single(result.Findings);
        Assert.Contains("Telefonul este tot un indiciu", finding.Excerpt);
    }

    [Fact]
    public void Headings_and_labels_are_cited_by_what_they_introduce_and_table_headers_are_not_claims()
    {
        const string text = """
            ## Indicii găsite

            Indicii:
            - Adresa în Germania (CRS-IND-ADDR, p. 29)
            - Telefonul german este un indiciu (CRS-IND-PHONE)

            | Indiciu | Regula | Pagina |
            | --- | --- | --- |
            | Adresa în Germania | CRS-IND-ADDR | p. 29 |
            """;

        Assert.Equal(CheckOutcome.Pass, Run("rule-cited", text).Outcome);
    }

    [Theory]
    [InlineData("Nu am găsit niciun indiciu.")]
    [InlineData("No indicia: the client is resident only in Romania.")]
    [InlineData("Clientul are rezidență fiscală doar în România.")]
    public void Without_an_indicium_the_citation_check_does_not_apply(string text) =>
        Assert.Equal(CheckOutcome.NotApplicable, Run("rule-cited", text).Outcome);

    // ─── evidence-quoted ──────────────────────────────────────────────────────────────────────────

    // A verified quote at the top of an output, one in an evidence array (property names in any case), one inside an
    // output kept as a JSON string; an unverified quote; and a quote that only the model proposed (input).
    private const string ToolResults = """
        [
          { "tool": "recordEvidence", "input": { "quote": "Adresa: Hauptstraße 12, 10115 Berlin" },
            "output": { "verified": true, "quote": "Adresa: Hauptstraße 12, 10115 Berlin" } },
          { "tool": "getCase", "input": { "caseCode": "CRS-2026-0001" },
            "output": { "evidence": [ { "Quote": "Locul nașterii: München, Germania", "Verified": true },
                                      { "quote": "Venit anual din Germania", "verified": false } ] } },
          { "tool": "readDocument", "input": { "quote": "Text inventat de model" },
            "output": "{\"page\": 2, \"quote\": \"Rezidență fiscală: Germania\", \"verified\": true}" }
        ]
        """;

    [Fact]
    public void Verified_quotes_are_collected_from_every_output()
    {
        Assert.Equal(
            new[] { "Adresa: Hauptstraße 12, 10115 Berlin", "Locul nașterii: München, Germania", "Rezidență fiscală: Germania" },
            EvidenceQuotedCheck.VerifiedQuotes(ToolResults));
    }

    [Theory]
    [InlineData("Documentul spune „Adresa: Hauptstraße 12, 10115 Berlin”.")]
    [InlineData("Formularul arată „locul nasterii: munchen,\n   germania”.")] // case, diacritics, whitespace
    [InlineData("The form says “Hauptstraße 12, 10115 Berlin” and \"Rezidență fiscală: Germania\".")] // part of a verified quote
    [InlineData("Pe pagina 2: „Rezidență fiscală: Germania (bifat)”.")] // contains a verified quote
    [InlineData("Documentul spune ”Adresa: Hauptstraße 12, 10115 Berlin”.")]
    [InlineData("Documentul spune „Adresa: Hauptstraße 12, 10115 Berlin\".")]
    public void Verified_quotations_pass(string text) =>
        Assert.Equal(CheckOutcome.Pass, Run("evidence-quoted", text, ToolResults).Outcome);

    [Fact]
    public void Unverified_quotations_fail_and_are_listed()
    {
        const string text = "Clientul declară „Venit anual din Germania”, apoi „Text inventat de model” și \"Clientul locuiește în SUA\". "
                            + "Adresa: „Adresa: Hauptstraße 12, 10115 Berlin”.";

        var result = Run("evidence-quoted", text, ToolResults);

        Assert.Equal(CheckOutcome.Fail, result.Outcome);
        Assert.Equal(3, result.Findings.Count);
        Assert.Contains(result.Findings, f => f.Excerpt!.Contains("Venit anual"));
        Assert.Contains(result.Findings, f => f.Excerpt!.Contains("Text inventat"));
        Assert.Contains(result.Findings, f => f.Excerpt!.Contains("locuiește în SUA"));
    }

    [Theory]
    [InlineData("Răspunsul: „Text inventat complet\" și apoi „Adresa: Hauptstraße 12, 10115 Berlin”.")] // „ closed by "
    [InlineData("Răspunsul: “Text inventat complet\" și apoi „Adresa: Hauptstraße 12, 10115 Berlin”.")] // “ closed by "
    [InlineData("Documentul spune ”Text inventat complet” și „Adresa: Hauptstraße 12, 10115 Berlin”.")] // ” on both sides
    public void Quotations_with_mixed_marks_are_checked(string text)
    {
        var result = Run("evidence-quoted", text, ToolResults);

        Assert.Equal(CheckOutcome.Fail, result.Outcome);
        Assert.Contains("Text inventat complet", Assert.Single(result.Findings).Excerpt);
    }

    [Fact]
    public void Unverified_quotation_evidence_is_masked()
    {
        var result = Run("evidence-quoted", "Extrasul arată „IBAN RO49AAAA1B31007593840000 al clientului”.", ToolResults);

        var finding = Assert.Single(result.Findings);
        Assert.DoesNotContain("RO49AAAA1B31007593840000", finding.Excerpt);
        Assert.Contains("[IBAN-1]", finding.Excerpt);
    }

    [Fact]
    public void A_short_verified_quote_does_not_vouch_for_a_sentence()
    {
        const string toolResults = """[{ "tool": "recordEvidence", "input": {}, "output": { "quote": "Berlin", "verified": true } }]""";

        Assert.Equal(CheckOutcome.Fail, Run("evidence-quoted", "Clientul scrie „Locuiesc acum în Berlin”.", toolResults).Outcome);
    }

    [Fact]
    public void Tool_results_that_are_not_json_fail_the_quotations()
    {
        var result = Run("evidence-quoted", "Documentul spune „Adresa: Hauptstraße 12, 10115 Berlin”.", "not json");

        Assert.Equal(CheckOutcome.Fail, result.Outcome);
        Assert.Single(result.Findings);
    }

    [Theory]
    [InlineData("Documentul spune „Adresa: Hauptstraße 12, 10115 Berlin”.", null)] // no fact
    [InlineData("Adresa din Germania este un indiciu CRS-IND-ADDR (p. 29).", ToolResults)] // no quotation
    [InlineData("Țara bifată este „DE”.", ToolResults)] // too short to be a quotation
    public void Without_tool_results_or_quotations_the_quote_check_does_not_apply(string text, string? toolResults) =>
        Assert.Equal(CheckOutcome.NotApplicable, Run("evidence-quoted", text, toolResults).Outcome);

    // ─── Registration ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AddChecks_registers_the_crs_checks()
    {
        using var provider = new ServiceCollection().AddChecks().BuildServiceProvider();
        var engine = provider.GetRequiredService<CheckEngine>();

        foreach (var id in new[] { "no-tin-valid-claim", "signals-never-alert", "rule-cited", "evidence-quoted" })
            Assert.NotNull(engine.Find(id));
        Assert.IsType<NoTinValidClaimCheck>(engine.Find("no-tin-valid-claim")); // whole words, not plain substrings
    }
}
