using HackathonStarter.Agents;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Sentinel;
using HackathonStarter.Tests.Support;
using Microsoft.Extensions.AI;

namespace HackathonStarter.Tests.Crs.Sentinel;

/// <summary>The live path with a scripted model (FakeChatClient): the model's tool calls hit the fakes, the approval stops the run.</summary>
public class AgentLiveTests
{
    private const string Letter = """
        Bună ziua, vă rugăm să ne confirmați țara în care aveți rezidența fiscală.
        Am primit o adresă și un telefon noi, din Germania. Vă rugăm să ne trimiteți până la 26.01.2027:
        - Autocertificare nouă privind rezidența fiscală, semnată și datată
        Vă mulțumim,
        Banca Demo
        """;

    [Fact]
    public async Task Run_1_live_calls_the_tools_on_the_fakes_and_ends_awaiting_approval_at_sendClientRequest()
    {
        var model = new FakeChatClient()
            .CallTool("getCase", new { caseCode = MariaWorld.CaseCode })
            .CallTool("listDocuments", new { clientCode = MariaWorld.ClientCode })
            .CallTool("readDocument", new { caseCode = MariaWorld.CaseCode, documentId = MariaWorld.PayslipId })
            .CallTool("recordEvidence", new
            {
                caseCode = MariaWorld.CaseCode, documentId = MariaWorld.PayslipId, page = 1, quote = "Telefon: +49 89 4521 7730", ruleId = "CRS-IND-PHONE",
            })
            .CallTool("recordSignal", new
            {
                caseCode = MariaWorld.CaseCode, description = "Salariu de la Müller Logistik GmbH", documentId = MariaWorld.PayslipId, page = 1,
                quote = "Angajator: Müller Logistik GmbH",
            })
            .CallTool("draftClientRequest", new { caseCode = MariaWorld.CaseCode, body = Letter })
            .CallTool("sendClientRequest", new { caseCode = MariaWorld.CaseCode, requestId = 1 })
            .Reply("Am pregătit cazul CRS-2026-0001. Trimiterea cererii pe email așteaptă aprobarea.")
            .CallTool("getCase", new { caseCode = MariaWorld.CaseCode }); // never asked for: the run ends after the proposal
        var world = new MariaWorld();
        await using var host = await world.HostAsync(model);

        var run = await host.RunAsync("crs-sentinel", $"Case: {MariaWorld.CaseCode}\n\nPregătește cazul");

        Assert.Equal(RunSource.Live, run.Source);
        Assert.Equal(RunStatus.AwaitingApproval, run.Status);
        Assert.Equal(["getCase", "listDocuments", "readDocument", "recordEvidence", "recordSignal", "draftClientRequest"], run.ToolCalls());
        Assert.DoesNotContain(run.Steps, s => s is { Kind: StepKind.ToolResult, Error: not null });
        var approval = Assert.Single(run.Steps, s => s.Kind == StepKind.Approval);
        Assert.Equal("sendClientRequest", approval.Tool);
        Assert.Contains("Bună ziua, vă rugăm să ne confirmați țara în care aveți rezidența fiscală.", approval.Summary);
        Assert.Equal("Am pregătit cazul CRS-2026-0001. Trimiterea cererii pe email așteaptă aprobarea.", run.Steps.Last(s => s.Kind == StepKind.Text).Text);
        Assert.Equal(8, model.Calls.Count);
        Assert.IsType<NoneChatToolMode>(model.CallOptions[^1]!.ToolMode);
        Assert.StartsWith(SentinelPrompt.Text, model.Calls[0][0].Text);

        Assert.True(Assert.Single(world.Case.Evidence, e => e.LinkedRuleId == "CRS-IND-PHONE").Verified);
        Assert.Single(world.Case.Signals);
        Assert.Equal(RequestStatus.Draft, Assert.Single(world.Case.Requests).Status);
        Assert.Equal(CaseStatus.Open, world.Case.Status); // nothing is sent before a person approves
    }
}
