using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Rules;

namespace HackathonStarter.Tests.Crs;

/// <summary>
/// Deadlines as pure code (docs/PLAN.md 9.2, docs/GOLDEN-CASES.md section 1). Owner: R2. No clock, no database:
/// every input is a fixed UTC instant.
/// </summary>
public class DeadlineCalculatorTests
{
    /// <summary>GOLDEN-CASES.md section 1: the default moment of a change (09:00 in Bucharest).</summary>
    private static readonly DateTime ChangeAt = Utc("2026-10-28T07:00:00");
    private static readonly DateOnly NewAccount = new(2020, 3, 1);
    private static readonly DateOnly PreexistingAccount = new(2014, 3, 1);

    private readonly DeadlineCalculator _calculator = new();

    [Fact]
    public void New_crs_account_is_due_90_days_after_the_bank_knew_with_a_reminder_5_days_before()
    {
        var result = _calculator.Compute(new DeadlineInput(ChangeAt, Regime.Crs, NewAccount));

        Assert.Equal("DL-NEW-90", result.RuleId);
        Assert.False(result.Preexisting);
        Assert.Equal(Utc("2027-01-26T07:00:00"), result.DueAt);
        Assert.Equal(Utc("2027-01-21T07:00:00"), result.RemindAt);
        Assert.Equal(DateTimeKind.Utc, result.DueAt!.Value.Kind);
    }

    /// <summary>
    /// DL-PRE-YEAR: the later of 31 Dec 23:59:59 Europe/Bucharest of the year the bank knew, and KnownAt + 90 days
    /// (PLAN 9.2: our interpretation for electronic indicia, CPF Annex 1 III.B(16)). Reminder 5 days before.
    /// </summary>
    [Theory]
    [InlineData("2026-03-10T08:00:00", "2026-12-31T21:59:59")] // golden case 17b: March, the year end wins (UTC+2 in winter)
    [InlineData("2026-10-28T07:00:00", "2027-01-26T07:00:00")] // golden case 12: +90 days passes the year end
    [InlineData("2026-11-20T07:00:00", "2027-02-18T07:00:00")] // late in the year: +90 days wins
    [InlineData("2026-12-31T22:30:00", "2027-12-31T21:59:59")] // already 1 Jan 2027 in Bucharest: the year end is 2027's
    public void Preexisting_crs_account_is_due_on_the_later_of_year_end_and_90_days(string knownAt, string dueAt)
    {
        var result = _calculator.Compute(new DeadlineInput(Utc(knownAt), Regime.Crs, PreexistingAccount));

        Assert.Equal("DL-PRE-YEAR", result.RuleId);
        Assert.True(result.Preexisting);
        Assert.True(result.DesignChoice, "For electronic indicia, the later-of reading is our interpretation (CPF Annex 1 III.B(16)).");
        Assert.Equal(Utc(dueAt), result.DueAt);
        Assert.Equal(Utc(dueAt).AddDays(-5), result.RemindAt);
    }

    [Fact]
    public void Preexisting_explanation_distinguishes_electronic_indicia_from_the_residence_address_test()
    {
        var result = _calculator.Compute(new DeadlineInput(ChangeAt, Regime.Crs, PreexistingAccount));

        Assert.Contains("electronic", result.Explanation);
        Assert.Contains("our interpretation", result.Explanation);
        Assert.Contains("Fiscal Procedure Code, Annex 1, Section III, subsection B, point 16", result.Explanation);
        Assert.Contains("OECD Commentary on Section III, paragraph 16", result.Explanation);
        Assert.Contains("residence address test", result.Explanation);
        Assert.Contains("Annex 2, point 1", result.Explanation);
        Assert.Contains("law", result.Explanation);
        Assert.Contains("căutarea electronică", result.ExplanationRo);
        Assert.Contains("interpretarea noastră", result.ExplanationRo);
        Assert.Contains("Codul de procedură fiscală, Anexa nr. 1, secțiunea III, subsecțiunea B, pct. 16", result.ExplanationRo);
        Assert.Contains("Comentariul OCDE la Secțiunea III, pct. 16", result.ExplanationRo);
        Assert.Contains("testul adresei de rezidență", result.ExplanationRo);
        Assert.Contains("Anexa nr. 2, pct. 1", result.ExplanationRo);
        Assert.Contains("lege", result.ExplanationRo);
    }

    [Theory]
    [InlineData(2010, 1, 1)] // preexisting for FATCA
    [InlineData(2020, 3, 1)] // new for FATCA
    public void Fatca_has_no_deadline_in_the_guide(int year, int month, int day)
    {
        var openedOn = new DateOnly(year, month, day);

        var result = _calculator.Compute(new DeadlineInput(ChangeAt, Regime.Fatca, openedOn));

        Assert.Equal("FATCA-SC", result.RuleId);
        Assert.Equal("38", result.GuidePage);
        Assert.Null(result.DueAt);   // GOLDEN-CASES Î1: we do not borrow the CRS 90 days
        Assert.Null(result.RemindAt);
        Assert.Equal(Preexisting.For(Regime.Fatca, openedOn), result.Preexisting);
    }

    /// <summary>Guide p. 20: CRS preexisting = held on 31 Dec 2015; FATCA preexisting = held on 30 Jun 2014.</summary>
    [Theory]
    [InlineData(Regime.Crs, 2015, 12, 31, true)]
    [InlineData(Regime.Crs, 2016, 1, 1, false)]
    [InlineData(Regime.Fatca, 2014, 6, 30, true)]
    [InlineData(Regime.Fatca, 2014, 7, 1, false)]
    public void Preexisting_depends_on_the_regime_cutoff(Regime regime, int year, int month, int day, bool preexisting)
    {
        Assert.Equal(preexisting, Preexisting.For(regime, new DateOnly(year, month, day)));
    }

    [Fact]
    public void The_same_account_is_preexisting_for_crs_and_new_for_fatca()
    {
        var openedOn = new DateOnly(2015, 1, 1);

        Assert.True(Preexisting.For(Regime.Crs, openedOn));
        Assert.False(Preexisting.For(Regime.Fatca, openedOn));

        var crs = _calculator.Compute(new DeadlineInput(ChangeAt, Regime.Crs, openedOn));
        var fatca = _calculator.Compute(new DeadlineInput(ChangeAt, Regime.Fatca, openedOn));
        Assert.True(crs.Preexisting);
        Assert.Equal("DL-PRE-YEAR", crs.RuleId);
        Assert.False(fatca.Preexisting);
        Assert.Equal("FATCA-SC", fatca.RuleId);
    }

    [Fact]
    public void Every_result_is_explained_in_both_languages_with_the_catalog_rule_and_page()
    {
        DeadlineInput[] inputs =
        [
            new(ChangeAt, Regime.Crs, NewAccount),
            new(ChangeAt, Regime.Crs, PreexistingAccount),
            new(Utc("2026-03-15T07:00:00"), Regime.Crs, PreexistingAccount),
            new(ChangeAt, Regime.Fatca, NewAccount),
            new(ChangeAt, Regime.Fatca, PreexistingAccount),
        ];

        foreach (var input in inputs)
        {
            var result = _calculator.Compute(input);
            var rule = RuleCatalog.Get(result.RuleId); // throws for an id missing from the catalog
            Assert.Equal(rule.GuidePage, result.GuidePage);
            Assert.False(string.IsNullOrWhiteSpace(result.Explanation), $"{input}: empty Explanation");
            Assert.False(string.IsNullOrWhiteSpace(result.ExplanationRo), $"{input}: empty ExplanationRo");
            Assert.NotEqual(result.Explanation, result.ExplanationRo);
        }
    }

    private static DateTime Utc(string iso) =>
        DateTime.SpecifyKind(DateTime.ParseExact(iso, "yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), DateTimeKind.Utc);
}
