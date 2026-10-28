using HackathonStarter.Features.Changes;
using HackathonStarter.Features.Deadlines;
using HackathonStarter.Features.Evaluation;
using HackathonStarter.Features.Rules;
using HackathonStarter.Features.Shield;
using HackathonStarter.Features.Tin;
using Microsoft.Extensions.DependencyInjection;

namespace HackathonStarter.Tests.Crs.RuleEngine;

/// <summary>
/// R5's golden data (GoldenCases.Ready(GoldenLayer.Engine)) through the registered engine (AddRules + AddTin), compared by
/// EvaluationService itself: indicia exact on (RuleId, Jurisdiction, CoveredBy), Cleared as a subset, the decision and
/// ReportableFor. EngineCases is R1's copy of the spec; this is the guard on the shared data /metrics reports.
/// </summary>
public class GoldenDataEngineTests
{
    private static readonly ServiceProvider Services = new ServiceCollection()
        .AddRules().AddTin().AddDeadlines().AddShield().BuildServiceProvider();

    public static TheoryData<string> EngineIds() => [.. GoldenCases.Ready(GoldenLayer.Engine).Select(c => c.Id)];

    // Owner R1: EvaluationService compares a workflow-owned (R2) row only on decision and deadline; the engine layer of the
    // case is compared in full. A self-certification's deadline is not compared: the guide gives none for a corrected form
    // (GOLDEN-CASES Î1, case 2), while IDeadlineCalculator answers by regime (R2's, see EvaluationServiceTests).
    private static GoldenCase EngineLayer(GoldenCase c) => c with
    {
        Owner = GoldenOwner.R1,
        ExpectedDeadline = c.Change!.Kind == ChangeKind.SelfCertSubmitted ? null : c.ExpectedDeadline,
    };

    private static async Task<CoverageRow> Row(GoldenCase golden)
    {
        var service = new EvaluationService(
            Services.GetRequiredService<ICrsRuleEngine>(), Services.GetRequiredService<IDeadlineCalculator>(),
            Services.GetRequiredService<IHiddenTextShield>(), Services.GetRequiredService<IEvidenceVerifier>(),
            Services.GetRequiredService<ISimClock>(), inputs: new EvaluationInputs(() => [golden], () => [], () => []));
        return Assert.Single((await service.RunAsync()).Spec.Golden);
    }

    [Theory]
    [MemberData(nameof(EngineIds))]
    public async Task Golden_data_passes_the_evaluation_comparison(string id)
    {
        var row = await Row(EngineLayer(GoldenCases.Get(id)));

        Assert.True(row.Evaluated, $"{id}: {row.Status}");
        Assert.Null(row.Error);
        Assert.Equal(row.Expected, row.Actual);
        Assert.True(row.IndiciaMatch,
            $"{id}: missing [{string.Join(", ", row.MissingIndicia)}], unexpected [{string.Join(", ", row.UnexpectedIndicia)}]");
        Assert.True(row.ClearedMatch, $"{id}: cleared missing [{string.Join(", ", row.MissingCleared)}]");
        Assert.NotEqual(false, row.ReportableMatches);
        Assert.NotEqual(false, row.Deadline?.Matches);
        Assert.Equal(CoverageStatus.Passed, row.Status);
    }

    [Fact]
    public void The_engine_layer_has_golden_data()
    {
        Assert.Contains(GoldenCases.Ready(GoldenLayer.Engine), c => c.Id == "7");
        Assert.Contains(GoldenCases.Ready(GoldenLayer.Engine), c => c.Id == "12");
        Assert.IsType<CrsRuleEngine>(Services.GetRequiredService<ICrsRuleEngine>());
        Assert.IsType<TinFormat>(Services.GetRequiredService<ITinFormat>());
    }
}
