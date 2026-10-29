using HackathonStarter.Features.Clients;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Rules;

namespace HackathonStarter.Tests.Crs;

public class FatcaDeadlineMetadataTests
{
    [Theory]
    [InlineData("5c")]
    [InlineData("6")]
    [InlineData("7")]
    [InlineData("14")]
    public void Fatca_golden_cards_match_no_deadline_metadata_for_every_account(string id)
    {
        var card = GoldenCases.Get(id);
        var receivedAt = Assert.IsType<DateTime>(card.ReceivedAt);
        var before = Assert.IsType<ClientSnapshot>(card.Before);
        var after = Assert.IsType<ClientSnapshot>(card.After);
        var expected = Assert.IsType<GoldenDeadline>(card.ExpectedDeadline);

        Assert.Equal(Regime.Fatca, card.ExpectedRegime);
        Assert.Null(expected.DueAt);
        Assert.Null(expected.RemindAt);

        var calculator = new DeadlineCalculator();
        Assert.All(new[] { before, after }, snapshot =>
        {
            Assert.NotEmpty(snapshot.Accounts);
            if (id == "5c") Assert.Equal(2, snapshot.Accounts.Count);

            Assert.All(snapshot.Accounts, account =>
            {
                var actual = calculator.Compute(new DeadlineInput(receivedAt, Regime.Fatca, account.OpenedOn));

                Assert.Equal(card.ExpectedDeadlineRule, actual.RuleId);
                Assert.Equal(expected.RuleId, actual.RuleId);
                Assert.Equal("FATCA-SC", actual.RuleId);
                Assert.Equal("38", actual.GuidePage);
                Assert.Equal(Preexisting.For(Regime.Fatca, account.OpenedOn), actual.Preexisting);
                Assert.Equal(expected.Preexisting, actual.Preexisting);
                Assert.Null(actual.DueAt);
                Assert.Null(actual.RemindAt);
            });
        });
    }
}
