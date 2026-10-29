using HackathonStarter.Components.Crs;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Tin;
using Microsoft.Extensions.DependencyInjection;

namespace HackathonStarter.Tests.Crs.Components;

public class ChangeVerdictTests
{
    private static readonly ICrsRuleEngine Engine = new ServiceCollection().AddRules().AddTin()
        .BuildServiceProvider().GetRequiredService<ICrsRuleEngine>();

    public static TheoryData<string> GoldenIds() => [.. GoldenCases.Ready(GoldenLayer.Engine).Select(c => c.Id)];

    [Theory]
    [MemberData(nameof(GoldenIds))]
    public void Promoted_rule_agrees_with_every_golden_decision(string id)
    {
        var c = GoldenCases.Get(id);
        var outcome = c.Change!.Kind == ChangeKind.SelfCertSubmitted
            ? Engine.EvaluateSelfCertification(c.After!, c.Change.SelfCertification!)
            : Engine.Evaluate(c.Before!, c.After!, c.Change.Kind);
        Assert.Equal(c.ExpectedDecision, outcome.Decision);
        var promoted = ChangeVerdict.Select(outcome);
        var expected = outcome.Decision switch
        {
            Decision.FatcaAlert => outcome.Uncovered.FirstOrDefault(h => h.Regime == Regime.Fatca),
            Decision.Clarification => outcome.Uncovered.FirstOrDefault(h => h.Regime == Regime.Crs),
            Decision.Compliant => outcome.Indicia.FirstOrDefault(h => h.CoveredBy != Coverage.None),
            Decision.NoCase => outcome.Cleared.FirstOrDefault(),
            _ => null,
        };
        Assert.Equal(expected, promoted?.Hit);
        if (promoted is null) return;
        Assert.Equal(outcome.Decision switch
        {
            Decision.NoCase => PromotedRuleRole.Cleared,
            Decision.Compliant => PromotedRuleRole.Covered,
            _ => PromotedRuleRole.Uncovered,
        }, promoted.Role);
        Assert.False(string.IsNullOrWhiteSpace(promoted.Hit.GuidePage));
        if (id == "9")
        {
            Assert.Equal(Coverage.SelfCertification, promoted.Hit.CoveredBy);
            Assert.NotEqual("SC-CONSISTENT", promoted.Hit.RuleId);
        }
    }

    [Theory]
    [InlineData(Decision.FatcaAlert, Regime.Fatca)]
    [InlineData(Decision.Clarification, Regime.Crs)]
    public void Mixed_findings_promote_the_decisions_regime(Decision decision, Regime regime)
    {
        var crs = Hit(Regime.Crs);
        var fatca = Hit(Regime.Fatca);
        var outcome = new RuleOutcome(decision, [crs, fatca], [], [], "", "", "test");
        Assert.Equal(regime, ChangeVerdict.Select(outcome)!.Hit.Regime);
    }

    [Theory]
    [InlineData(Decision.FatcaAlert)]
    [InlineData(Decision.Clarification)]
    [InlineData(Decision.Compliant)]
    [InlineData(Decision.NoCase)]
    [InlineData(Decision.ShieldAlert)]
    public void No_matching_role_promotes_nothing(Decision decision)
    {
        var outcome = new RuleOutcome(decision, [], [], [], "", "", "test");
        Assert.Null(ChangeVerdict.Select(outcome));
    }

    [Theory]
    [InlineData(37)]
    [InlineData(90)]
    public void New_case_duration_comes_from_its_dates(int days)
    {
        var opened = new DateTime(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);
        var item = new Case { TriggerChangeId = 42, OpenedAt = opened, DueAt = opened.AddDays(days) };
        Assert.Equal(days, ChangeVerdict.DeadlineDays(item, 42));
    }

    [Fact]
    public void Extended_case_does_not_count_from_its_original_opening()
    {
        var opened = new DateTime(2026, 7, 21, 10, 0, 0, DateTimeKind.Utc);
        var item = new Case { TriggerChangeId = 41, OpenedAt = opened, DueAt = opened.AddDays(190) };
        Assert.Null(ChangeVerdict.DeadlineDays(item, 42));
        item.DueAt = null;
        Assert.Null(ChangeVerdict.DeadlineDays(item, 41));
    }

    private static RuleHit Hit(Regime regime) => new("test", regime, "DE", "", "", "29", "");
}
