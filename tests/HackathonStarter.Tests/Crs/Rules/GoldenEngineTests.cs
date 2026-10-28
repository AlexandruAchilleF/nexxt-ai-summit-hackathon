using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Tin;
using Microsoft.Extensions.DependencyInjection;
using AcceptedExplanationSnapshot = HackathonStarter.Features.Clients.AcceptedExplanationSnapshot;
using AccountType = HackathonStarter.Features.Clients.AccountType;
using ExplanationCategory = HackathonStarter.Features.Clients.ExplanationCategory;
using ResponseKind = HackathonStarter.Features.Cases.ResponseKind;
using static HackathonStarter.Tests.Crs.RuleEngine.Subjects;

namespace HackathonStarter.Tests.Crs.RuleEngine;

/// <summary>
/// Golden cases, engine layer (docs/GOLDEN-CASES.md). The expectations are R5's canonical data (<see cref="GoldenCases"/>),
/// run through <see cref="ICrsRuleEngine"/> wired as in Program.cs, with R3's TIN format: hits compared exactly with their
/// coverage, Cleared as a superset. <see cref="EngineCases"/> are R1's RuleSubject fixtures and must never contradict them.
/// </summary>
public class GoldenEngineTests
{
    private static readonly CrsRuleEngine Engine = new(new FakeTinFormat());
    private static readonly ICrsRuleEngine Wired = new ServiceCollection().AddRules().AddTin().BuildServiceProvider()
        .GetRequiredService<ICrsRuleEngine>();

    public static TheoryData<string> CanonicalIds() => [.. GoldenCases.Ready(GoldenLayer.Engine).Select(c => c.Id)];

    /// <summary>What EvaluationService and ChangeService call for the case's change.</summary>
    private static RuleOutcome Run(GoldenCase c) => c.Change!.Kind == ChangeKind.SelfCertSubmitted
        ? Wired.EvaluateSelfCertification(c.After!, c.Change.SelfCertification!)
        : Wired.Evaluate(c.Before!, c.After!, c.Change.Kind);

    private static void AssertMatches(IReadOnlyList<GoldenHit> indicia, IReadOnlyList<GoldenHit> cleared,
        IReadOnlyList<string>? reportableFor, RuleOutcome o)
    {
        Assert.Equal(indicia.Select(h => (h.RuleId, h.Jurisdiction ?? "*", h.CoveredBy)).Order(),
            o.Indicia.Select(h => (h.RuleId, h.Jurisdiction, h.CoveredBy)).Order());
        foreach (var e in indicia.Where(e => e.SourceField is not null))
            Assert.Equal(e.SourceField, o.Indicia.Single(h => h.RuleId == e.RuleId && h.Jurisdiction == e.Jurisdiction).SourceField);
        foreach (var e in cleared)
            Assert.Contains(o.Cleared, x => x.RuleId == e.RuleId && (e.Jurisdiction is null || x.Jurisdiction == e.Jurisdiction));
        if (reportableFor is not null) Assert.Equal(reportableFor.Order(), o.ReportableFor.Order());
    }

    [Theory]
    [MemberData(nameof(CanonicalIds))]
    public void Canonical_golden_case(string id)
    {
        var c = GoldenCases.Get(id);
        var o = Run(c);

        Assert.Equal(c.ExpectedDecision, o.Decision);
        Assert.Equal(c.ExpectedRegime, o.Regime);
        AssertMatches(c.ExpectedIndicia, c.ExpectedClearedRules, c.ExpectedReportableFor, o);
        Assert.All(o.Indicia.Concat(o.Cleared), h => Assert.Equal(RuleCatalog.Get(h.RuleId).GuidePage, h.GuidePage));
    }

    [Theory]
    [InlineData("4")]
    [InlineData("18")]
    public void Canonical_golden_response_reevaluates_as_the_card_says(string id)
    {
        var c = GoldenCases.Get(id);
        var r = c.Response!;
        // Case 18: R5's snapshot after the answer; case 4: After with the new self-certification stored as the current one.
        var answered = r.Kind == ResponseKind.ReasonableExplanation
            ? GoldenCases.TeodoraAfterAnswer
            : c.After! with { SelfCertification = r.SelfCertification };

        var o = Wired.EvaluateCurrent(answered);

        Assert.Equal(r.ExpectedDecision, o.Decision);
        AssertMatches(r.ExpectedIndicia, [], r.ExpectedReportableFor, o);
    }

    [Fact]
    public void Engine_cases_never_contradict_the_canonical_golden_cases()
    {
        foreach (var golden in GoldenCases.Ready(GoldenLayer.Engine))
        {
            if (EngineCases.All.SingleOrDefault(e => e.Id == golden.Id) is not { } mine) continue;
            Assert.True(golden.ExpectedDecision == mine.Expected, golden.Id);
            Assert.Equal(golden.ExpectedIndicia.Select(h => (h.RuleId, h.Jurisdiction ?? "*", h.CoveredBy)).Order(),
                mine.Hits.Select(h => (h.RuleId, h.Jurisdiction, h.CoveredBy)).Order());
            Assert.Equal(golden.ExpectedRegime, mine.Regime);
        }
    }

    [Theory]
    [MemberData(nameof(EngineCases.Ids), MemberType = typeof(EngineCases))]
    public void Engine_case_on_the_rule_subject_path(string id)
    {
        var c = EngineCases.Get(id);
        var outcome = Engine.Evaluate(c.Before, c.After, c.Change);

        Assert.Equal(c.Expected, outcome.Decision);
        Assert.Equal(
            c.Hits.Select(h => (h.RuleId, h.Jurisdiction, h.CoveredBy)).Order(),
            outcome.Indicia.Select(h => (h.RuleId, h.Jurisdiction, h.CoveredBy)).Order());
        foreach (var expected in c.Hits.Where(h => h.SourceField is not null))
            Assert.Equal(expected.SourceField, outcome.Indicia.Single(h => h.RuleId == expected.RuleId && h.Jurisdiction == expected.Jurisdiction).SourceField);
        foreach (var (ruleId, jurisdiction) in c.ClearedAtLeast)
            Assert.Contains(outcome.Cleared, x => x.RuleId == ruleId && x.Jurisdiction == jurisdiction);
        if (c.ReportableFor is not null) Assert.Equal(c.ReportableFor, outcome.ReportableFor);
        Assert.Equal(c.Regime, outcome.Regime);
        var said = string.Join(" ", outcome.Cleared.Select(x => x.ExplanationRo).Prepend(outcome.ExplanationRo));
        foreach (var phrase in c.Says ?? []) Assert.Contains(phrase, said);

        Assert.Equal(CrsRuleEngine.Version, outcome.EngineVersion);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ExplanationRo));
        Assert.False(string.IsNullOrWhiteSpace(outcome.Explanation));
        Assert.All(outcome.Indicia.Concat(outcome.Cleared), h =>
        {
            Assert.True(RuleCatalog.Contains(h.RuleId), h.RuleId);
            Assert.Equal(RuleCatalog.Get(h.RuleId).GuidePage, h.GuidePage);
            Assert.False(string.IsNullOrWhiteSpace(h.ExplanationRo), h.RuleId);
            Assert.False(string.IsNullOrWhiteSpace(h.Explanation), h.RuleId);
        });
    }

    [Fact]
    public void Maria_opens_a_crs_case_with_both_indicia_on_the_new_account()
    {
        var outcome = Engine.Evaluate(EngineCases.MariaBefore, EngineCases.MariaAfter, new RuleChange("AddressChanged"));

        Assert.Equal(Regime.Crs, outcome.Regime);
        Assert.Equal(2, outcome.Uncovered.Count());
        Assert.Contains(outcome.Indicia, h => h.RuleId == RuleIds.CrsAddress && h.SourceField == "Addresses/Residence");
        Assert.Contains(outcome.Indicia, h => h.RuleId == RuleIds.CrsPhone && h.SourceField == "Phones");
        // New account: the indicia are our "reason to know" reading, and the explanation says so.
        Assert.All(outcome.Indicia, h => Assert.Equal(RuleBasis.Interpretation, h.Basis));
        Assert.All(outcome.Indicia, h => Assert.Contains("GEN-NEW-ACCOUNT", h.ExplanationRo));
        Assert.Contains("Cont nou", outcome.ExplanationRo);
        Assert.Empty(outcome.ReportableFor);
    }

    [Fact]
    public void Maria_after_response_is_reportable_for_germany()
    {
        var outcome = Engine.Evaluate(EngineCases.MariaAfter, EngineCases.MariaAfterResponse, new RuleChange("SelfCertSubmitted"));

        Assert.Equal(Decision.Compliant, outcome.Decision);
        Assert.Null(outcome.Regime);
        Assert.Contains("SC-CONSISTENT", outcome.ExplanationRo);
        Assert.All(outcome.Indicia, h => Assert.Contains("exista și înainte", h.ExplanationRo));
    }

    [Fact]
    public void Maria_response_form_has_every_validity_element_and_a_format_valid_german_tin()
    {
        var outcome = Engine.EvaluateSelfCertification(EngineCases.MariaAfter, EngineCases.MariaResponseForm);

        Assert.Equal(Decision.NoCase, outcome.Decision);
        Assert.Empty(outcome.Indicia);
        Assert.Equal(RuleBasis.Verified, Assert.Single(outcome.Cleared, x => x.RuleId == RuleIds.SelfCertValid).Basis);
        Assert.Contains("format valid", Assert.Single(outcome.Cleared, x => x.RuleId == RuleIds.TinFormat).ExplanationRo);
        Assert.Equal(["DE"], outcome.ReportableFor);
    }

    [Fact]
    public void Standing_order_from_a_deposit_account_is_cleared_with_the_reason()
    {
        var outcome = Engine.Evaluate(EngineCases.Get("5a").Before, EngineCases.Get("5a").After, new RuleChange("StandingOrderCreated"));
        var cleared = Assert.Single(outcome.Cleared, x => x.RuleId == RuleIds.CrsStandingOrder);

        Assert.Contains("cont de depozit", cleared.ExplanationRo);
        Assert.Contains("depository account", cleared.Explanation);
        Assert.Equal("StandingOrders", cleared.SourceField);
    }

    [Fact]
    public void Golden_5c_us_standing_order_from_a_deposit_account_is_fatca_and_cleared_for_crs_on_the_guide_exception_first()
    {
        var outcome = Run(GoldenCases.Get("5c"));

        Assert.Equal(Regime.Fatca, outcome.Regime);
        Assert.Contains("excepția CRS", Assert.Single(outcome.Indicia).ExplanationRo); // FATCA inherits no CRS exception
        var cleared = Assert.Single(outcome.Cleared, x => x.RuleId == RuleIds.CrsStandingOrder);
        // Decision Î12: the guide's reason (p. 29 lit. d) first, verified; "the US is not CRS" after it, as our reading.
        Assert.Equal(("US", RuleBasis.Verified), (cleared.Jurisdiction, cleared.Basis));
        Assert.StartsWith("Ordin permanent către SUA (US) dintr-un cont de depozit", cleared.ExplanationRo);
        AssertInOrder(cleared.ExplanationRo, "exceptat la CRS (pct. 12 lit. d", "în plus, SUA nu e jurisdicție CRS raportabilă", "interpretare");
        AssertInOrder(cleared.Explanation, "excepted under CRS (12(d)", "besides, the US is not a CRS reportable jurisdiction", "interpretation");
    }

    [Fact]
    public void Golden_14_us_phone_next_to_a_romanian_phone_is_cleared_for_crs_on_the_romanian_phone_first()
    {
        var outcome = Run(GoldenCases.Get("14"));

        Assert.Contains("excepția CRS nu se aplică", Assert.Single(outcome.Indicia, h => h.RuleId == RuleIds.FatcaPhone).ExplanationRo);
        var cleared = Assert.Single(outcome.Cleared, x => x.RuleId == RuleIds.CrsPhone);
        Assert.Equal(("US", RuleBasis.Verified), (cleared.Jurisdiction, cleared.Basis));
        Assert.StartsWith("Telefon din SUA (US), dar există și un telefon actual în România", cleared.ExplanationRo);
        AssertInOrder(cleared.ExplanationRo, "nu e indiciu CRS (pct. 12 lit. c)", "în plus, SUA nu e jurisdicție CRS raportabilă", "interpretare");
    }

    [Fact]
    public void Golden_7_says_the_place_of_birth_was_checked_and_is_not_the_us()
    {
        var outcome = Run(GoldenCases.Get("7"));

        var pob = Assert.Single(outcome.Cleared, x => x.RuleId == RuleIds.FatcaBirthPlace);
        // A new FATCA account (2018): the indicia list read as "reason to know" (GEN-NEW-ACCOUNT), so our reading.
        Assert.Equal(("US", "BirthCountry", RuleBasis.Interpretation), (pob.Jurisdiction, pob.SourceField, pob.Basis));
        Assert.Contains("România (RO)", pob.ExplanationRo);
        Assert.Contains("Romania (RO)", pob.Explanation);
        Assert.DoesNotContain(outcome.Indicia, h => h.RuleId == RuleIds.FatcaBirthPlace);
    }

    [Fact]
    public void Golden_17b_an_accepted_explanation_does_not_cover_an_indicium_on_a_preexisting_account()
    {
        // Decision Î11 (guide p. 29, 30, 35, 37): a preexisting account closes on a new self-certification and documentary
        // evidence; the reasonable explanation is a new-account route. R5's case 17b, plus an accepted student explanation.
        var c = GoldenCases.Get("17b");
        var explained = c.After! with
        {
            AcceptedExplanations = [new AcceptedExplanationSnapshot("ES", ExplanationCategory.Student, "CRS-2026-0017", c.ReceivedAt!.Value)],
        };

        foreach (var o in new[] { Wired.Evaluate(c.Before!, explained, c.Change!.Kind), Wired.EvaluateCurrent(explained) })
        {
            Assert.Equal((Decision.Clarification, Regime.Crs), (o.Decision, o.Regime));
            var hit = Assert.Single(o.Indicia);
            Assert.Equal((RuleIds.CrsAddress, "ES", Coverage.None, RuleBasis.Verified), (hit.RuleId, hit.Jurisdiction, hit.CoveredBy, hit.Basis));
            Assert.Contains("explicația acceptată nu acoperă indiciul", hit.ExplanationRo);
            Assert.Contains("the accepted explanation does not cover the indicium", hit.Explanation);
            Assert.DoesNotContain(o.Cleared, x => x.RuleId == RuleIds.Explanation);
            Assert.Contains("Cont preexistent", o.ExplanationRo);
            Assert.Empty(o.ReportableFor);
        }

        // The same explanation on a new account still covers it (case 18 shape): the restriction is per regime and account.
        var newAccount = explained with { Accounts = [.. explained.Accounts.Select(a => a with { OpenedOn = new DateOnly(2019, 3, 14) })] };
        Assert.Equal(Decision.Compliant, Wired.EvaluateCurrent(newAccount).Decision);
    }

    private static void AssertInOrder(string text, params string[] parts)
    {
        var at = 0;
        foreach (var part in parts)
        {
            var i = text.IndexOf(part, at, StringComparison.Ordinal);
            Assert.True(i >= 0, $"\"{part}\" not found in order in: {text}");
            at = i + part.Length;
        }
    }

    [Fact]
    public void Andrei_german_phone_is_cleared_because_of_the_romanian_phone()
    {
        var c = EngineCases.Get("13");
        var cleared = Assert.Single(Engine.Evaluate(c.Before, c.After, c.Change).Cleared, x => x.RuleId == RuleIds.CrsPhone);
        Assert.Contains("telefon actual în România", cleared.ExplanationRo);
    }

    [Fact]
    public void Cluj_to_iasi_explains_the_new_address_is_in_romania()
    {
        var c = EngineCases.Get("15");
        var outcome = Engine.Evaluate(c.Before, c.After, c.Change);

        var domestic = Assert.Single(outcome.Cleared);
        Assert.Equal(RuleIds.Domestic, domestic.RuleId);
        Assert.Contains("Noua adresă e în România", domestic.ExplanationRo);
        Assert.Equal(RuleBasis.Interpretation, domestic.Basis);
    }

    [Fact]
    public void Preexisting_account_indicia_are_verified_law_not_interpretation()
    {
        var c = EngineCases.Get("17b");
        var hit = Assert.Single(Engine.Evaluate(c.Before, c.After, c.Change).Indicia);
        Assert.Equal(RuleBasis.Verified, hit.Basis);
        Assert.DoesNotContain("GEN-NEW-ACCOUNT", hit.ExplanationRo);
    }

    [Fact]
    public void Standing_order_from_a_preexisting_custodial_account_is_verified_law()
    {
        var custodial = Account(AccountType.Custodial, new DateOnly(2014, 3, 1), "RO88RZBR0000069004471852");
        var before = RoResident().WithAccounts(custodial);
        var after = RoResident().WithAccounts(custodial.WithOrder("DE02700100800918273645", "DE"));

        var hit = Assert.Single(Engine.Evaluate(before, after, new RuleChange("StandingOrderCreated")).Indicia);
        Assert.Equal((RuleIds.CrsStandingOrder, RuleBasis.Verified), (hit.RuleId, hit.Basis));
    }

    [Fact]
    public void Golden_12_in_care_of_is_also_a_mailing_address_and_keeps_the_undocumented_account_route()
    {
        var outcome = Run(GoldenCases.Get("12"));

        var co = Assert.Single(outcome.Indicia, h => h.RuleId == RuleIds.CrsInCareOf);
        Assert.Contains("cont nedocumentat", co.ExplanationRo);
        Assert.Equal(RuleBasis.Interpretation, co.Basis); // the old Cluj address is on file, but not current (Q6)
        // Decision Î10: a c/o address is also the current mailing address (p. 29 lit. b): our reading, said as such.
        var mail = Assert.Single(outcome.Indicia, h => h.RuleId == RuleIds.CrsMailing);
        Assert.Equal(("IT", "Addresses/InCareOf", RuleBasis.Interpretation), (mail.Jurisdiction, mail.SourceField, mail.Basis));
        Assert.Contains("Î10", mail.ExplanationRo);
        // The c/o route of p. 31 (paper search or self-certification, otherwise undocumented), not III.B pct. 19.
        Assert.Contains("registrul pe hârtie", outcome.ExplanationRo);
        Assert.DoesNotContain("pct. 19", outcome.ExplanationRo);
        Assert.Contains("valoare mică", Assert.Single(outcome.Cleared, x => x.RuleId == RuleIds.FatcaInCareOf).ExplanationRo);
    }

    [Fact]
    public void Student_explanation_covers_the_indicium_without_making_germany_reportable()
    {
        var c = EngineCases.Get("18-explained");
        var outcome = Engine.Evaluate(c.Before, c.After, c.Change);

        Assert.Empty(outcome.ReportableFor);
        Assert.Contains("Student", Assert.Single(outcome.Cleared, x => x.RuleId == RuleIds.Explanation).ExplanationRo);
    }
}

/// <summary>R3's ITinFormat stand-in for the golden cases: Italian codice fiscale length (16), German IdNr (11 digits).</summary>
internal sealed class FakeTinFormat : ITinFormat
{
    public TinCheck Check(string country, string tin) => country switch
    {
        "IT" => new(country, tin, tin.Length == 16, TinCheckSource.LocalRules, ""),
        "DE" => new(country, tin, tin.Length == 11 && tin.All(char.IsDigit), TinCheckSource.LocalRules, ""),
        _ => new(country, tin, false, TinCheckSource.NotChecked, ""),
    };

    public bool? IssuesTinToAllResidents(string country) => null;
}
