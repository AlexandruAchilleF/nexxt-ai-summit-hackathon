using Amazon;
using Amazon.BedrockRuntime;
using Amazon.Runtime.Credentials;
using HackathonStarter.Agents;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Sentinel;
using HackathonStarter.Features.Sentinel.Demo;
using Microsoft.Extensions.AI;
using Xunit.Abstractions;

namespace HackathonStarter.Tests.Crs.Sentinel;

/// <summary>Runs only with CRS_LIVE=1 and AWS credentials in the environment: it calls Amazon Bedrock.</summary>
public sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("CRS_LIVE") != "1") Skip = "Live model test: set CRS_LIVE=1 (calls Amazon Bedrock, us-west-2).";
    }
}

/// <summary>
/// Maria end to end on the live model (Sonnet on Bedrock, the extraction on Haiku), on the same real CRS modules and seed as
/// <see cref="SentinelMariaTests"/>. The model may choose its own words and tool order; what code guarantees must hold anyway:
/// the run stops at each approval, nothing is sent or closed before a person approves, evidence is verified, the checks pass,
/// and the case closes with the proof. Prints every step so the run can be read.
/// </summary>
public class SentinelLiveTests(ITestOutputHelper output)
{
    private const string CaseCode = "CRS-2026-0001";
    private const string SonnetModel = "us.anthropic.claude-sonnet-5-5";
    private const string HaikuModel = "us.anthropic.claude-haiku-5-5";

    private static IChatClient Bedrock(string model) =>
        new AmazonBedrockRuntimeClient(DefaultAWSCredentialsIdentityResolver.GetCredentials(),
                new AmazonBedrockRuntimeConfig { RegionEndpoint = RegionEndpoint.USWest2, MaxErrorRetry = 1 })
            .AsIChatClient(model)
            .AsBuilder()
            .ConfigureOptions(o => o.MaxOutputTokens ??= 4096)
            .Build();

    [LiveFact]
    public async Task Maria_on_the_live_model_stops_at_both_approvals_and_closes_with_the_proof()
    {
        using var _ = new UiLanguage("ro-RO");
        var (host, clock) = await RealWorld.CreateAsync(Bedrock(SonnetModel),
            profiles: new Dictionary<string, IChatClient> { ["extract"] = Bedrock(HaikuModel) },
            ai: o => { o.Model = SonnetModel; o.TimeoutSeconds = 120; });
        await using var __ = host;

        var change = await host.RecordGoldenChangeAsync("4");
        Assert.Equal(CaseCode, change.CaseCode);

        var run1 = await host.RunAsync(SentinelTools.AgentName, $"{CaseCode}\n\n{SentinelActions.Analyze(CaseCode)}");
        Print("run 1", run1);
        Assert.Equal(RunSource.Live, run1.Source);
        Assert.Equal(RunStatus.AwaitingApproval, run1.Status);
        Assert.Equal(SentinelTools.SendClientRequest, Assert.Single(run1.Steps, s => s.Kind == StepKind.Approval).Tool);
        run1.ChecksPass();
        var prepared = (await host.CaseAsync(CaseCode))!;
        Assert.Equal(CaseStatus.Open, prepared.Status);
        Assert.Contains(prepared.Evidence, e => e.Verified && e.LinkedRuleId == "CRS-IND-PHONE");
        Assert.All(prepared.Evidence, e => Assert.True(e.Verified, e.Quote));
        Assert.Equal(RequestStatus.Draft, Assert.Single(prepared.Requests).Status);

        await host.DecideAsync(run1, SentinelTools.SendClientRequest);
        Assert.Equal(CaseStatus.AwaitingClient, (await host.CaseAsync(CaseCode))!.Status);

        clock.Advance(TimeSpan.FromDays(86));
        await host.Get<ICaseService>().TickAsync(clock.UtcNow);
        await host.Get<IDemoScenarios>().DeliverResponseAsync(CaseCode);

        var run2 = await host.RunAsync(SentinelTools.AgentName, $"{CaseCode}\n\n{SentinelActions.Verify(CaseCode)}");
        Print("run 2", run2);
        Assert.Equal(RunSource.Live, run2.Source);
        Assert.Equal(RunStatus.AwaitingApproval, run2.Status);
        Assert.Equal(SentinelTools.CloseCase, Assert.Single(run2.Steps, s => s.Kind == StepKind.Approval).Tool);
        run2.ChecksPass();
        Assert.Equal(CaseStatus.ResponseReceived, (await host.CaseAsync(CaseCode))!.Status);

        await host.DecideAsync(run2, SentinelTools.CloseCase);
        var closed = (await host.CaseAsync(CaseCode))!;
        Assert.Equal(CaseStatus.Closed, closed.Status);
        Assert.Equal(["DE"], closed.ReportableFor);
        Assert.NotNull(closed.Proof);
        Assert.True((await host.VerifyAuditAsync()).Ok);
    }

    private void Print(string label, AgentRun run)
    {
        var seconds = run.FinishedAt is { } f ? (f - run.CreatedAt).TotalSeconds : double.NaN;
        output.WriteLine($"== {label}: source={run.Source} model={run.Model} status={run.Status} seconds={seconds:F1} tokens in={run.InputTokens} out={run.OutputTokens} masked={run.MaskedCount} fallback={run.FallbackReason ?? "-"} error={run.Error ?? "-"}");
        foreach (var s in run.Steps)
        {
            var text = s.Kind switch
            {
                StepKind.ToolCall => $"{s.Tool} {Cut(s.Input, 300)}",
                StepKind.ToolResult => Cut(s.Output ?? s.Error, 500),
                StepKind.Approval => $"{s.Tool} state={s.State} {Cut(s.Summary, 300)}",
                StepKind.Check => string.Join("; ", s.Checks!.Select(c => $"{c.CheckId}={c.Outcome}{(c.Outcome == Features.Checks.CheckOutcome.Fail ? " " + c.Reason : "")}")),
                _ => Cut(s.Text, 2000),
            };
            output.WriteLine($"[{s.Kind}] {text}");
        }
    }

    private static string Cut(string? s, int max) => s is null ? "" : s.Length <= max ? s : s[..max] + "…";
}
