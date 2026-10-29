using System.Text.Json;
using System.Text.RegularExpressions;
using HackathonStarter.Agents;
using HackathonStarter.Config;
using HackathonStarter.Features.Cases;
using HackathonStarter.Features.Checks;
using HackathonStarter.Tests.Support;

namespace HackathonStarter.Tests.Crs.Sentinel;

/// <summary>
/// The scripted mock (PLAN 13.3, the main demo path): a state machine over the case in the database, because the mock sees
/// only the last message. Maria end to end: run 1, approval, day 86 with the answer, run 2, approval, the proof.
/// </summary>
public class AgentMockTests
{
    private const string Prefix = $"Case: {MariaWorld.CaseCode}\n\n";

    [Fact]
    public async Task Run_1_prepares_the_case_and_stops_at_the_approval_of_the_request()
    {
        using var _ = new UiLanguage("ro-RO");
        var world = new MariaWorld();
        await using var host = await world.HostAsync();

        var run = await host.RunAsync("crs-sentinel", Prefix + "Pregătește cazul");

        Assert.Equal(RunSource.Mock, run.Source);
        Assert.Equal(RunStatus.AwaitingApproval, run.Status);
        Assert.Equal(["getCase", "listDocuments", "readDocument", "recordEvidence", "recordSignal", "draftClientRequest"], run.ToolCalls());
        Assert.DoesNotContain(run.Steps, s => s is { Kind: StepKind.ToolResult, Error: not null });
        var approval = Assert.Single(run.Steps, s => s.Kind == StepKind.Approval);
        Assert.Equal("sendClientRequest", approval.Tool);
        Assert.Contains(MariaWorld.CaseCode, approval.Summary);
        Assert.Contains("email", approval.Summary);

        var phone = Assert.Single(world.Case.Evidence, e => e.LinkedRuleId == "CRS-IND-PHONE");
        Assert.True(phone.Verified);
        Assert.StartsWith("Telefon:", phone.Quote);
        var signal = Assert.Single(world.Case.Signals);
        Assert.True(world.Case.Evidence.Single(e => e.Id == signal.EvidenceId).Verified);
        var request = Assert.Single(world.Case.Requests);
        Assert.Equal(RequestStatus.Draft, request.Status);
        Assert.Contains("26.01.2027", request.Body);
        Assert.Contains(FakeCases.RequiredDocument, request.Body);
        Assert.Equal(CaseStatus.Open, world.Case.Status);

        var answer = run.Answer();
        Assert.Contains("CRS-IND-ADDR", answer);
        Assert.Contains("CRS-IND-PHONE", answer);
        Assert.Contains("p. 29", answer);
        Assert.Contains("semnal, fără alertă", answer);
        Assert.Contains("„Angajator: Müller Logistik GmbH”", answer);
        Assert.Contains("26.01.2027", answer);
        Assert.Contains("aprob", answer);
        AssertSafe(run);
    }

    [Fact]
    public async Task A_pending_draft_is_not_drafted_again()
    {
        using var _ = new UiLanguage("ro-RO");
        var world = new MariaWorld();
        await using var host = await world.HostAsync();
        await host.RunAsync("crs-sentinel", Prefix + "Pregătește cazul");

        var again = await host.RunAsync("crs-sentinel", Prefix + "Pregătește cazul");

        Assert.Equal(RunStatus.Completed, again.Status);
        Assert.Equal(["getCase"], again.ToolCalls());
        Assert.Single(world.Case.Requests);
        Assert.Contains("aprobare", again.Answer());
        AssertSafe(again);
    }

    [Fact]
    public async Task Maria_end_to_end_two_runs_two_approvals_and_the_proof()
    {
        using var _ = new UiLanguage("ro-RO");
        var world = new MariaWorld();
        await using var host = await world.HostAsync();
        var runner = host.Get<AgentRunner>();
        var agent = host.Agent("crs-sentinel");

        // Run 1, then the operator approves the request: the tool runs without a model.
        var run1 = await host.RunAsync("crs-sentinel", Prefix + "Pregătește cazul");
        var sent = await runner.DecideAsync(agent, run1.Id, run1.Steps.Single(s => s.Kind == StepKind.Approval).Id, approved: true);
        Assert.DoesNotContain(sent.Steps, s => s is { Kind: StepKind.ToolResult, Error: not null });
        Assert.Equal(CaseStatus.AwaitingClient, world.Case.Status);
        Assert.Equal("email", world.Case.Requests.Single().Channel);

        // Before the answer: what is awaited and until when.
        var waiting = await host.RunAsync("crs-sentinel", Prefix + "Ce mai așteptăm?");
        Assert.Equal(RunStatus.Completed, waiting.Status);
        Assert.Equal(["getCase", "listDocuments"], waiting.ToolCalls());
        Assert.Contains(FakeCases.RequiredDocument, waiting.Answer());
        Assert.Contains("26.01.2027", waiting.Answer());
        AssertSafe(waiting);

        // Day 86: reminder, escalation, then the client's new self-certification arrives.
        world.DeliverAnswer();
        var run2 = await host.RunAsync("crs-sentinel", Prefix + "Clientul a răspuns. Verifică autocertificarea.");

        Assert.Equal(RunStatus.AwaitingApproval, run2.Status);
        Assert.Equal(["getCase", "listDocuments", "extractSelfCertification", "checkTin", "registerResponse", "evaluateRules"], run2.ToolCalls());
        Assert.DoesNotContain(run2.Steps, s => s is { Kind: StepKind.ToolResult, Error: not null });
        Assert.Equal("closeCase", Assert.Single(run2.Steps, s => s.Kind == StepKind.Approval).Tool);
        Assert.Equal(CaseStatus.ResponseReceived, world.Case.Status);
        var response = Assert.Single(world.Case.Responses);
        Assert.Equal(ResponseKind.NewSelfCertification, response.Kind);
        Assert.Equal(MariaWorld.SelfCertDocumentId, response.DocumentId);
        Assert.Equal(2, response.SelfCertificationId);
        var answer2 = run2.Answer();
        Assert.Contains("Compliant", answer2);
        Assert.Contains("DE", answer2);
        Assert.Contains("format valid", answer2);
        Assert.Contains("CRS-IND-ADDR", answer2);
        Assert.Contains("p. 29", answer2);
        Assert.Contains("aprob", answer2);
        AssertSafe(run2);

        // The operator approves the closing.
        var closed = await runner.DecideAsync(agent, run2.Id, run2.Steps.Single(s => s.Kind == StepKind.Approval).Id, approved: true);
        Assert.DoesNotContain(closed.Steps, s => s is { Kind: StepKind.ToolResult, Error: not null });
        Assert.Equal(CaseStatus.Closed, world.Case.Status);
        Assert.Equal(Features.Cases.ConfirmationChannel.Online, world.Case.Proof!.Channel);

        // Closed: the proof.
        var proof = await host.RunAsync("crs-sentinel", Prefix + "Rezumă cazul");
        Assert.Equal(["getCase"], proof.ToolCalls());
        Assert.Contains("Online", proof.Answer());
        Assert.Contains("DE", proof.Answer());
        Assert.Contains("21.01.2027", proof.Answer());
        AssertSafe(proof);
    }

    [Theory]
    [InlineData("ro-RO", "Clientul a răspuns. Verifică autocertificarea.")]
    [InlineData("en-GB", "The client replied. Check the self-certification.")]
    public async Task A_missing_saved_id_stops_the_mock_before_registering_a_response_or_requesting_close_approval(string culture, string message)
    {
        using var _ = new UiLanguage(culture);
        var world = new MariaWorld();
        world.DeliverAnswer();
        world.Changes.AfterRecord = recorded => recorded with { SelfCertificationId = null };
        await using var host = await world.HostAsync();

        var run = await host.RunAsync("crs-sentinel", Prefix + message);

        Assert.Equal(RunSource.Mock, run.Source);
        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(["getCase", "listDocuments", "extractSelfCertification"], run.ToolCalls());
        Assert.DoesNotContain(run.Steps, s => s.Kind == StepKind.Approval);
        Assert.Single(world.Changes.Requests);
        Assert.Equal(2, world.Clients.Maria.SelfCertifications.Count);
        Assert.Empty(world.Case.Responses);
        Assert.Equal(CaseStatus.Escalated, world.Case.Status);
        Assert.Null(world.Case.Proof);
        var extraction = Assert.Single(run.Steps, s => s is { Kind: StepKind.ToolResult, Tool: "extractSelfCertification" });
        Assert.Null(extraction.Error);
        var result = Json(extraction.Output) ?? throw new InvalidOperationException("Missing extraction tool result.");
        var error = result.GetProperty("error").GetString();
        Assert.Contains("no selfCertificationId", error);
        Assert.Contains("Do not call registerResponse or closeCase", error);
        Assert.Contains(error!, run.Answer());
        AssertSafe(run);
    }

    [Fact]
    public async Task ResponseReceived_proposes_the_closing()
    {
        using var _ = new UiLanguage("ro-RO");
        var world = new MariaWorld();
        world.DeliverAnswer();
        await using var host = await world.HostAsync();
        await host.ToolAsync("extractSelfCertification", new { caseCode = MariaWorld.CaseCode, documentId = MariaWorld.SelfCertDocumentId });
        await host.ToolAsync("registerResponse", new { caseCode = MariaWorld.CaseCode, kind = "NewSelfCertification", documentId = 12, selfCertificationId = 2 });

        var run = await host.RunAsync("crs-sentinel", Prefix + "Continuă");

        Assert.Equal(["getCase", "evaluateRules"], run.ToolCalls());
        Assert.Equal("closeCase", Assert.Single(run.Steps, s => s.Kind == StepKind.Approval).Tool);
        Assert.Contains("Compliant", run.Answer());
        AssertSafe(run);
    }

    [Fact]
    public async Task The_mock_finds_the_case_from_the_client_code_and_answers_in_english()
    {
        using var _ = new UiLanguage("en-GB");
        var world = new MariaWorld();
        await using var host = await world.HostAsync();

        var run = await host.RunAsync("crs-sentinel", "Prepare the case of client CL-0001");

        Assert.Equal(RunStatus.AwaitingApproval, run.Status);
        Assert.Equal("getCase", run.ToolCalls()[0]);
        var answer = run.Answer();
        Assert.Contains("CRS-IND-PHONE", answer);
        Assert.Contains("signal, no alert", answer);
        Assert.Contains("approv", answer);
        Assert.DoesNotContain("semnal", answer);
        AssertSafe(run);
    }

    [Fact]
    public async Task A_viewer_gets_a_read_only_summary()
    {
        using var _ = new UiLanguage("ro-RO");
        var world = new MariaWorld();
        await using var host = await world.HostAsync(user: SiteConfig.Users.Single(u => u.Role == Roles.Viewer));

        var run = await host.RunAsync("crs-sentinel", Prefix + "Pregătește cazul");

        Assert.Equal(RunStatus.Completed, run.Status);
        Assert.Equal(["getCase"], run.ToolCalls());
        Assert.Empty(world.Case.Requests);
        Assert.Contains("CRS-IND-ADDR", run.Answer());
        AssertSafe(run);
    }

    [Fact]
    public async Task Without_a_code_the_mock_asks_for_one()
    {
        using var _ = new UiLanguage("ro-RO");
        var world = new MariaWorld();
        await using var host = await world.HostAsync();

        var run = await host.RunAsync("crs-sentinel", "Bună!");

        Assert.Empty(run.ToolCalls());
        Assert.Contains("CRS-", run.Answer());
    }

    /// <summary>What every mock answer must respect: checks pass, no masked value repeated, "format valid" wording, quotes only verified.</summary>
    private static void AssertSafe(AgentRun run)
    {
        var check = Assert.Single(run.Steps, s => s.Kind == StepKind.Check);
        Assert.All(check.Checks!, c => Assert.NotEqual(CheckOutcome.Fail, c.Outcome));
        var answer = run.Answer();
        Assert.DoesNotContain("+49", answer);
        Assert.DoesNotContain("4521", answer);
        Assert.DoesNotContain("RO19", answer);
        Assert.DoesNotContain("2910608400319", answer);
        Assert.DoesNotContain("86095174223", answer);
        Assert.DoesNotContain("TIN valid", answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"", answer);

        // A quote in the answer is the input of a tool call whose result says verified = true.
        var verified = run.Steps
            .Where(s => s is { Kind: StepKind.ToolResult, Error: null } && Json(s.Output) is { } o
                        && o.TryGetProperty("verified", out var v) && v.ValueKind == JsonValueKind.True)
            .SelectMany(r => run.Steps.Where(c => c.Kind == StepKind.ToolCall && c.Id == r.Id).SelectMany(c => Json(c.Input)?.Strings() ?? []))
            .ToHashSet();
        foreach (Match quote in Regex.Matches(answer, "„([^”]*)”"))
            Assert.Contains(quote.Groups[1].Value, verified);
    }

    private static JsonElement? Json(string? text) => text is null ? null : JsonDocument.Parse(text).RootElement.Clone();
}
